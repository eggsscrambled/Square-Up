using UnityEngine;

public class PredictedBullet : MonoBehaviour
{
    private Vector2 velocity;
    private float lifetime;

    /// <summary>Fixed step to match server tick (e.g. 1/60). Movement and lifetime use this for consistent feel.</summary>
    private const float FixedStep = 1f / 60f;

    [Header("Detection Layers")]
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask combatLayer;

    [Header("Particle Effects")]
    public GameObject fizzlePrefab;
    public GameObject collidePrefab;

    private LayerMask collisionLayers;
    private float _stepAccumulator;

    // Unique ID to match with networked bullet
    public int BulletId { get; private set; }

    public void Initialize(Vector2 vel, float life, int bulletId)
    {
        velocity = vel;
        lifetime = life;
        BulletId = bulletId;
        collisionLayers = environmentLayer | combatLayer;
        _stepAccumulator = 0f;

        // Register this predicted bullet
        PredictedBulletManager.Instance?.RegisterPredictedBullet(this);
    }

    void Update()
    {
        _stepAccumulator += Time.deltaTime;

        while (_stepAccumulator >= FixedStep && lifetime > 0f)
        {
            _stepAccumulator -= FixedStep;
            lifetime -= FixedStep;

            Vector2 movement = velocity * FixedStep;
            float distance = movement.magnitude;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, velocity.normalized,
                                                distance, collisionLayers);

            if (hit.collider != null)
            {
                transform.position = hit.point;

                if (collidePrefab != null)
                {
                    float angle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg;
                    Instantiate(collidePrefab, hit.point, Quaternion.Euler(0, 0, angle));
                }

                DestroyPredicted();
                return;
            }

            transform.position += (Vector3)movement;
            if (velocity != Vector2.zero)
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }

        if (lifetime <= 0f)
        {
            if (fizzlePrefab != null)
                Instantiate(fizzlePrefab, transform.position, Quaternion.identity);
            DestroyPredicted();
        }
    }

    private void DestroyPredicted()
    {
        PredictedBulletManager.Instance?.UnregisterPredictedBullet(this);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Ensure we're unregistered even if destroyed externally
        PredictedBulletManager.Instance?.UnregisterPredictedBullet(this);
    }
}