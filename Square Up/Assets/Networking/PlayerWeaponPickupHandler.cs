using Fusion;
using UnityEngine;

public class PlayerWeaponPickupHandler : NetworkBehaviour
{
    [SerializeField] private float pickupCheckRadius = 2f;
    [SerializeField] private float throwForce = 8f;

    private PlayerData playerData;

    // This networked timer prevents the "Instant Drop" by creating a small gap
    // between interactions, even during re-simulations.
    [Networked] private TickTimer InteractionCooldown { get; set; }

    private void Awake() => playerData = GetComponent<PlayerData>();

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input))
        {
            // Check if the Pickup button is held AND the cooldown has finished
            if (input.buttons.IsSet(MyButtons.Pickup) && InteractionCooldown.ExpiredOrNotRunning(Runner))
            {
                if (playerData != null && playerData.Dead) return;

                // 1. Close the gate for 0.25 seconds so this can't fire again immediately
                InteractionCooldown = TickTimer.CreateFromSeconds(Runner, 0.25f);

                // 2. Calculate throw velocity
                Vector2 aimDir = input.aimDirection.magnitude > 0.1f ? input.aimDirection.normalized : (Vector2)transform.up;
                Vector2 throwVelocity = (aimDir + Vector2.up * 0.4f).normalized * throwForce;

                // 3. Send the request to the server
                RPC_RequestInteraction(throwVelocity);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteraction(Vector2 throwVelocity)
    {
        WeaponPickup closestWeapon = null;
        float closestDistance = float.MaxValue;

        // Find the nearest weapon on the server
        WeaponPickup[] allWeapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        foreach (var weapon in allWeapons)
        {
            if (weapon == null || weapon.GetIsPickedUp()) continue;
            float dist = Vector3.Distance(transform.position, weapon.transform.position);
            if (dist <= pickupCheckRadius && dist < closestDistance)
            {
                closestDistance = dist;
                closestWeapon = weapon;
            }
        }

        // The Server makes the final call
        if (closestWeapon != null)
        {
            // SWAP logic
            HandleServerDrop(throwVelocity);
            closestWeapon.ServerExecutePickup(Object.InputAuthority);
        }
        else
        {
            // PURE DROP logic
            HandleServerDrop(throwVelocity);
        }
    }

    private void HandleServerDrop(Vector2 throwVelocity)
    {
        WeaponAimController aim = GetComponent<WeaponAimController>();
        if (aim != null && aim.CurrentWeapon != null)
        {
            WeaponPickup toDrop = aim.CurrentWeapon;
            aim.SetCurrentWeapon(null);

            // This calls the 'Drop' method in WeaponPickup
            toDrop.Drop(transform.position, throwVelocity);

            if (playerData != null) playerData.PickupWeapon(0);
        }
    }
}