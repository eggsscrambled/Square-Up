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
            // DEBUG: Check if input reaches here
            if (input.pickup)
            {
                Debug.Log("E KEY INPUT RECEIVED in FixedUpdateNetwork()");
                TryPickupNearbyWeapon();
            }
        }
    }

    private NetworkBool wasPickupPressed; // Add this field at the top

    private void TryPickupNearbyWeapon()
    {
        Debug.Log($"[Pickup] Attempting pickup for player {Object.InputAuthority}");

        Collider2D[] nearbyWeapons = Physics2D.OverlapCircleAll(transform.position, pickupCheckRadius, weaponLayer);

        Debug.Log($"[Pickup] Found {nearbyWeapons.Length} nearby colliders");

        WeaponPickup closestWeapon = null;
        float closestDistance = float.MaxValue;

        // Find the closest weapon
        foreach (var col in nearbyWeapons)
        {
            WeaponPickup weapon = col.GetComponent<WeaponPickup>();
            if (weapon != null && weapon.IsPlayerNearby(Object.InputAuthority))
            {
                float distance = Vector3.Distance(transform.position, weapon.transform.position);
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
            closestWeapon.TryPickup(Object.InputAuthority);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupCheckRadius);
    }
}