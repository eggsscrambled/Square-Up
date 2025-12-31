using UnityEngine;

public class PredictedBullet : MonoBehaviour
{
    private Vector2 velocity;
    private float lifetime;

    [SerializeField] private LayerMask collisionLayers;

    public void Initialize(Vector2 vel, float life)
    {
        velocity = vel;
        lifetime = life;
    }

    void Update()
    {
        transform.position += (Vector3)velocity * Time.deltaTime;

        if (velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collided object's layer is in our collision layers mask
        if (((1 << other.gameObject.layer) & collisionLayers) != 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the collided object's layer is in our collision layers mask
        if (((1 << collision.gameObject.layer) & collisionLayers) != 0)
        {
            Destroy(gameObject);
        }
    }
}