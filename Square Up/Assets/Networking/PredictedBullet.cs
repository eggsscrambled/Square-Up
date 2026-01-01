using UnityEngine;

public class PredictedBullet : MonoBehaviour
{
    private Vector2 velocity;
    private float lifetime;

    [Header("Detection Layers")]
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask combatLayer;

    [Header("Particle Effects")]
    public GameObject fizzlePrefab;
    public GameObject collidePrefab;

    private LayerMask collisionLayers;

    // Unique ID to match with networked bullet
    public int BulletId { get; private set; }

    public void Initialize(Vector2 vel, float life, int bulletId)
    {
        velocity = vel;
        lifetime = life;
        BulletId = bulletId;
        collisionLayers = environmentLayer | combatLayer;

        // Register this predicted bullet
        PredictedBulletManager.Instance?.RegisterPredictedBullet(this);
    }

    void Update()
    {
        Vector2 movement = velocity * Time.deltaTime;
        float distance = movement.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, velocity.normalized,
                                              distance, collisionLayers);

        if (hit.collider != null)
        {
            transform.position = hit.point;

            // Instantiate collision effect aligned to surface normal
            if (collidePrefab != null)
            {
                // Calculate rotation from surface normal
                float angle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0, 0, angle);

                Instantiate(collidePrefab, hit.point, rotation);
            }

            DestroyPredicted();
            return;
        }

        transform.position += (Vector3)movement;

        if (velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            // Instantiate fizzle effect at bullet position
            if (fizzlePrefab != null)
            {
                Instantiate(fizzlePrefab, transform.position, Quaternion.identity);
            }

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