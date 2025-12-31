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
        Transform portalOBJ = collider.gameObject.GetComponent<Transform>();

        // Calculate the offset from this portal's position
        Vector2 offset = portalOBJ.position - transform.position;

        // Apply the same offset to the teleport position
        portalOBJ.position = (Vector2)teleportPos.position + offset;

        Particles.Play();
        source.PlayOneShot(portalSFX);
    }
}