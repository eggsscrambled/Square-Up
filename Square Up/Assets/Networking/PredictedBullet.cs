using UnityEngine;

public class PredictedBullet : MonoBehaviour
{
    private Vector2 velocity;
    private float lifetime;

    [Header("Detection Layers")]
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask combatLayer;

    private LayerMask collisionLayers; // Combined mask

    public void Initialize(Vector2 vel, float life)
    {
        velocity = vel;
        lifetime = life;

        // Combine both layers for collision detection
        collisionLayers = environmentLayer | combatLayer;
    }

    void Update()
    {
        // Calculate movement exactly like the networked version
        Vector2 movement = velocity * Time.deltaTime;
        float distance = movement.magnitude;

        // Raycast to detect collisions along the movement path (matches NetworkedProjectile logic)
        RaycastHit2D hit = Physics2D.Raycast(transform.position, velocity.normalized,
                                              distance, collisionLayers);

        if (hit.collider != null)
        {
            // Position at hit point for visual accuracy
            transform.position = hit.point;
            Destroy(gameObject);
            return;
        }

        // Apply movement (matches the networked version)
        transform.position += (Vector3)movement;

        // Update rotation to match velocity
        if (velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Handle lifetime
        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            Destroy(gameObject);
        }
    }
}