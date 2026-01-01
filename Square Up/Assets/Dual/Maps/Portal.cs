using UnityEngine;
using Fusion;

public class Portal : MonoBehaviour
{
    public Transform teleportPos;
    public ParticleSystem Particles;
    public AudioSource source;
    public AudioClip portalSFX;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        OnColliderEntered(collision);
    }

    private void OnColliderEntered(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            Transform portalOBJ = collider.gameObject.GetComponent<Transform>();

            // Calculate the offset from this portal's position
            Vector2 offset = portalOBJ.position - transform.position;

            // Calculate the offset relative to the destination portal's orientation
            Vector2 destOffset = teleportPos.position - transform.position;
            float relativeX = Mathf.Sign(destOffset.x) == 0 ? offset.x : offset.x * Mathf.Sign(destOffset.x);
            float relativeY = Mathf.Sign(destOffset.y) == 0 ? offset.y : offset.y * Mathf.Sign(destOffset.y);

            // Apply the corrected offset to the teleport position
            portalOBJ.position = (Vector2)teleportPos.position + new Vector2(relativeX, relativeY);

            Particles.Play();
            source.PlayOneShot(portalSFX);
        }
        
    }
}