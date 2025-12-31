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
        // This is much faster than FindObjectOfType and works for all clients
        ResolveWeaponReference();

        if (_currentWeapon == null || _playerData == null || _playerData.Dead)
            return;

        // 2. Get Input (Standard Fusion Pattern)
        if (GetInput(out NetworkInputData input))
        {
            // Update Aim (State Authority handles the actual rotation)
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                _currentWeapon.UpdateAimDirection(input.aimDirection);
            }

            // 3. Handle Firing (Logic runs on both Client and Server for prediction)
            if (input.fire)
            {
                WeaponData data = _currentWeapon.GetWeaponData();

                if (data != null && fireRateTimer.ExpiredOrNotRunning(Runner))
                {
                    // Reset Timer
                    fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

                    // Execute Fire Logic
                    FireWeapon(input.aimDirection, data);
                }
            }
        }
    }

    private void FireWeapon(Vector2 aimDirection, WeaponData weaponData)
    {
        if (aimDirection.magnitude < 0.1f) return;

        // FIX 1: Ensure we are finding the FireOrigin on the weapon instance the Server sees
        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");

        // Fallback to weapon position if FireOrigin is missing
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;

        // Only the State Authority (Server/Host) should perform the actual Spawn
        if (Object.HasStateAuthority)
        {
            for (int i = 0; i < weaponData.bulletAmount; i++)
            {
                Vector2 direction = CalculateSpreadDirection(aimDirection.normalized, weaponData.spreadAmount, weaponData.maxSpreadDegrees);

                // FIX 2: Explicitly pass the rotation so it doesn't default to Zero
                Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, direction);

                NetworkObject projectile = Runner.Spawn(
                    weaponData.bulletPrefab,
                    spawnPos,
                    spawnRotation,
                    Object.InputAuthority // This ensures the client still "owns" the credit for the shot
                );

                // FIX 3: Ensure the Projectile component is initialized on the Server
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
            CurrentWeaponId = default; // Replaces .None
        }
    }
}