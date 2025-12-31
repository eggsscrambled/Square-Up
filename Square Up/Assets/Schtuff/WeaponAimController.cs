using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    private WeaponPickup currentWeapon;
    private PlayerData playerData;
    private TickTimer fireRateTimer;
    private GameManager gameManager;

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
    }

    public override void Spawned()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    public override void FixedUpdateNetwork()
    {
        // Only process for players with input authority
        if (!Object.HasInputAuthority)
            return;

        if (playerData == null || !playerData.HasWeapon())
            return;

        // Find the weapon if we don't have a reference
        if (currentWeapon == null)
        {
            currentWeapon = FindObjectOfType<WeaponPickup>();
            // Make sure it's actually our weapon (picked up)
            if (currentWeapon != null && !currentWeapon.GetIsPickedUp())
            {
                currentWeapon = null;
            }
        }

        if (currentWeapon == null)
            return;

        // Get the networked input
        if (GetInput(out NetworkInputData input))
        {
            // Send aim direction to weapon via RPC
            if (input.aimDirection.magnitude > 0.1f)
            {
                RPC_UpdateWeaponAim(input.aimDirection);
            }

            // Handle firing
            if (input.fire && !playerData.Dead)
            {
                WeaponData weaponData = currentWeapon.GetWeaponData();

                if (weaponData != null)
                {
                    // Check if we can fire based on fire rate
                    if (fireRateTimer.ExpiredOrNotRunning(Runner))
                    {
                        RPC_Fire(input.aimDirection);
                        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / weaponData.fireRate);
                    }
                }
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_UpdateWeaponAim(Vector2 aimDirection)
    {
        if (currentWeapon != null)
        {
            currentWeapon.UpdateAimDirection(aimDirection);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Fire(Vector2 aimDirection)
    {
        if (currentWeapon == null || playerData.Dead)
            return;

        WeaponData weaponData = currentWeapon.GetWeaponData();
        if (weaponData == null || weaponData.bulletPrefab == null)
            return;

        if (aimDirection.magnitude < 0.1f)
            return;

        // Get the FireOrigin from the weapon
        Transform fireOrigin = currentWeapon.transform.Find("FireOrigin");
        if (fireOrigin == null)
        {
            Debug.LogError($"FireOrigin not found on weapon {weaponData.weaponID}!");
            return;
        }

        // Fire multiple bullets if bulletAmount > 1
        for (int i = 0; i < weaponData.bulletAmount; i++)
        {
            // Calculate spread
            Vector2 direction = CalculateSpreadDirection(aimDirection.normalized, weaponData.spreadAmount, weaponData.maxSpreadDegrees);

            // Spawn the projectile at the FireOrigin position
            NetworkObject projectile = Runner.Spawn(
                weaponData.bulletPrefab,
                fireOrigin.position,
                Quaternion.identity,
                Object.InputAuthority
            );

            // Initialize the projectile
            if (projectile.TryGetComponent<NetworkedProjectile>(out NetworkedProjectile proj))
            {
                proj.Initialize(weaponData, direction, Object.InputAuthority);
            }
        }

        Debug.Log($"Player {Object.InputAuthority} fired {weaponData.weaponID} from FireOrigin");
    }

    private Vector2 CalculateSpreadDirection(Vector2 baseDirection, float spreadAmount, float maxSpread)
    {
        if (spreadAmount <= 0) return baseDirection;

        float spreadAngle = Mathf.Min(spreadAmount, maxSpread);
        float randomAngle = Random.Range(-spreadAngle, spreadAngle);

        // Rotate the base direction by the random angle
        float angle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        angle += randomAngle;

        return new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        );
    }

    public void SetCurrentWeapon(WeaponPickup weapon)
    {
        currentWeapon = weapon;
    }

    public void ClearCurrentWeapon()
    {
        currentWeapon = null;
    }

    public WeaponPickup GetCurrentWeapon()
    {
        return currentWeapon;
    }
}