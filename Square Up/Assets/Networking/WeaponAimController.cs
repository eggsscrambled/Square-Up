using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }

    [Header("Networked State (Debug)")]
    [Networked] public TickTimer fireRateTimer { get; set; }
    [Networked] public TickTimer reloadTimer { get; set; }
    [Networked] public int RemainingAmmo { get; set; }
    [Networked] private NetworkBool wasFirePressedLastFrame { get; set; }
    [Networked] private int nextBulletId { get; set; }
    [Networked] private int muzzleFlashCounter { get; set; }
    [Networked] private NetworkBool hasPlayedReloadStartSound { get; set; }
    [Networked] private NetworkBool hasPlayedReloadMidSound { get; set; }

    [Header("Visuals")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    private int _lastMuzzleFlashCounter = -1;
    private WeaponPickup _currentWeapon;
    private PlayerData _playerData;
    private PlayerController _playerController;

    public WeaponPickup CurrentWeapon => _currentWeapon;

    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
        _playerController = GetComponent<PlayerController>();
        _lastMuzzleFlashCounter = muzzleFlashCounter;
    }

    public override void FixedUpdateNetwork()
    {
        ResolveWeaponReference();
        if (_currentWeapon == null || _playerData == null || _playerData.Dead) return;

        WeaponData data = _currentWeapon.GetWeaponData();
        if (data == null) return;

        // 1. CHECK FOR RELOAD COMPLETION (State Authority Only)
        if (Object.HasStateAuthority && reloadTimer.Expired(Runner))
        {
            RemainingAmmo = data.maxAmmo;
            // Sync ammo back to weapon
            _currentWeapon.SetCurrentAmmo(RemainingAmmo);
            reloadTimer = TickTimer.None;
            hasPlayedReloadStartSound = false;
            hasPlayedReloadMidSound = false;

            // Play reload end sound only in forward simulation
            if (_currentWeapon != null && Runner.IsForward)
            {
                _currentWeapon.PlayReloadEndSound();
            }

            Debug.Log($"<color=green>SERVER: Reload Complete. Ammo: {RemainingAmmo}</color>");
        }

        // Check for mid-reload sound (at 50% progress) - only in forward simulation
        if (Object.HasStateAuthority && !reloadTimer.ExpiredOrNotRunning(Runner) && !hasPlayedReloadMidSound && Runner.IsForward)
        {
            float remainingTime = reloadTimer.RemainingTime(Runner) ?? 0f;
            float elapsedTime = data.reloadTimeSeconds - remainingTime;
            float progress = elapsedTime / data.reloadTimeSeconds;

            if (progress >= 0.5f)
            {
                hasPlayedReloadMidSound = true;
                if (_currentWeapon != null)
                {
                    _currentWeapon.PlayReloadMidSound();
                }
            }
        }

        if (GetInput(out NetworkInputData input))
        {
            // 2. Handle Aiming (State Authority)
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            // 3. RELOAD INPUT GATING
            bool isReloading = !reloadTimer.ExpiredOrNotRunning(Runner);

            // Manual Reload
            if (input.buttons.IsSet(MyButtons.Reload) && !isReloading && RemainingAmmo < data.maxAmmo)
            {
                StartReload(data);
                isReloading = true;
            }

            // 4. FIRING LOGIC
            bool firePressed = input.buttons.IsSet(MyButtons.Fire);

            if (firePressed && !isReloading && fireRateTimer.ExpiredOrNotRunning(Runner))
            {
                if (RemainingAmmo > 0)
                {
                    bool canShootThisFrame = data.isAutomatic || !wasFirePressedLastFrame;
                    if (canShootThisFrame)
                    {
                        ExecuteShoot(input, data);
                    }
                }
                else if (!isReloading)
                {
                    StartReload(data); // Auto-reload trigger
                }
            }

            wasFirePressedLastFrame = firePressed;
        }
    }

    private void StartReload(WeaponData data)
    {
        // Safety check: Don't restart if already active
        if (!reloadTimer.ExpiredOrNotRunning(Runner)) return;

        reloadTimer = TickTimer.CreateFromSeconds(Runner, data.reloadTimeSeconds);
        hasPlayedReloadStartSound = false;
        hasPlayedReloadMidSound = false;

        // Play reload start sound ONLY in forward simulation
        if (_currentWeapon != null && Runner.IsForward)
        {
            hasPlayedReloadStartSound = true;
            _currentWeapon.PlayReloadStartSound();
        }

        Debug.Log($"<color=yellow>Reloading started for {data.reloadTimeSeconds}s...</color>");
    }

    private void ExecuteShoot(NetworkInputData input, WeaponData data)
    {
        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
        Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

        int bulletIdStart;

        if (Object.HasStateAuthority)
        {
            bulletIdStart = nextBulletId;
            nextBulletId += data.bulletAmount;
            RemainingAmmo--;
            // Sync ammo to weapon
            _currentWeapon.SetCurrentAmmo(RemainingAmmo);

            // FIX: Only increment counter if we're NOT the input authority
            // (so other clients can see our muzzle flash)
            if (!Object.HasInputAuthority)
            {
                muzzleFlashCounter++;
            }

            if (_playerController != null)
                _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);

            // State authority always triggers muzzle flash locally
            TriggerMuzzleFlash();
        }
        else
        {
            // Predicted client-side
            bulletIdStart = nextBulletId;
            nextBulletId += data.bulletAmount;
            TriggerMuzzleFlash();
        }

        Fire(baseAimDir, data, spawnPos, input.inputTick, bulletIdStart);
    }

    public override void Render()
    {
        // Check for muzzle flash updates from other players (anyone we don't have input authority over)
        if (!Object.HasInputAuthority && muzzleFlashCounter != _lastMuzzleFlashCounter)
        {
            _lastMuzzleFlashCounter = muzzleFlashCounter;
            TriggerMuzzleFlash();
        }
    }

    private void Fire(Vector2 direction, WeaponData data, Vector3 spawnPos, int seed, int startBulletId)
    {
        for (int i = 0; i < data.bulletAmount; i++)
        {
            int bulletId = startBulletId + i;
            Random.InitState(seed + i);

            Vector2 spreadDir = CalculateSpreadDirection(direction, data.spreadAmount, data.maxSpreadDegrees);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, spreadDir);

            // FIX: Only spawn predicted bullets during FORWARD simulation (not resimulation)
            if (Object.HasInputAuthority && !Object.HasStateAuthority && data.bulletVisualPrefab != null && Runner.IsForward)
            {
                GameObject predicted = Instantiate(data.bulletVisualPrefab, spawnPos, spawnRotation);
                var predBullet = predicted.GetComponent<PredictedBullet>();
                if (predBullet != null)
                    predBullet.Initialize(spreadDir * data.bulletSpeed, data.bulletLifetime, bulletId);
            }

            if (Object.HasStateAuthority)
            {
                Runner.Spawn(data.bulletPrefab, spawnPos, spawnRotation, Object.InputAuthority, (runner, obj) =>
                {
                    var proj = obj.GetComponent<NetworkedProjectile>();
                    proj.Initialize(data, spreadDir * data.bulletSpeed, Object.InputAuthority, bulletId);
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

    private void TriggerMuzzleFlash()
    {
        if (_currentWeapon == null) return;
        WeaponData data = _currentWeapon.GetWeaponData();
        GameObject flashPrefab = data?.muzzleFlashPrefab ?? muzzleFlashPrefab;
        if (flashPrefab == null) return;

        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
        GameObject flash = Instantiate(flashPrefab, spawnPos, _currentWeapon.transform.rotation);
        Destroy(flash, 1.0f);
    }

    public void SetCurrentWeapon(WeaponPickup weapon)
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = weapon;
            CurrentWeaponId = weapon != null ? weapon.Object.Id : default;
            // Load ammo from weapon instead of resetting to max
            RemainingAmmo = weapon != null ? weapon.GetCurrentAmmo() : 0;
            wasFirePressedLastFrame = false;
        }
        fireRateTimer = TickTimer.None;
        reloadTimer = TickTimer.None;
    }

    public void ClearCurrentWeapon()
    {
        if (Object.HasStateAuthority)
        {
            // Save current ammo to weapon before clearing
            if (_currentWeapon != null)
            {
                _currentWeapon.SetCurrentAmmo(RemainingAmmo);
            }

            _currentWeapon = null;
            CurrentWeaponId = default;
            RemainingAmmo = 0;
        }
        fireRateTimer = TickTimer.None;
        reloadTimer = TickTimer.None;
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