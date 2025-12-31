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

        portalOBJ.position = new Vector2(teleportPos.position.x,portalOBJ.position.y);

        Particles.Play();

        source.PlayOneShot(portalSFX);
    }

}