using UnityEngine;

public class PaintedPath : MonoBehaviour
{
    [Header("Path Settings")]
    public Transform startPoint;
    public Transform endPoint;
    public float completionThreshold = 0.5f; // Distance to B to consider complete

    [Header("Visuals")]
    public Renderer pathRenderer;
    public string progressProperty = "_Progress";

    [HideInInspector]
    public bool isPainted = false;

    private Material pathMat;
    private Collider pathCollider;

    void Start()
    {
        if (pathRenderer == null) pathRenderer = GetComponent<Renderer>();
        if (pathRenderer != null) pathMat = pathRenderer.material;
        
        pathCollider = GetComponent<Collider>();
        
        // Initial state: Invisible and Not Walkable
        SetProgress(0f);
        SetWalkable(false);
    }

    public void SetProgress(float progress)
    {
        if (pathMat != null)
        {
            pathMat.SetFloat(progressProperty, Mathf.Clamp01(progress));
        }
    }

    public void SetWalkable(bool walkable)
    {
        isPainted = walkable;
        
        // Update Collider or NavMesh layer
        // If using NavMesh, we might change the layer to "Walkable" or "Default" vs "Not Walkable"
        // Or enable/disable a NavMeshModifier
        
        if (pathCollider != null)
        {
            // If it's a trigger, it's not walkable. If it's solid, it might be.
            // Assuming NavMesh bakes on "Default" layer.
            // We can toggle the gameObject layer or the collider.
            // But for "Dynamic" updates, we usually need NavMeshSurface.UpdateNavMesh().
            // Here we will just set the collider trigger state or layer as a placeholder for the NavMesh update logic.
            
            // If walkable, it should be a solid collider for the mouse raycast to hit? 
            // Wait, painting happens when it's "Invisible and Not Walkable".
            // So the collider must be active for the Raycast to detect "Mouse on path projection".
            
            // The doc says: "Painted path... Invisible, Not Walkable".
            // But we need to raycast against it to calculate progress.
            // So maybe it's on a "Painting" layer that is not included in NavMesh, but is raycastable.
        }
    }

    public Vector3 GetProjectedPoint(Vector3 worldPos)
    {
        // Project worldPos onto the line segment AB
        Vector3 a = startPoint.position;
        Vector3 b = endPoint.position;
        Vector3 ab = b - a;
        Vector3 av = worldPos - a;
        
        float t = Vector3.Dot(av, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        
        return a + ab * t;
    }

    public float GetProgressAtPoint(Vector3 worldPos)
    {
        Vector3 a = startPoint.position;
        Vector3 b = endPoint.position;
        Vector3 ab = b - a;
        Vector3 av = worldPos - a;
        
        // Project onto AB direction
        float t = Vector3.Dot(av, ab.normalized) / ab.magnitude;
        return Mathf.Clamp01(t);
    }
}
