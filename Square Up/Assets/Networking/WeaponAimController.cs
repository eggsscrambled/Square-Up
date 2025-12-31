using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }
    [Networked] private NetworkBool wasFirePressedLastFrame { get; set; }

    [Header("Visuals")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    private WeaponPickup _currentWeapon;
    private PlayerData _playerData;
    private PlayerController _playerController;

    public WeaponPickup CurrentWeapon => _currentWeapon;

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
            // Update Aiming
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            // Check for Fire button via NetworkButtons
            bool firePressed = input.buttons.IsSet(MyButtons.Fire);

            WeaponData data = _currentWeapon.GetWeaponData();
            if (data != null && fireRateTimer.ExpiredOrNotRunning(Runner))
            {
                bool shouldFire = false;

                if (data.isAutomatic)
                {
                    // Automatic: fire while button is held
                    shouldFire = firePressed;
                }
                else
                {
                    // Semi-automatic: fire only on button press (not held)
                    shouldFire = firePressed && !wasFirePressedLastFrame;
                }

                if (shouldFire)
                {
                    if (Runner.IsForward) TriggerLocalMuzzleFlash();

                    if (Object.HasStateAuthority)
                    {
                        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);
                        if (_playerController != null) _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);

                        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
                        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
                        Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

                        Fire(baseAimDir, data, spawnPos);
                    }
                }
            }

            // Track button state for next frame (only on state authority)
            if (Object.HasStateAuthority)
            {
                wasFirePressedLastFrame = firePressed;
            }
        }
    }

    private void TriggerLocalMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || _currentWeapon == null) return;
        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
        GameObject flash = Instantiate(muzzleFlashPrefab, spawnPos, _currentWeapon.transform.rotation);
        Destroy(flash, 1.0f);
    }

    private void Fire(Vector2 direction, WeaponData data, Vector3 spawnPos)
    {
        // Use tick-based seed for deterministic randomness across client and server
        int seed = Runner.Tick.Raw;
        Random.InitState(seed);

        for (int i = 0; i < data.bulletAmount; i++)
        {
            Vector2 spreadDir = CalculateSpreadDirection(direction, data.spreadAmount, data.maxSpreadDegrees);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, spreadDir);

            // Spawn predicted visual bullet ONLY for remote clients (not host)
            // Remote clients need instant feedback since they don't have state authority
            if (!Object.HasStateAuthority && Object.HasInputAuthority && data.bulletVisualPrefab != null)
            {
                GameObject predicted = Instantiate(data.bulletVisualPrefab, spawnPos, spawnRotation);
                var predBullet = predicted.GetComponent<PredictedBullet>();
                if (predBullet != null)
                {
                    predBullet.Initialize(spreadDir * data.bulletSpeed, data.bulletLifetime);
                }
            }

            // Spawn authoritative networked bullet (happens on host/state authority)
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

    public void SetCurrentWeapon(WeaponPickup weapon)
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = weapon;
            CurrentWeaponId = weapon != null ? weapon.Object.Id : default;
            wasFirePressedLastFrame = false; // Reset button state when switching weapons
        }
    }

    public void ClearCurrentWeapon()
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = null;
            CurrentWeaponId = default;
            wasFirePressedLastFrame = false; // Reset button state when clearing weapon
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