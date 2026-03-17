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

        Vector2 aimDir = input.aimDirection.magnitude > 0.1f ? input.aimDirection.normalized : (Vector2)transform.up;
        Vector2 throwVelocity = (aimDir + Vector2.up * 0.4f).normalized * throwForce;

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
        InteractionCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);

        Debug.Log($"[Pickup] ExecutePickupOrDrop for {interactingPlayer} — tick {Runner.Tick}");

        HandleServerDrop(interactingPlayer, throwVelocity);

        WeaponPickup closestWeapon = null;
        float closestDist = float.MaxValue;

        WeaponPickup[] allWeapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        Debug.Log($"[Pickup] Scanning {allWeapons.Length} weapons");

        foreach (var weapon in allWeapons)
        {
            if (weapon == null) continue;
            bool pickedUp = weapon.GetIsPickedUp();
            float dist = Vector3.Distance(transform.position, weapon.transform.position);
            Debug.Log($"[Pickup]   — {weapon.name} | pickedUp={pickedUp} | dist={dist:F2} | inCooldown={!weapon.PickupCooldownExpired(Runner)}");

            if (pickedUp) continue;
            if (!weapon.PickupCooldownExpired(Runner)) continue;
            if (dist <= pickupCheckRadius && dist < closestDist)
            {
                closestDist = dist;
                closestWeapon = weapon;
            }
        }

        Debug.Log($"[Pickup] Result: {(closestWeapon != null ? closestWeapon.name : "none")}");
        if (closestWeapon != null)
            closestWeapon.ServerExecutePickup(interactingPlayer);
    }

    private void HandleServerDrop(PlayerRef interactingPlayer, Vector2 throwVelocity)
    {
        NetworkObject pObj = Runner.GetPlayerObject(interactingPlayer);
        if (pObj == null) return;

        WeaponAimController aim = pObj.GetComponent<WeaponAimController>();
        if (aim == null) return;

        // FIX: don't rely on _currentWeapon being resolved yet —
        // check CurrentWeaponId directly and force-resolve if needed
        if (aim.CurrentWeaponId == default) return;

        if (aim.CurrentWeapon == null)
        {
            // force resolve before dropping
            if (Runner.TryFindObject(aim.CurrentWeaponId, out NetworkObject weaponObj))
            {
                // let WeaponAimController resolve it properly next tick,
                // but grab it directly here for the drop
                WeaponPickup toDrop = weaponObj.GetComponent<WeaponPickup>();
                aim.ClearCurrentWeapon();
                toDrop?.Drop(pObj.transform.position, throwVelocity);
            }
            return;
        }

        WeaponPickup weapon = aim.CurrentWeapon;
        aim.ClearCurrentWeapon();
        weapon.Drop(pObj.transform.position, throwVelocity);

        PlayerData pd = pObj.GetComponent<PlayerData>();
        if (pd != null) pd.PickupWeapon(0);
    }
}