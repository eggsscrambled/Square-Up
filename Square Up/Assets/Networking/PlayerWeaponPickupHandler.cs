using Fusion;
using UnityEngine;

public class PlayerWeaponPickupHandler : NetworkBehaviour
{
    [SerializeField] private float pickupCheckRadius = 2f;
    [SerializeField] private float pickupCooldown = 0.3f; // Cooldown between pickups
    private PlayerData playerData;
    private WeaponPickup[] allWeapons;
    [Networked] private TickTimer PickupCooldownTimer { get; set; }
    private bool wasPickupPressed = false; // Track previous frame state

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
    }

    public override void Spawned()
    {
        // Cache all weapons in the scene once
        RefreshWeaponList();
    }

    private void RefreshWeaponList()
    {
        allWeapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        Debug.Log($"[Player {Object.InputAuthority}] Found {allWeapons.Length} total weapons in scene");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority)
            return;

        if (playerData != null && playerData.Dead)
            return;

        if (GetInput(out NetworkInputData input))
        {
            // Only trigger on the rising edge (key was just pressed, not held)
            bool pickupJustPressed = input.pickup && !wasPickupPressed;
            wasPickupPressed = input.pickup;

            if (pickupJustPressed && PickupCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                Debug.Log($"[Client {Object.InputAuthority}] E KEY INPUT RECEIVED");
                TryPickupNearbyWeapon();
            }
        }
    }

    private void TryPickupNearbyWeapon()
    {
        // Refresh weapon list periodically in case new ones spawned
        if (allWeapons == null || allWeapons.Length == 0)
        {
            RefreshWeaponList();
        }

        WeaponPickup closestWeapon = null;
        float closestDistance = float.MaxValue;

        // Check all weapons directly instead of using Physics2D
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
            Debug.Log($"[Client {Object.InputAuthority}] Trying to pickup weapon at distance {closestDistance}");
            closestWeapon.TryPickup(Object.InputAuthority);
            // Start cooldown timer
            PickupCooldownTimer = TickTimer.CreateFromSeconds(Runner, pickupCooldown);
        }
        else
        {
            // No weapons nearby - try to drop current weapon if we have one
            if (playerData != null && playerData.HasWeapon())
            {
                Debug.Log($"[Client {Object.InputAuthority}] No weapons in range - dropping current weapon");
                RPC_RequestDropCurrentWeapon(Object.InputAuthority);
                // Start cooldown timer
                PickupCooldownTimer = TickTimer.CreateFromSeconds(Runner, pickupCooldown);
            }
            else
            {
                Debug.Log($"[Client {Object.InputAuthority}] No weapons in range and no weapon to drop");
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDropCurrentWeapon(PlayerRef playerRef, RpcInfo info = default)
    {
        // Verify the source matches the player trying to drop
        if (info.Source != playerRef && info.Source != PlayerRef.None)
        {
            Debug.LogWarning($"[Server] Player {info.Source} tried to drop for player {playerRef} - rejected");
            return;
        }

        Debug.Log($"[Server] RPC_RequestDropCurrentWeapon called for player {playerRef}");

        // Find the weapon this player is holding
        WeaponPickup heldWeapon = WeaponPickup.GetHeldWeapon(playerRef);
        if (heldWeapon != null)
        {
            Debug.Log($"[Server] Found held weapon: {heldWeapon.GetWeaponData().weaponID}");

            Vector3 dropPosition = transform.position;
            Vector2 dropVelocity = new Vector2(Random.Range(-2f, 2f), Random.Range(2f, 4f));

            // Clear aim controller reference first
            WeaponAimController aimController = GetComponent<WeaponAimController>();
            if (aimController != null)
            {
                aimController.SetCurrentWeapon(null);
            }

            // Drop the weapon
            heldWeapon.Drop(dropPosition, dropVelocity);

            // Clear player's weapon index
            if (playerData != null)
            {
                playerData.PickupWeapon(0); // Set weapon index to 0 (no weapon)
            }

            Debug.Log($"[Server] Player {playerRef} dropped their weapon");
        }
        else
        {
            Debug.LogWarning($"[Server] No held weapon found for player {playerRef}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupCheckRadius);
    }
}