using UnityEngine;
using Fusion;

public class WeaponAimController : NetworkBehaviour
{
    [Networked] public NetworkId CurrentWeaponId { get; set; }
    [Networked] private TickTimer fireRateTimer { get; set; }
    [Networked] private NetworkBool lastFireState { get; set; }
    [Networked] private int fireCounter { get; set; }

    private WeaponPickup _currentWeapon;
    private PlayerData _playerData;
    private GameManager _gameManager;
    private bool _hasFiredThisFrame = false;

    private float _clientFireCooldown = 0f;
    private int _lastProcessedFireCounter = 0;

    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
        _gameManager = FindObjectOfType<GameManager>();
    }

    public override void FixedUpdateNetwork()
    {
        _hasFiredThisFrame = false;

        if (_clientFireCooldown > 0)
        {
            _clientFireCooldown -= Runner.DeltaTime;
        }

        ResolveWeaponReference();

        if (_currentWeapon == null || _playerData == null || _playerData.Dead)
            return;

        if (GetInput(out NetworkInputData input))
        {
            if (Object.HasStateAuthority && input.aimDirection.magnitude > 0.1f)
            {
                Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;
                _currentWeapon.UpdateAimDirection(aimFromWeapon);
            }

            if (Object.HasInputAuthority)
            {
                WeaponData data = _currentWeapon.GetWeaponData();

                bool canFire = fireRateTimer.ExpiredOrNotRunning(Runner) &&
                               _clientFireCooldown <= 0 &&
                               !_hasFiredThisFrame &&
                               fireCounter == _lastProcessedFireCounter;

                if (data != null && canFire)
                {
                    bool shouldFire = data.isAutomatic
                        ? input.fire
                        : (input.fire && !lastFireState);

                    if (shouldFire)
                    {
                        _hasFiredThisFrame = true;
                        _clientFireCooldown = (1f / data.fireRate) + 0.05f;

                        if (Object.HasStateAuthority)
                        {
                            fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / data.fireRate);
                            fireCounter++;
                            _lastProcessedFireCounter = fireCounter;
                        }
                        else
                        {
                            _lastProcessedFireCounter++;
                        }

                        Vector3 weaponHoldPos = _currentWeapon.GetWeaponHoldPosition();
                        Vector2 aimFromWeapon = (input.mouseWorldPosition - (Vector2)weaponHoldPos).normalized;

                        FireWeapon(aimFromWeapon, data);
                    }
                }

                if (Object.HasStateAuthority)
                {
                    lastFireState = input.fire;
                }
            }
        }
    }

    private void FireWeapon(Vector2 aimDirection, WeaponData weaponData)
    {
        if (aimDirection.magnitude < 0.1f) return;

        Transform fireOrigin = _currentWeapon.transform.Find("FireOrigin");
        Vector3 spawnPos = fireOrigin != null ? fireOrigin.position : _currentWeapon.transform.position;

        if (Object.HasStateAuthority)
        {
            SpawnFunctionalProjectiles(aimDirection, spawnPos, weaponData);

            // Get projectile settings from the bullet prefab using getter methods
            NetworkedProjectile projSettings = weaponData.bulletPrefab.GetComponent<NetworkedProjectile>();
            int hitLayersMask = projSettings != null ? projSettings.GetHitLayers() : Physics2D.AllLayers;
            bool useGravity = projSettings != null && projSettings.GetUseGravity();
            float gravityScale = projSettings != null ? projSettings.GetGravityScale() : 1f;

            RPC_SpawnVisualProjectiles(
                aimDirection,
                spawnPos,
                weaponData.bulletAmount,
                _currentWeapon.Object.Id,
                weaponData.bulletSpeed,
                weaponData.bulletLifetime,
                weaponData.spreadAmount,
                weaponData.maxSpreadDegrees,
                hitLayersMask,
                useGravity,
                gravityScale
            );
        }
        else
        {
            RPC_RequestFireWeapon(aimDirection, spawnPos);
        }
    }

    private void SpawnFunctionalProjectiles(Vector2 aimDirection, Vector3 spawnPos, WeaponData weaponData)
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnVisualProjectiles(
        Vector2 aimDirection,
        Vector3 spawnPos,
        int bulletAmount,
        NetworkId weaponId,
        float bulletSpeed,
        float bulletLifetime,
        float spreadAmount,
        float maxSpread,
        int hitLayersMask,      // Changed to int for RPC compatibility
        bool useGravity,
        float gravityScale)
    {
        WeaponPickup weapon = _currentWeapon;
        if (weapon == null || weapon.Object.Id != weaponId)
        {
            if (Runner.TryFindObject(weaponId, out NetworkObject weaponObj))
            {
                weapon = weaponObj.GetComponent<WeaponPickup>();
            }
        }

        if (weapon == null) return;

        WeaponData weaponData = weapon.GetWeaponData();
        if (weaponData == null) return;

        // Convert int back to LayerMask
        LayerMask hitLayers = hitLayersMask;

        for (int i = 0; i < bulletAmount; i++)
        {
            Vector2 direction = CalculateSpreadDirection(aimDirection.normalized, spreadAmount, maxSpread);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, direction);

            GameObject visualProjectile = Instantiate(
                weaponData.bulletPrefab.gameObject,
                spawnPos,
                spawnRotation
            );

            VisualProjectile visualComp = visualProjectile.AddComponent<VisualProjectile>();
            visualComp.Initialize(weaponData, direction, bulletLifetime, hitLayers, useGravity, gravityScale);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestFireWeapon(Vector2 aimDirection, Vector3 spawnPos)
    {
        if (_currentWeapon == null || _playerData == null || _playerData.Dead)
            return;

        WeaponData weaponData = _currentWeapon.GetWeaponData();
        if (weaponData == null) return;

        fireRateTimer = TickTimer.CreateFromSeconds(Runner, 1f / weaponData.fireRate);
        fireCounter++;

        SpawnFunctionalProjectiles(aimDirection, spawnPos, weaponData);

        // Get projectile settings from the bullet prefab using getter methods
        NetworkedProjectile projSettings = weaponData.bulletPrefab.GetComponent<NetworkedProjectile>();
        int hitLayersMask = projSettings != null ? projSettings.GetHitLayers() : Physics2D.AllLayers;
        bool useGravity = projSettings != null && projSettings.GetUseGravity();
        float gravityScale = projSettings != null ? projSettings.GetGravityScale() : 1f;

        RPC_SpawnVisualProjectiles(
            aimDirection,
            spawnPos,
            weaponData.bulletAmount,
            _currentWeapon.Object.Id,
            weaponData.bulletSpeed,
            weaponData.bulletLifetime,
            weaponData.spreadAmount,
            weaponData.maxSpreadDegrees,
            hitLayersMask,
            useGravity,
            gravityScale
        );
    }

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