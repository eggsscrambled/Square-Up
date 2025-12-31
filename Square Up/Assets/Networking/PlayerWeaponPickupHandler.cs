using Fusion;
using UnityEngine;

public class PlayerWeaponPickupHandler : NetworkBehaviour
{
    [SerializeField] private float pickupCheckRadius = 2f;
    [SerializeField] private float pickupCooldown = 0.5f;
    private PlayerData playerData;
    private WeaponPickup[] allWeapons;

    [Networked] private TickTimer PickupCooldownTimer { get; set; }
    [Networked] private NetworkBool PendingPickup { get; set; } // Track if we're waiting for a pickup RPC

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
    }

    public override void Spawned()
    {
        RefreshWeaponList();
    }

    private void RefreshWeaponList()
    {
        allWeapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input))
        {
            // Only proceed if E was pressed and cooldown is over
            if (input.pickup && PickupCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                if (playerData != null && playerData.Dead) return;

                // Start cooldown immediately to prevent "double-tap" logic in re-simulations
                PickupCooldownTimer = TickTimer.CreateFromSeconds(Runner, pickupCooldown);
                HandlePickupSwapOrDrop();
            }
        }

        // Clear pending pickup flag after a short delay
        if (PendingPickup && PickupCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            PendingPickup = false;
        }
    }

    private void HandlePickupSwapOrDrop()
    {
        RefreshWeaponList();
        WeaponPickup closestWeapon = null;
        float closestDistance = float.MaxValue;

        // 1. Find the closest ground weapon
        foreach (var weapon in allWeapons)
        {
            if (weapon == null || weapon.GetIsPickedUp())
                continue;

            float distance = Vector3.Distance(transform.position, weapon.transform.position);
            if (distance <= pickupCheckRadius && distance < closestDistance)
            {
                closestDistance = distance;
                closestWeapon = weapon;
            }
        }

        // 2. SWAP OR PICKUP LOGIC
        if (closestWeapon != null)
        {
            // Set flag to prevent drop during prediction
            PendingPickup = true;

            // If we are already holding a weapon, drop it first to "Swap"
            if (playerData != null && playerData.HasWeapon())
            {
                RPC_RequestDropCurrentWeapon(Object.InputAuthority);
            }

            // Pickup the new weapon
            closestWeapon.TryPickup(Object.InputAuthority);
            return;
        }

        // 3. PURE DROP LOGIC
        // If no weapon was nearby AND we're not waiting for a pickup, drop the held weapon
        if (!PendingPickup && playerData != null && playerData.HasWeapon())
        {
            RPC_RequestDropCurrentWeapon(Object.InputAuthority);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDropCurrentWeapon(PlayerRef playerRef, RpcInfo info = default)
    {
        // Validation: Only the owner or the server can trigger this
        if (info.Source != playerRef && info.Source != PlayerRef.None) return;

        WeaponPickup heldWeapon = WeaponPickup.GetHeldWeapon(playerRef);
        if (heldWeapon != null)
        {
            Vector3 dropPosition = transform.position;
            Vector2 dropVelocity = new Vector2(Random.Range(-2f, 2f), Random.Range(2f, 4f));

            WeaponAimController aimController = GetComponent<WeaponAimController>();
            if (aimController != null)
            {
                aimController.SetCurrentWeapon(null);
            }

            heldWeapon.Drop(dropPosition, dropVelocity);

            if (playerData != null)
            {
                playerData.PickupWeapon(0);
            }
        }

        // Clear the pending pickup flag on the server after drop completes
        PendingPickup = false;
    }
}