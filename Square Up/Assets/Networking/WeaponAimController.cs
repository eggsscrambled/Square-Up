using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }

    private WeaponPickup _currentWeapon;
    private PlayerData _playerData;
    private GameManager _gameManager;

    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
        _gameManager = FindObjectOfType<GameManager>();
    }

    public override void FixedUpdateNetwork()
    {
        // 1. Resolve the weapon reference using the Networked ID
        ResolveWeaponReference();

        if (_currentWeapon == null || _playerData == null || _playerData.Dead)
            return;

        // 2. Get Input (Standard Fusion Pattern)
        if (GetInput(out NetworkInputData input))
        {
            // Update Aim (State Authority handles the actual rotation)
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                // FIXED: Calculate aim direction from weapon's actual hold position
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;

                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            // 3. Handle Firing
            WeaponData data = _currentWeapon.GetWeaponData();

            if (data != null && fireRateTimer.ExpiredOrNotRunning(Runner))
            {
                // Check if weapon is automatic OR if this is a new button press
                bool shouldFire = data.isAutomatic
                    ? input.fire
                    : (input.fire && !input.wasFirePressedLastTick);

                if (shouldFire)
                {
                    // Reset Timer
                    fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

                    // FIXED: Pass the corrected aim direction
                    Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                    Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;

                    // Execute Fire Logic
                    FireWeapon(aimFromWeapon, data);
                }
            }
        }
    }

    private void FireWeapon(Vector2 aimDirection, WeaponData weaponData)
    {
        if (aimDirection.magnitude < 0.1f) return;

        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;

        // CHANGED: Spawn on both State Authority (server) AND Input Authority (client)
        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            for (int i = 0; i < weaponData.bulletAmount; i++)
            {
                Vector2 direction = CalculateSpreadDirection(aimDirection.normalized, weaponData.spreadAmount, weaponData.maxSpreadDegrees);
                Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, direction);

                NetworkObject projectile = Runner.Spawn(
                    weaponData.bulletPrefab,
                    spawnPos,
                    spawnRotation,
                    Object.InputAuthority
                );

                if (projectile.TryGetComponent<NetworkedProjectile>(out var proj))
                {
                    proj.Initialize(weaponData, direction, Object.InputAuthority);
                }
            }
        }
    }

    private void ResolveWeaponReference()
    {
        // If our local reference doesn't match our networked ID, find it once
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

    private Vector2 CalculateSpreadDirection(Vector2 baseDirection, float spreadAmount, float maxSpread)
    {
        if (spreadAmount <= 0) return baseDirection;

        float spreadAngle = Mathf.Min(spreadAmount, maxSpread);
        float randomAngle = Random.Range(-spreadAngle, spreadAngle);

        float angle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        angle += randomAngle;

        return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }

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
}