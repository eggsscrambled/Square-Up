using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] public TickTimer fireRateTimer { get; set; }
    [Networked] public TickTimer reloadTimer { get; set; }
    [Networked] public int RemainingAmmo { get; set; }
    [Networked] private NetworkBool wasFirePressedLastFrame { get; set; }
    [Networked] private int nextBulletId { get; set; }

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

        // 1. Basic Validation
        if (_playerData == null || _playerData.Dead) return;

        if (GetInput(out NetworkInputData input))
        {
            // --- HEALING & WIND-UP LOGIC ---
            bool healButtonPressed = input.buttons.IsSet(MyButtons.Heal);
            bool needsHealing = _playerData.Health < 100f; // Assuming 100 is Max Health

            // Check if we should be in the healing state
            if (healButtonPressed && needsHealing)
            {
                // If this is the very first frame we pressed Q, start the wind-up timer
                if (!_playerData.IsHealing)
                {
                    _playerData.HealWindupTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                }
                _playerData.IsHealing = true;
            }
            else
            {
                // If button released or health full, reset everything
                _playerData.IsHealing = false;
                _playerData.HealWindupTimer = TickTimer.None;
            }

            // --- WEAPON BLOCKING ---
            // If we are healing (even in wind-up), we cannot shoot or reload.
            bool isHealingCommitment = _playerData.IsHealing;

            if (_currentWeapon == null) return;
            WeaponData data = _currentWeapon.GetWeaponData();
            if (data == null) return;

            // --- RELOAD LOGIC ---
            if (isHealingCommitment)
            {
                // Cancel active reload if we start healing
                reloadTimer = TickTimer.None;
            }
            else if (Object.HasStateAuthority && reloadTimer.Expired(Runner))
            {
                RemainingAmmo = data.maxAmmo;
                _currentWeapon.SetCurrentAmmo(RemainingAmmo);
                reloadTimer = TickTimer.None;
                if (Runner.IsForward) _currentWeapon.PlayReloadEndSound();
            }

            // --- AIMING LOGIC ---
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)_currentWeapon.GetWeaponHoldPosition()).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            // --- SHOOTING LOGIC ---
            bool isReloading = !reloadTimer.ExpiredOrNotRunning(Runner);
            bool firePressed = input.buttons.IsSet(MyButtons.Fire);

            // Conditions to shoot: Fire held, NOT reloading, NOT healing, and Rate of Fire timer is ready
            if (firePressed && !isReloading && !isHealingCommitment && fireRateTimer.ExpiredOrNotRunning(Runner))
            {
                if (RemainingAmmo > 0)
                {
                    // Handle Automatic vs Semi-Auto
                    if (data.isAutomatic || !wasFirePressedLastFrame)
                    {
                        ExecuteShoot(input, data);
                    }
                }
                else if (Object.HasStateAuthority)
                {
                    // Auto-reload if empty
                    StartReload(data);
                }
            }

            // Manual Reload Input (Blocked by healing)
            if (input.buttons.IsSet(MyButtons.Reload) && !isHealingCommitment && !isReloading && RemainingAmmo < data.maxAmmo)
            {
                if (Object.HasStateAuthority) StartReload(data);
            }

            wasFirePressedLastFrame = firePressed;
        }
    }

    private void StartReload(WeaponData data)
    {
        if (_playerData.IsHealing) return; // Cannot start reload while healing
        if (!reloadTimer.ExpiredOrNotRunning(Runner)) return;
        reloadTimer = TickTimer.CreateFromSeconds(Runner, data.reloadTimeSeconds);
        if (Runner.IsForward) _currentWeapon.PlayReloadStartSound();
    }

    private void ExecuteShoot(NetworkInputData input, WeaponData data)
    {
        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);

        if (Object.HasStateAuthority)
        {
            RemainingAmmo--;
            _currentWeapon.SetCurrentAmmo(RemainingAmmo);

            // Pass the weapon index to GlobalFXManager for the muzzle flash
            int weaponIdx = GameManager.Instance.GetWeaponIndex(data);
            Transform origin = _currentWeapon.transform.Find("FireOrigin") ?? _currentWeapon.transform;
            GlobalFXManager.Instance.RequestMuzzleFlash(origin.position, origin.rotation, weaponIdx);

            if (_playerController != null)
                _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);
        }

        SpawnBullets(input, data);
    }

    private void SpawnBullets(NetworkInputData input, WeaponData data)
    {
        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
        Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

        for (int i = 0; i < data.bulletAmount; i++)
        {
            int bulletId = nextBulletId + i;
            Random.InitState(input.inputTick + i);
            Vector2 spreadDir = CalculateSpreadDirection(baseAimDir, data.spreadAmount, data.maxSpreadDegrees);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, spreadDir);

            if (Object.HasInputAuthority && Runner.IsForward && !Object.HasStateAuthority)
            {
                GameObject pred = Instantiate(data.bulletVisualPrefab, spawnPos, spawnRotation);
                pred.GetComponent<PredictedBullet>()?.Initialize(spreadDir * data.bulletSpeed, data.bulletLifetime, bulletId);
            }

            if (Object.HasStateAuthority)
            {
                Runner.Spawn(data.bulletPrefab, spawnPos, spawnRotation, Object.InputAuthority, (runner, obj) =>
                {
                    obj.GetComponent<NetworkedProjectile>().Initialize(data, spreadDir * data.bulletSpeed, Object.InputAuthority, bulletId);
                });
            }
        }
        if (Object.HasStateAuthority) nextBulletId += data.bulletAmount;
    }

    // --- RESTORED HELPER METHODS FOR EXTERNAL SCRIPTS ---
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

    private Vector2 CalculateSpreadDirection(Vector2 baseDir, float spread, float maxDegrees)
    {
        float randomAngle = Random.Range(-spread, spread);
        float angle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg + randomAngle;
        return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }
}