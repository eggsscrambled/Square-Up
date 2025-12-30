// Simple visual-only projectile for client-side prediction
using UnityEngine;

public class VisualProjectile : MonoBehaviour
{
    private Vector2 _velocity;
    private float _lifetime;
    private float _elapsedTime;
    private bool _useGravity;
    private float _gravityScale = 1f;
    private LayerMask _hitLayers;

    public void Initialize(WeaponData weaponData, Vector2 direction, float lifetime)
    {
        _velocity = direction.normalized * weaponData.bulletSpeed;
        _lifetime = lifetime;
        _elapsedTime = 0f;

        // Match the gravity settings from the networked projectile prefab
        // You may need to expose these in WeaponData if they vary per weapon
        _hitLayers = LayerMask.GetMask("Default", "Player", "Ground"); // Adjust layer names as needed
    }

    private void Update()
    {
        _elapsedTime += Time.deltaTime;

        // Self-destruct after lifetime
        if (_elapsedTime >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Apply gravity if needed
        if (_useGravity)
        {
            Vector2 gravity = new Vector2(0, Physics2D.gravity.y * _gravityScale * Time.deltaTime);
            _velocity += gravity;
        }

        Vector2 movement = _velocity * Time.deltaTime;

        // Check for collisions
        RaycastHit2D hit = Physics2D.Raycast(transform.position, movement.normalized, movement.magnitude, _hitLayers);

        if (hit.collider != null)
        {
            // Hit something, destroy visual projectile
            Destroy(gameObject);
            return;
        }

        // Move projectile
        transform.position += (Vector3)movement;

        // Rotate to face direction
        if (_velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}