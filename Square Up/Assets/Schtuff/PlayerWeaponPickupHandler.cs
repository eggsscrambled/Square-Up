using Fusion;
using UnityEngine;

public class PlayerWeaponPickupHandler : NetworkBehaviour
{
    [SerializeField] private float pickupCheckRadius = 2f;
    [SerializeField] private LayerMask weaponLayer;
    private PlayerData playerData;

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority)
            return;

        if (playerData != null && playerData.Dead)
            return;

        if (GetInput(out NetworkInputData input))
        {
            if (input.pickup)
            {
                Debug.Log($"[Client] E KEY INPUT RECEIVED for player {Object.InputAuthority}");
                TryPickupNearbyWeapon();
            }
        }
    }

    private void TryPickupNearbyWeapon()
    {
        Debug.Log($"[Client] Attempting pickup for player {Object.InputAuthority}");

        Collider2D[] nearbyWeapons = Physics2D.OverlapCircleAll(transform.position, pickupCheckRadius, weaponLayer);
        Debug.Log($"[Client] Found {nearbyWeapons.Length} nearby weapon colliders");

        WeaponPickup closestWeapon = null;
        float closestDistance = float.MaxValue;

        // Find the closest weapon that ISN'T picked up
        foreach (var col in nearbyWeapons)
        {
            WeaponPickup weapon = col.GetComponent<WeaponPickup>();

            // FIXED: Remove the IsPlayerNearby check - just check if it's not picked up
            if (weapon != null && !weapon.GetIsPickedUp())
            {
                float distance = Vector3.Distance(transform.position, weapon.transform.position);
                Debug.Log($"[Client] Found weapon at distance {distance}, picked up: {weapon.GetIsPickedUp()}");

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestWeapon = weapon;
                }
            }
        }

        // Try to pickup the closest weapon
        if (closestWeapon != null)
        {
            Debug.Log($"[Client] Calling TryPickup on closest weapon");
            closestWeapon.TryPickup(Object.InputAuthority);
        }
        else
        {
            Debug.Log($"[Client] No valid weapons found to pickup");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupCheckRadius);
    }
}