using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// This ensures the FOV script runs AFTER movement scripts to prevent "ghosting/lag"
[DefaultExecutionOrder(100)]
public class FieldOfView : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private float fov = 360f;
    [SerializeField] private float viewDistance = 10f;
    [SerializeField] private int rayCount = 100;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;

    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        // Optimization: Set bounds manually once so Unity doesn't have to 
        // loop through all vertices every frame to check for culling.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(viewDistance * 2, viewDistance * 2, viewDistance * 2));

        // Sorting setup
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sortingLayerName = "Default";
        mr.sortingOrder = -100;
    }

    // LateUpdate is best for objects following a player or camera
    void LateUpdate()
    {
        int vertexCount = rayCount + 1 + 1;

        // Only recreate arrays if rayCount changes (optimization)
        if (vertices == null || vertices.Length != vertexCount)
        {
            vertices = new Vector3[vertexCount];
            triangles = new int[rayCount * 3];
        }

        // Center the FOV based on object rotation
        float startingAngle = transform.eulerAngles.z + (fov / 2f);
        float angle = startingAngle;
        float angleStep = fov / rayCount;

        vertices[0] = Vector3.zero; // Local center point

        for (int i = 0; i <= rayCount; i++)
        {
            float angleRad = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            // Cast the ray in World Space
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, viewDistance, layerMask);

            if (hit.collider == null)
            {
                // No hit: point is at the maximum distance
                vertices[i + 1] = dir * viewDistance;
            }
            else
            {
                // Hit: convert world hit point back to local space
                // Subtracting a tiny bit from the hit point prevents clipping inside walls
                Vector3 localHitPoint = transform.InverseTransformPoint(hit.point);
                vertices[i + 1] = localHitPoint;
            }

            if (i < rayCount)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            angle -= angleStep;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        // We skipped RecalculateBounds() for performance since we set it in Start()
    }
}