using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }

    [Header("Visuals")]
    [SerializeField] private GameObject muzzleFlashPrefab;

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
            // 1. Aiming (Only the state authority updates the networked weapon position)
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            // 2. Firing Logic
            WeaponData data = _currentWeapon.GetWeaponData();
            if (data != null && fireRateTimer.ExpiredOrNotRunning(Runner))
            {
                if (input.fire)
                {
                    // Instant Local Visual Feedback (Runs on Client immediately)
                    if (Runner.IsForward)
                    {
                        TriggerLocalMuzzleFlash();
                    }

                    // Server-Authoritative Bullet Spawn
                    if (Object.HasStateAuthority)
                    {
                        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

                        // Recoil applied instantly on server
                        if (_playerController != null) _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);

                        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
                        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
                        Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

                        Fire(baseAimDir, data, spawnPos);
                    }
                }
            }
        }
    }

    private void TriggerLocalMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || _currentWeapon == null) return;

        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;

        // Instant visual instantiation on the client's side
        Instantiate(muzzleFlashPrefab, spawnPos, _currentWeapon.transform.rotation);
    }

    private void Fire(Vector2 direction, WeaponData data, Vector3 spawnPos)
    {
        for (int i = 0; i < data.bulletAmount; i++)
        {
            Vector2 spreadDir = CalculateSpreadDirection(direction, data.spreadAmount, data.maxSpreadDegrees);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, spreadDir);

            // Server-only spawn to maintain authority and prevent desync
            Runner.Spawn(data.bulletPrefab, spawnPos, spawnRotation, Object.InputAuthority, (runner, obj) =>
            {
                var proj = obj.GetComponent<NetworkedProjectile>();
                proj.Initialize(data, spreadDir * data.bulletSpeed, Object.InputAuthority);
            });
        }
    }

    private Vector2 CalculateSpreadDirection(Vector2 baseDir, float spread, float maxDegrees)
    {
        if (spread <= 0) return baseDir;
        float randomAngle = Random.Range(-spread, spread);
        float angle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg + randomAngle;
        return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }

    // --- PICKUP LINKING METHODS (Fixed Compiler Errors) ---

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

    private void ResolveWeaponReference()
    {
        if (CurrentWeaponId != default && (_currentWeapon == null || _currentWeapon.Object.Id != CurrentWeaponId))
        {
            if (Runner.TryFindObject(CurrentWeaponId, out NetworkObject weaponObj))
                _currentWeapon = weaponObj.GetComponent<WeaponPickup>();
        }
        else if (CurrentWeaponId == default) _currentWeapon = null;
    }
}