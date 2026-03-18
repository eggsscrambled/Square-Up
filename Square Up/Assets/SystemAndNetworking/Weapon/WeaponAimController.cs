using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    // =========================================================
    //  NETWORKED STATE
    // =========================================================

    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] public TickTimer fireRateTimer { get; set; }
    [Networked] public TickTimer reloadTimer { get; set; }
    [Networked] public int RemainingAmmo { get; set; }
    [Networked] private NetworkBool wasFirePressedLastFrame { get; set; }
    [Networked] private int nextBulletId { get; set; }
    [Networked] private NetworkBool hasPlayedMidSound { get; set; }

    // =========================================================
    //  PRIVATE FIELDS
    // =========================================================

    private WeaponPickup _currentWeapon;
    private PlayerData _playerData;
    private PlayerController _playerController;

    // =========================================================
    //  PUBLIC ACCESSORS
    // =========================================================

    public WeaponPickup CurrentWeapon => _currentWeapon;

    // =========================================================
    //  UNITY / FUSION LIFECYCLE
    // =========================================================

    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
        _playerController = GetComponent<PlayerController>();
    }

    public override void FixedUpdateNetwork()
    {
        ResolveWeaponReference();

        if (_playerData == null || _playerData.Dead) return;
        if (_currentWeapon == null) return;
        if (!GetInput(out NetworkInputData input)) return;

        WeaponData data = _currentWeapon.GetWeaponData();
        if (data == null) return;

        bool isHealingCommitment = _playerData.IsHealing;

        HandleHealing(input);
        HandleReload(data, isHealingCommitment);
        HandleAiming(input);
        HandleShooting(input, data, isHealingCommitment);

        wasFirePressedLastFrame = input.buttons.IsSet(MyButtons.Fire);
    }

    // =========================================================
    //  PUBLIC API — WEAPON MANAGEMENT
    // =========================================================

    public void SetCurrentWeapon(WeaponPickup weapon)
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = weapon;
            CurrentWeaponId = weapon != null ? weapon.Object.Id : default;
            RemainingAmmo = weapon != null ? weapon.GetCurrentAmmo() : 0;
            wasFirePressedLastFrame = false;
        }

        fireRateTimer = TickTimer.None;
        reloadTimer = TickTimer.None;
        hasPlayedMidSound = false;
    }

    public void ClearCurrentWeapon()
    {
        if (Object.HasStateAuthority)
        {
            if (_currentWeapon != null) _currentWeapon.SetCurrentAmmo(RemainingAmmo);
            _currentWeapon = null;
            CurrentWeaponId = default;
            RemainingAmmo = 0;
        }

        fireRateTimer = TickTimer.None;
        reloadTimer = TickTimer.None;
        hasPlayedMidSound = false;
    }

    // =========================================================
    //  PRIVATE — INPUT HANDLERS
    // =========================================================

    private void HandleHealing(NetworkInputData input)
    {
        bool healPressed = input.buttons.IsSet(MyButtons.Heal);
        bool needsHealing = _playerData.Health < 100f;

        if (healPressed && needsHealing)
        {
            if (!_playerData.IsHealing)
                _playerData.HealWindupTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            _playerData.IsHealing = true;
        }
        else
        {
            _playerData.IsHealing = false;
            _playerData.HealWindupTimer = TickTimer.None;
        }
    }

    private void HandleReload(WeaponData data, bool isHealingCommitment)
    {
        if (isHealingCommitment)
        {
            reloadTimer = TickTimer.None;
            hasPlayedMidSound = false;
            return;
        }

        if (!reloadTimer.IsRunning) return;

        // Mid-reload sound (fires once at the halfway point).
        if (Object.HasStateAuthority && !hasPlayedMidSound)
        {
            float? remaining = reloadTimer.RemainingTime(Runner);
            if (remaining.HasValue && remaining.Value <= (data.reloadTimeSeconds / 2f))
            {
                _currentWeapon.PlayReloadMidSound();
                hasPlayedMidSound = true;
            }
        }

        // Reload complete.
        if (Object.HasStateAuthority && reloadTimer.Expired(Runner))
        {
            RemainingAmmo = data.maxAmmo;
            _currentWeapon.SetCurrentAmmo(RemainingAmmo);
            reloadTimer = TickTimer.None;
            hasPlayedMidSound = false;

            if (Runner.IsForward) _currentWeapon.PlayReloadEndSound();
        }
    }

    private void HandleAiming(NetworkInputData input)
    {
        if (!Object.HasStateAuthority) return;
        if (input.aimDirection.magnitude <= 0.1f) return;

        Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)_currentWeapon.GetWeaponHoldPosition()).normalized;
        _currentWeapon.UpdateAimDirection(aimFromWeapon);
    }

    private void HandleShooting(NetworkInputData input, WeaponData data, bool isHealingCommitment)
    {
        bool isReloading = !reloadTimer.ExpiredOrNotRunning(Runner);
        bool firePressed = input.buttons.IsSet(MyButtons.Fire);
        bool canFire = firePressed && !isReloading && !isHealingCommitment
                           && fireRateTimer.ExpiredOrNotRunning(Runner);

        if (canFire)
        {
            if (RemainingAmmo > 0)
            {
                if (data.isAutomatic || !wasFirePressedLastFrame)
                    ExecuteShoot(input, data);
            }
            else if (Object.HasStateAuthority)
            {
                StartReload(data);
            }
        }

        // Manual reload.
        bool manualReload = input.buttons.IsSet(MyButtons.Reload)
                            && !isHealingCommitment
                            && !isReloading
                            && RemainingAmmo < data.maxAmmo;

        if (manualReload && Object.HasStateAuthority)
            StartReload(data);
    }

    // =========================================================
    //  PRIVATE — SHOOTING
    // =========================================================

    private void StartReload(WeaponData data)
    {
        if (_playerData.IsHealing) return;
        if (!reloadTimer.ExpiredOrNotRunning(Runner)) return;

        reloadTimer = TickTimer.CreateFromSeconds(Runner, data.reloadTimeSeconds);
        hasPlayedMidSound = false;

        if (Runner.IsForward) _currentWeapon.PlayReloadStartSound();
    }

    private void ExecuteShoot(NetworkInputData input, WeaponData data)
    {
        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

        if (Object.HasStateAuthority)
        {
            RemainingAmmo--;
            _currentWeapon.SetCurrentAmmo(RemainingAmmo);

            int weaponIdx = GameManager.Instance.GetWeaponIndex(data);
            Transform origin = _currentWeapon.transform.Find("FireOrigin") ?? _currentWeapon.transform;
            GlobalFXManager.Instance.RequestMuzzleFlash(origin.position, origin.rotation, weaponIdx);

            if (_playerController != null)
                _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);
        }

        // Local camera shake — input authority only.
        if (Object.HasInputAuthority && CameraEffects.Instance != null)
            CameraEffects.Instance.AddScreenShake(data.cameraShakeIntensity, data.cameraShakeDuration);

        // Client-side predictive muzzle flash to eliminate perceived latency.
        if (Object.HasInputAuthority && !Object.HasStateAuthority && GlobalFXManager.Instance != null)
        {
            Transform origin = _currentWeapon.transform.Find("FireOrigin") ?? _currentWeapon.transform;
            int weaponIdx = GameManager.Instance != null ? GameManager.Instance.GetWeaponIndex(data) : 0;
            GlobalFXManager.Instance.PlayMuzzleFlashLocal(origin.position, origin.rotation, weaponIdx);
        }

        SpawnBullets(input, data);
    }

    private void SpawnBullets(NetworkInputData input, WeaponData data)
    {
        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
        Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

        if (DynamicMusicManager.Instance != null)
            DynamicMusicManager.Instance.RegisterShot();

        int seed = input.inputTick * 7919 + nextBulletId;
        System.Random rng = new System.Random(seed);

        for (int i = 0; i < data.bulletAmount; i++)
        {
            int bulletId = nextBulletId + i;
            Vector2 spreadDir = CalculateSpreadDirection(baseAimDir, data.spreadAmount, rng);
            Quaternion spawnRot = Quaternion.LookRotation(Vector3.forward, spreadDir);

            Vector2 capturedSpreadDir = spreadDir;
            int capturedBulletId = bulletId;

            if (Object.HasInputAuthority && Runner.IsForward && !Object.HasStateAuthority)
            {
                GameObject pred = Instantiate(data.bulletVisualPrefab, spawnPos, spawnRot);
                pred.GetComponent<PredictedBullet>()?.Initialize(
                    capturedSpreadDir * data.bulletSpeed, data.bulletLifetime, capturedBulletId);
            }

            if (Object.HasStateAuthority)
            {
                Runner.Spawn(data.bulletPrefab, spawnPos, spawnRot, Object.InputAuthority,
                    (runner, obj) => obj.GetComponent<NetworkedProjectile>().Initialize(
                        data, capturedSpreadDir * data.bulletSpeed, Object.InputAuthority, capturedBulletId));
            }
        }

        if (Object.HasStateAuthority) nextBulletId += data.bulletAmount;
    }

    // =========================================================
    //  PRIVATE — UTILITIES
    // =========================================================

    private void ResolveWeaponReference()
    {
        if (CurrentWeaponId != default &&
            (_currentWeapon == null || _currentWeapon.Object.Id != CurrentWeaponId))
        {
            if (Runner.TryFindObject(CurrentWeaponId, out NetworkObject weaponObj))
                _currentWeapon = weaponObj.GetComponent<WeaponPickup>();
        }
        else if (CurrentWeaponId == default)
        {
            _currentWeapon = null;
        }
    }

    private Vector2 CalculateSpreadDirection(Vector2 baseDir, float spread, System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double gaussian = System.Math.Sqrt(-2.0 * System.Math.Log(u1))
                        * System.Math.Sin(2.0 * System.Math.PI * u2);

        float randomAngle = (float)(gaussian * spread * 0.5f);
        float angle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg + randomAngle;
        return new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad));
    }
}