using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }

    private WeaponPickup _currentWeapon;
    private PlayerData _playerData;
    private PlayerController _playerController;

    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
        _playerController = GetComponent<PlayerController>();
    }

    public override void FixedUpdateNetwork()
    {
        ResolveWeaponReference();

        if (_currentWeapon == null || _playerData == null || _playerData.Dead) return;

        if (GetInput(out NetworkInputData input))
        {
            // 1. Update Aim Direction (Only State Authority updates the Networked property in WeaponPickup)
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            // 2. Firing Logic (Predicted)
            WeaponData data = _currentWeapon.GetWeaponData();
            if (data != null && fireRateTimer.ExpiredOrNotRunning(Runner))
            {
                // Check for fire input (Automatic vs Semi-Auto logic can be added here)
                if (input.fire)
                {
                    fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

                    Vector3 spawnPos = _currentWeapon.transform.position;
                    // Find FireOrigin if it exists
                    Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
                    if (fireOrigin != null) spawnPos = fireOrigin.position;

                    Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

                    Fire(baseAimDir, data, spawnPos);
                }
            }
        }
    }

    private void Fire(Vector2 direction, WeaponData data, Vector3 spawnPos)
    {
        for (int i = 0; i < data.bulletAmount; i++)
        {
            Vector2 spreadDir = CalculateSpread(direction, data.spreadAmount, data.maxSpreadDegrees);

            // This Spawn is PREDICTED. Client spawns it immediately, Server confirms.
            Runner.Spawn(data.bulletPrefab, spawnPos, Quaternion.identity, Object.InputAuthority, (runner, obj) =>
            {
                var projectile = obj.GetComponent<NetworkedProjectile>();
                projectile.Velocity = spreadDir * data.bulletSpeed;
                projectile.Owner = Object.InputAuthority;
            });
        }

        // Apply Recoil locally and on server
        if (_playerController != null && data.recoilForce > 0)
        {
            _playerController.ApplyRecoil(-direction * data.recoilForce);
        }
    }

    private Vector2 CalculateSpread(Vector2 baseDir, float spread, float maxDegrees)
    {
        if (spread <= 0) return baseDir;
        float randomAngle = Random.Range(-spread, spread);
        float angle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg + randomAngle;
        return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }

    // --- PICKUP LINKING METHODS (Fixes your Compiler Error) ---

    public void SetCurrentWeapon(WeaponPickup weapon)
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = weapon;
            CurrentWeaponId = weapon != null ? weapon.Object.Id : default;
        }
    }

    public void ClearCurrentWeapon()
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = null;
            CurrentWeaponId = default;
        }
    }

    // --- UTILITY ---

    private void ResolveWeaponReference()
    {
        if (CurrentWeaponId != default && (_currentWeapon == null || _currentWeapon.Object.Id != CurrentWeaponId))
        {
            if (Runner.TryFindObject(CurrentWeaponId, out NetworkObject weaponObj))
            {
                _currentWeapon = weaponObj.GetComponent<WeaponPickup>();
            }
        }
        else if (CurrentWeaponId == default)
        {
            _currentWeapon = null;
        }
    }
}