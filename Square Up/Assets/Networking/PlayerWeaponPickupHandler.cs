using Fusion;
using UnityEngine;

public class PlayerWeaponPickupHandler : NetworkBehaviour
{
    [SerializeField] private float pickupCheckRadius = 2f;
    [SerializeField] private float pickupCooldown = 0.3f;
    private PlayerData playerData;
    private WeaponPickup[] allWeapons;

    [Networked] private TickTimer PickupCooldownTimer { get; set; }

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
        // Must run for both Input Authority (Prediction) and State Authority (Server)
        if (GetInput(out NetworkInputData input))
        {
            // 1. Only act if the 'pickup' bit is set and the cooldown has expired
            if (input.pickup && PickupCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                if (playerData != null && playerData.Dead) return;

                // 2. Attempt the pickup/drop logic
                // We return a bool so we know if we actually picked something up
                bool didPickup = TryPickupNearbyWeapon();

                // 3. Start the cooldown immediately 
                // This prevents the "Instant Drop" because the 'E' input 
                // won't be processed again until the timer clears.
                PickupCooldownTimer = TickTimer.CreateFromSeconds(Runner, pickupCooldown);
            }
        }
    }

    private bool TryPickupNearbyWeapon()
    {
        RefreshWeaponList();

        WeaponPickup closestWeapon = null;
        float closestDistance = float.MaxValue;

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

        if (closestWeapon != null)
        {
            // PICKUP LOGIC
            closestWeapon.TryPickup(Object.InputAuthority);
            return true; // We performed a pickup
        }
        else if (playerData != null && playerData.HasWeapon())
        {
            // DROP LOGIC
            // This only runs if NO weapon was found in range
            RPC_RequestDropCurrentWeapon(Object.InputAuthority);
            return false;
        }

        return false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDropCurrentWeapon(PlayerRef playerRef, RpcInfo info = default)
    {
        // Server-side validation
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
    }
}