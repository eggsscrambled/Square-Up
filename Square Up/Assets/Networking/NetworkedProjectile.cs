using UnityEngine;
using Fusion;

public class NetworkedProjectile : NetworkBehaviour
{
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public TickTimer LifeTime { get; set; }
    [Networked] public int Damage { get; set; }
    [Networked] public float Knockback { get; set; }
    [Networked] public int BulletId { get; set; }

    [Header("Detection Layers")]
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask combatLayer;

    [Header("Particle Effects")]
    [SerializeField] private GameObject fizzlePrefab;
    [SerializeField] private GameObject environmentCollidePrefab;
    [SerializeField] private GameObject combatCollidePrefab;

    private TickTimer _ignoreOwnerTimer;
    private bool _hasNotifiedPrediction = false;
    private bool _hasSpawnedCombatEffect = false;
    private bool _hasSpawnedEnvironmentEffect = false;
    private bool _hasSpawnedFizzleEffect = false;

    public void Initialize(WeaponData data, Vector2 velocity, PlayerRef owner, int bulletId)
    {
        Velocity = velocity;
        Owner = owner;
        Damage = data.damage;
        Knockback = data.knockbackForce;
        BulletId = bulletId;
        LifeTime = TickTimer.CreateFromSeconds(Runner, data.bulletLifetime);
        _ignoreOwnerTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority && PredictedBulletManager.Instance != null)
        {
            PredictedBulletManager.Instance.OnNetworkedBulletSpawned(BulletId);
            _hasNotifiedPrediction = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_hasNotifiedPrediction && !Object.HasStateAuthority && PredictedBulletManager.Instance != null)
        {
            PredictedBulletManager.Instance.OnNetworkedBulletSpawned(BulletId);
            _hasNotifiedPrediction = true;
        }

        // LIFETIME EXPIRY
        if (LifeTime.Expired(Runner))
        {
            if (fizzlePrefab != null && !_hasSpawnedFizzleEffect && Runner.IsForward)
            {
                Instantiate(fizzlePrefab, transform.position, Quaternion.identity);
                _hasSpawnedFizzleEffect = true;
            }

            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
            return;
        }

        Vector2 movement = Velocity * Runner.DeltaTime;
        float distance = movement.magnitude;

        // ENVIRONMENT COLLISION
        RaycastHit2D envHit = Physics2D.Raycast(transform.position, Velocity.normalized, distance, environmentLayer);
        if (envHit.collider != null)
        {
            if (environmentCollidePrefab != null && !_hasSpawnedEnvironmentEffect && Runner.IsForward)
            {
                float angle = Mathf.Atan2(envHit.normal.y, envHit.normal.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0, 0, angle);
                Instantiate(environmentCollidePrefab, envHit.point, rotation);
                _hasSpawnedEnvironmentEffect = true;
            }

            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
            return;
        }

        // COMBAT COLLISION (LAG COMPENSATED)
        if (Runner.LagCompensation.Raycast(transform.position, Velocity.normalized, distance,
            Owner, out LagCompensatedHit hit, combatLayer, HitOptions.IncludePhysX))
        {
            // Check if we should ignore the owner
            if (hit.Hitbox != null && hit.Hitbox.Root.Object.InputAuthority == Owner && !_ignoreOwnerTimer.Expired(Runner))
            {
                // Ignore and continue movement
            }
            else
            {
                // Spawn combat hit effect
                if (combatCollidePrefab != null && hit.GameObject != null && !_hasSpawnedCombatEffect)
                {
                    Vector3 hitPoint = hit.Point;
                    Vector2 normal = -Velocity.normalized;
                    float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
                    Quaternion rotation = Quaternion.Euler(0, 0, angle);

                    GameObject effectInstance = Instantiate(combatCollidePrefab, hitPoint, rotation);
                    Color playerColor = GetPlayerColor(hit.GameObject);
                    ApplyColorToParticles(effectInstance, playerColor);

                    // Only set the flag during forward simulation to allow resimulation to spawn again
                    if (Runner.IsForward)
                    {
                        _hasSpawnedCombatEffect = true;
                    }
                }

                // Apply damage and despawn (STATE AUTHORITY ONLY)
                if (Object.HasStateAuthority)
                {
                    // Get hit player's input authority
                    PlayerRef hitPlayer = hit.Hitbox.Root.Object.InputAuthority;

                    // Call RPC to show blood effect on the hit player's client
                    Vector3 hitPoint = hit.Point;
                    Vector2 normal = -Velocity.normalized;
                    float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
                    Color playerColor = GetPlayerColor(hit.GameObject);

                    RPC_SpawnBloodEffect(hitPlayer, hitPoint, angle, playerColor);

                    ApplyHitLogic(hit);
                    Runner.Despawn(Object);
                    return;
                }

                return;
            }
        }

        // APPLY MOVEMENT
        transform.position += (Vector3)movement;

        if (Velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnBloodEffect(PlayerRef targetPlayer, Vector3 hitPoint, float angle, Color playerColor)
    {
        // Only spawn on the target player's client
        if (Runner.LocalPlayer == targetPlayer && combatCollidePrefab != null)
        {
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            GameObject effectInstance = Instantiate(combatCollidePrefab, hitPoint, rotation);
            ApplyColorToParticles(effectInstance, playerColor);
        }
    }

    private Color GetPlayerColor(GameObject playerObject)
    {
        SpriteRenderer spriteRenderer = playerObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = playerObject.GetComponentInParent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = playerObject.GetComponentInChildren<SpriteRenderer>();
        }

        return spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    private void ApplyColorToParticles(GameObject effectInstance, Color color)
    {
        ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem ps in particleSystems)
        {
            var main = ps.main;
            main.startColor = color;
        }
    }

    private void ApplyHitLogic(LagCompensatedHit hit)
    {
        if (hit.GameObject.TryGetComponent<PlayerData>(out var data))
        {
            data.TakeDamage(Damage);

            if (hit.GameObject.TryGetComponent<Rigidbody2D>(out var rb))
                rb.AddForce(Velocity.normalized * Knockback, ForceMode2D.Impulse);
        }
    }
}