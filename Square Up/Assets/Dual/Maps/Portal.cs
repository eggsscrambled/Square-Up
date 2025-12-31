using UnityEngine;

public class Portal : MonoBehaviour
{
    public Transform teleportPos;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        OnColliderEntered(collision);
    }

    private void OnColliderEntered(Collider2D collider)
    {
        Transform portalOBJ = collider.gameObject.GetComponent<Transform>();

        portalOBJ.position = new Vector2(teleportPos.position.x,portalOBJ.position.y);
    }

}