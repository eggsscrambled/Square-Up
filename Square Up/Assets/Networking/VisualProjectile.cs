using UnityEngine;

public class VisualProjectile : MonoBehaviour
{
    private Vector2 _velocity;
    private float _lifetime;
    private float _elapsedTime;
    private bool _useGravity;
    private float _gravityScale = 1f;
    private LayerMask _hitLayers;

    // New method to match NetworkedProjectile's settings
    public void Initialize(WeaponData weaponData, Vector2 direction, float lifetime,
                          LayerMask hitLayers, bool useGravity = false, float gravityScale = 1f)
    {
        _velocity = direction.normalized * weaponData.bulletSpeed;
        _lifetime = lifetime;
        _elapsedTime = 0f;
        _hitLayers = hitLayers; // Use the same layer mask as the networked version!
        _useGravity = useGravity;
        _gravityScale = gravityScale;
    }

    private void Update()
    {
        _elapsedTime += Time.deltaTime;

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
        Vector2 startPos = transform.position;

        // Check for collisions BEFORE moving (same as NetworkedProjectile)
        RaycastHit2D hit = Physics2D.Raycast(startPos, movement.normalized, movement.magnitude, _hitLayers);

        if (hit.collider != null)
        {
            // Move to hit point, then destroy
            transform.position = hit.point;
            Destroy(gameObject);
            return;
        }

        // No collision, move projectile
        transform.position = startPos + movement;

        // Rotate to face direction
        if (_velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}