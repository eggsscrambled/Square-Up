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

    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask combatLayer;

    private TickTimer _ignoreOwnerTimer;

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

    public override void FixedUpdateNetwork()
    {
        if (LifeTime.Expired(Runner))
        {
            if (Object.HasStateAuthority)
            {
                GlobalFXManager.Instance.RequestHitEffect(transform.position, 0, "fizzle", Color.white);
                Runner.Despawn(Object);
            }
            return;
        }

        Vector2 movement = Velocity * Runner.DeltaTime;

        RaycastHit2D envHit = Physics2D.Raycast(transform.position, Velocity.normalized, movement.magnitude, environmentLayer);
        if (envHit.collider != null)
        {
            if (Object.HasStateAuthority)
            {
                float angle = Mathf.Atan2(envHit.normal.y, envHit.normal.x) * Mathf.Rad2Deg;
                GlobalFXManager.Instance.RequestHitEffect(envHit.point, angle, "environment", Color.white);
                Runner.Despawn(Object);
            }
            return;
        }

        if (Runner.LagCompensation.Raycast(transform.position, Velocity.normalized, movement.magnitude,
            Owner, out LagCompensatedHit hit, combatLayer, HitOptions.IncludePhysX))
        {
            if (hit.Hitbox != null && hit.Hitbox.Root.Object.InputAuthority == Owner && !_ignoreOwnerTimer.Expired(Runner))
            {
                transform.position += (Vector3)movement;
            }
            else if (Object.HasStateAuthority)
            {
                float angle = Mathf.Atan2(-Velocity.y, -Velocity.x) * Mathf.Rad2Deg;
                GlobalFXManager.Instance.RequestHitEffect(hit.Point, angle, "blood", GetPlayerColor(hit.GameObject));
                ApplyHitLogic(hit);
                Runner.Despawn(Object);
            }
            return;
        }

        transform.position += (Vector3)movement;
        if (Velocity != Vector2.zero)
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg);
    }

    private Color GetPlayerColor(GameObject obj)
    {
        var sr = obj.GetComponentInChildren<SpriteRenderer>() ?? obj.GetComponentInParent<SpriteRenderer>();
        return sr != null ? sr.color : Color.red;
    }

    private void ApplyHitLogic(LagCompensatedHit hit)
    {
        if (hit.GameObject.TryGetComponent<PlayerData>(out var d))
        {
            d.TakeDamage(Damage);
            if (hit.GameObject.TryGetComponent<Rigidbody2D>(out var rb))
                rb.AddForce(Velocity.normalized * Knockback, ForceMode2D.Impulse);
        }
    }
}