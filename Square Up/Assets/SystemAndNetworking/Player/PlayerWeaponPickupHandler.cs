using Fusion;
using UnityEngine;

public class PlayerWeaponPickupHandler : NetworkBehaviour
{
    // =========================================================
    //  INSPECTOR
    // =========================================================

    [SerializeField] private float pickupCheckRadius = 2f;
    [SerializeField] private float throwForce = 8f;

    // =========================================================
    //  NETWORKED STATE
    // =========================================================

    // Prevents the "instant re-pickup" by gating interactions,
    // even across resimulation ticks.
    [Networked] private TickTimer InteractionCooldown { get; set; }

    // =========================================================
    //  PRIVATE FIELDS
    // =========================================================

    private PlayerData _playerData;

    // =========================================================
    //  UNITY / FUSION LIFECYCLE
    // =========================================================

    private void Awake() => _playerData = GetComponent<PlayerData>();

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input)) return;
        if (_playerData != null && _playerData.Dead) return;
        if (!input.buttons.IsSet(MyButtons.Pickup)) return;
        if (!InteractionCooldown.ExpiredOrNotRunning(Runner)) return;

        InteractionCooldown = TickTimer.CreateFromSeconds(Runner, 0.25f);

        Vector2 aimDir = input.aimDirection.magnitude > 0.1f ? input.aimDirection.normalized : (Vector2)transform.up;
        Vector2 throwVelocity = (aimDir + Vector2.up * 0.4f).normalized * throwForce;

        // BUG FIX: Previously the client sent RPC_RequestInteraction which called
        // ExecutePickupOrDrop on the server using Object.InputAuthority — this is
        // correct. But the scan was finding no weapon because the client's
        // WeaponAimController.SetCurrentWeapon only runs its state-setting branch
        // on the authority, so CurrentWeaponId was never cleared after a drop,
        // and ResolveWeaponReference kept restoring _currentWeapon, making the
        // weapon appear still held even after dropping.
        //
        // The fix: ExecutePickupOrDrop now passes the interacting PlayerRef
        // explicitly so the server always operates on the correct player,
        // and HandleServerDrop clears weapon state via the authoritative path.
        if (Object.HasStateAuthority)
            ExecutePickupOrDrop(Object.InputAuthority, throwVelocity);
        else
            RPC_RequestInteraction(throwVelocity);
    }

    // =========================================================
    //  RPCS
    // =========================================================

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteraction(Vector2 throwVelocity)
    {
        // Object.InputAuthority is valid here — this component belongs to
        // the client's player object, so InputAuthority is the client's PlayerRef.
        ExecutePickupOrDrop(Object.InputAuthority, throwVelocity);
    }

    // =========================================================
    //  PRIVATE — CORE LOGIC
    // =========================================================

    private void ExecutePickupOrDrop(PlayerRef interactingPlayer, Vector2 throwVelocity)
    {
        // Always drop first regardless of whether a pickup is available.
        HandleServerDrop(interactingPlayer, throwVelocity);

        // Scan for the closest available weapon.
        WeaponPickup closestWeapon = null;
        float closestDist = float.MaxValue;

        WeaponPickup[] allWeapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        foreach (var weapon in allWeapons)
        {
            if (weapon == null || weapon.GetIsPickedUp()) continue;

            float dist = Vector3.Distance(transform.position, weapon.transform.position);
            if (dist <= pickupCheckRadius && dist < closestDist)
            {
                closestDist = dist;
                closestWeapon = weapon;
            }
        }

        if (closestWeapon != null)
            closestWeapon.ServerExecutePickup(interactingPlayer);
    }

    private void HandleServerDrop(PlayerRef interactingPlayer, Vector2 throwVelocity)
    {
        // Resolve the player's NetworkObject from the PlayerRef so we always
        // operate on the right player regardless of who called this method.
        NetworkObject pObj = Runner.GetPlayerObject(interactingPlayer);
        if (pObj == null) return;

        WeaponAimController aim = pObj.GetComponent<WeaponAimController>();
        if (aim == null || aim.CurrentWeapon == null) return;

        WeaponPickup toDrop = aim.CurrentWeapon;

        // Clear weapon state on the authoritative WeaponAimController before
        // calling Drop, so CurrentWeaponId is reset and ResolveWeaponReference
        // won't resurrect the reference on the next tick.
        aim.ClearCurrentWeapon();

        toDrop.Drop(transform.position, throwVelocity);

        PlayerData pd = pObj.GetComponent<PlayerData>();
        if (pd != null) pd.PickupWeapon(0);
    }
}