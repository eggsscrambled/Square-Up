using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }
    [Networked] private NetworkBool wasFirePressedLastFrame { get; set; }
    [Networked] private int nextBulletId { get; set; }

    [Header("Visuals")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    private TickTimer _clientFireRateTimer;
    private int _clientNextBulletId = 0;

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
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            bool firePressed = input.buttons.IsSet(MyButtons.Fire);
            WeaponData data = _currentWeapon.GetWeaponData();

            bool canFire = Object.HasStateAuthority
                ? fireRateTimer.ExpiredOrNotRunning(Runner)
                : _clientFireRateTimer.ExpiredOrNotRunning(Runner);

            if (data != null && canFire)
            {
                bool shouldFire = false;

                if (data.isAutomatic)
                {
                    shouldFire = firePressed;
                }
                else
                {
                    shouldFire = firePressed && !wasFirePressedLastFrame;
                }

                if (shouldFire)
                {
                    if (Runner.IsForward) TriggerLocalMuzzleFlash();

                    Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
                    Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
                    Vector2 baseAimDir = (input.mouseWorldPosition - (Vector2)spawnPos).normalized;

                    // Generate bullet ID for this shot
                    int bulletId;
                    if (Object.HasStateAuthority)
                    {
                        bulletId = nextBulletId;
                        nextBulletId += data.bulletAmount; // Reserve IDs for all bullets in this shot
                        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);
                        if (_playerController != null) _playerController.ApplyRecoil(-input.aimDirection.normalized * data.recoilForce);
                    }
                    else
                    {
                        bulletId = _clientNextBulletId;
                        _clientNextBulletId += data.bulletAmount;
                        _clientFireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);
                    }

                    Fire(baseAimDir, data, spawnPos, input.inputTick, bulletId);
                }
            }

            if (Object.HasStateAuthority)
            {
                wasFirePressedLastFrame = firePressed;
            }
        }
    }

    private void TriggerLocalMuzzleFlash()
    {
        if (_currentWeapon == null) return;

        WeaponData data = _currentWeapon.GetWeaponData();
        GameObject flashPrefab = data?.muzzleFlashPrefab ?? muzzleFlashPrefab; // Fallback to default

        if (flashPrefab == null) return;

        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;
        GameObject flash = Instantiate(flashPrefab, spawnPos, _currentWeapon.transform.rotation);
        Destroy(flash, 1.0f);
    }

    private void Fire(Vector2 direction, WeaponData data, Vector3 spawnPos, int seed, int startBulletId)
    {
        Debug.Log($"[Fire] Using seed: {seed}, StartBulletId: {startBulletId}, HasStateAuthority: {Object.HasStateAuthority}");

        for (int i = 0; i < data.bulletAmount; i++)
        {
            int bulletId = startBulletId + i;
            Random.InitState(seed + i);

            Vector2 spreadDir = CalculateSpreadDirection(direction, data.spreadAmount, data.maxSpreadDegrees);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, spreadDir);

            bool isClient = !Object.HasStateAuthority && Object.HasInputAuthority;

            if (isClient && data.bulletVisualPrefab != null)
            {
                GameObject predicted = Instantiate(data.bulletVisualPrefab, spawnPos, spawnRotation);
                var predBullet = predicted.GetComponent<PredictedBullet>();
                if (predBullet != null)
                {
                    predBullet.Initialize(spreadDir * data.bulletSpeed, data.bulletLifetime, bulletId);
                }
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

    public void SetCurrentWeapon(WeaponPickup weapon)
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = weapon;
            CurrentWeaponId = weapon != null ? weapon.Object.Id : default;
            wasFirePressedLastFrame = false;
        }
        _clientFireRateTimer = TickTimer.None;
    }

    public void ClearCurrentWeapon()
    {
        if (Object.HasStateAuthority)
        {
            _currentWeapon = null;
            CurrentWeaponId = default;
            wasFirePressedLastFrame = false;
        }
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