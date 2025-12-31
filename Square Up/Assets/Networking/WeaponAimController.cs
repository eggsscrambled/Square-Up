using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }
    [Networked] private NetworkBool wasFirePressedLastFrame { get; set; }

    [Header("Visuals")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    // Client-side fire rate tracking for predicted bullets
    private TickTimer _clientFireRateTimer;

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

            // Check fire rate timer - use client timer for clients, networked timer for host
            bool canFire = Object.HasStateAuthority
                ? fireRateTimer.ExpiredOrNotRunning(Runner)
                : _clientFireRateTimer.ExpiredOrNotRunning(Runner);

            if (data != null && canFire)
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

                    Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
                    Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
                    Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

                    if (Object.HasStateAuthority)
                    {
                        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);
                        if (_playerController != null) _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);
                    }
                    else
                    {
                        // Client sets their own fire rate timer for predicted bullets
                        _clientFireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);
                    }

                    // Fire() runs on both host and client
                    // Host spawns networked bullets, client spawns predicted visuals
                    Fire(baseAimDir, data, spawnPos);
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

        Debug.Log($"[Fire] HasStateAuthority: {Object.HasStateAuthority}, HasInputAuthority: {Object.HasInputAuthority}, IsServer: {Runner.IsServer}, IsClient: {Runner.IsClient}");

        for (int i = 0; i < data.bulletAmount; i++)
        {
            Vector2 spreadDir = CalculateSpreadDirection(direction, data.spreadAmount, data.maxSpreadDegrees);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, spreadDir);

            // Spawn predicted visual bullet ONLY for clients (not host/server)
            // Clients experience network delay and need instant visual feedback
            // Host already has state authority and sees networked bullets instantly
            bool isClient = !Object.HasStateAuthority && Object.HasInputAuthority;

            Debug.Log($"[Fire] Bullet {i}: isClient={isClient}, bulletVisualPrefab={(data.bulletVisualPrefab != null ? "exists" : "NULL")}");

            if (isClient && data.bulletVisualPrefab != null)
            {
                Debug.Log($"[Fire] SPAWNING PREDICTED BULLET at {spawnPos}");
                GameObject predicted = Instantiate(data.bulletVisualPrefab, spawnPos, spawnRotation);
                var predBullet = predicted.GetComponent<PredictedBullet>();
                if (predBullet != null)
                {
                    predBullet.Initialize(spreadDir * data.bulletSpeed, data.bulletLifetime);
                    Debug.Log($"[Fire] Predicted bullet initialized with velocity: {spreadDir * data.bulletSpeed}");
                }
                else
                {
                    Debug.LogError($"[Fire] PredictedBullet component NOT FOUND on visual prefab!");
                }
            }

            // Spawn authoritative networked bullet (happens on host/state authority)
            if (Object.HasStateAuthority)
            {
                Debug.Log($"[Fire] Spawning networked bullet (state authority)");
                Runner.Spawn(data.bulletPrefab, spawnPos, spawnRotation, Object.InputAuthority, (runner, obj) =>
                {
                    var proj = obj.GetComponent<NetworkedProjectile>();
                    proj.Initialize(data, spreadDir * data.bulletSpeed, Object.InputAuthority);
                });
            }
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
        // Reset client timer when switching weapons
        _clientFireRateTimer = TickTimer.None;
    }

    public void ClearCurrentWeapon()
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = null;
            CurrentWeaponId = default;
            wasFirePressedLastFrame = false; // Reset button state when clearing weapon
        }
        // Reset client timer when clearing weapon
        _clientFireRateTimer = TickTimer.None;
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