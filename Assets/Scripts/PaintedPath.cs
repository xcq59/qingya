using UnityEngine;
using UnityEngine.AI;

public class PaintedPath : MonoBehaviour
{
    [Header("Path Settings")]
    public Transform startPoint;
    public Transform endPoint;
    public float completionThreshold = 0.5f; // Distance to B to consider complete

    [Header("Visuals")]
    public Renderer pathRenderer;
    public string progressProperty = "_Progress";

    [Tooltip("Seconds to fade back to hidden when painting is interrupted.")]
    public float retractDuration = 0.25f;

    [HideInInspector]
    public bool isPainted = false;

    private Material pathMat;
    private Collider pathCollider;
    private NavMeshObstacle navObstacle;

    private float currentProgress = 0f;
    private Coroutine retractRoutine;

    void Start()
    {
        if (pathRenderer == null) pathRenderer = GetComponent<Renderer>();
        if (pathRenderer != null) pathMat = pathRenderer.material;
        
        pathCollider = GetComponent<Collider>();
        navObstacle = GetComponent<NavMeshObstacle>();
        
        // Ensure we have an obstacle if we want to block navigation dynamically
        if (navObstacle == null)
        {
            // Try to add one if missing, or just warn
            // navObstacle = gameObject.AddComponent<NavMeshObstacle>();
            // navObstacle.carving = true;
        }

        // Initial state: Invisible and Not Walkable
        SetProgress(0f);
        SetWalkable(false);
    }

    public void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);
        if (pathMat != null)
        {
            pathMat.SetFloat(progressProperty, currentProgress);
        }
    }

    public void RetractToHidden()
    {
        if (retractRoutine != null)
        {
            StopCoroutine(retractRoutine);
        }
        retractRoutine = StartCoroutine(RetractRoutine());
    }

    System.Collections.IEnumerator RetractRoutine()
    {
        float start = currentProgress;
        float duration = Mathf.Max(0.01f, retractDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            SetProgress(Mathf.Lerp(start, 0f, u));
            yield return null;
        }

        SetProgress(0f);
        retractRoutine = null;
    }

    public void SetWalkable(bool walkable)
    {
        isPainted = walkable;
        
        // Control NavMeshObstacle to block/allow path
        if (navObstacle != null)
        {
            // If walkable, disable obstacle (allow passage)
            // If not walkable, enable obstacle (block passage)
            navObstacle.enabled = !walkable;
            navObstacle.carving = !walkable; 
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
