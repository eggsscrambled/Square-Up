using UnityEngine;

public class GarbageCleanup : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Time in seconds before this object is destroyed")]
    private float lifetime = 5f;

    private void Start()
    {
        // Destroy this GameObject after 'lifetime' seconds
        Destroy(gameObject, lifetime);
    }
}