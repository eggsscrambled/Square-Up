using UnityEngine;

public class Portal : MonoBehaviour
{
    public Transform teleportPos;

    private void OnTriggerEnter(Collider other)
    {
        OnColliderEntered(other);
    }

    private void OnColliderEntered(Collider collider)
    {
        Transform portalOBJ = collider.gameObject.GetComponent<Transform>();

        portalOBJ.position = new Vector2(teleportPos.position.x,portalOBJ.position.y);
    }

}