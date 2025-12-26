using UnityEngine;
using UnityEngine.UI;

public class PaintingSystem : MonoBehaviour
{
    public LayerMask pathLayer; // Layer for PaintedPaths
    public float startPointClickRadius = 1.0f;

    private PaintedPath currentPath;
    private bool isDragging = false;

    public static PaintingSystem Instance { get; private set; }
    public bool IsDraggingPath => isDragging;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (pathLayer.value == 0)
        {
            Debug.LogWarning("PaintingSystem: 'Path Layer' is not set! Please assign a LayerMask in the Inspector.");
        }
    }

    void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Painting) return;

        if (Input.GetMouseButtonDown(0))
        {
            TryStartPainting();
        }
        else if (Input.GetMouseButton(0) && isDragging && currentPath != null)
        {
            UpdatePainting();
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            FinishPainting();
        }
    }

    public bool IsMouseOverStartPoint()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Painting) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, pathLayer))
        {
            PaintedPath path = hit.collider.GetComponentInParent<PaintedPath>();
            if (path != null && !path.isPainted)
            {
                // Debug.Log($"Hovering path: {path.name}, Dist to start: {Vector3.Distance(hit.point, path.startPoint.position)}");
                if (Vector3.Distance(hit.point, path.startPoint.position) <= startPointClickRadius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    void TryStartPainting()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, pathLayer))
        {
            PaintedPath path = hit.collider.GetComponentInParent<PaintedPath>();
            if (path != null && !path.isPainted)
            {
                float dist = Vector3.Distance(hit.point, path.startPoint.position);
                // Debug.Log($"Clicked path: {path.name}, Hit Point: {hit.point}, Start Point: {path.startPoint.position}, Dist: {dist}");
                
                // Check if clicked near Start Point
                if (dist <= startPointClickRadius)
                {
                    currentPath = path;
                    isDragging = true;
                    // Debug.Log("Started Painting!");
                }
                else
                {
                    // Debug.Log("Clicked path but too far from start point.");
                }
            }
            else
            {
                // Debug.Log($"Hit object {hit.collider.name} but no PaintedPath component found or already painted.");
            }
        }
    }

    void UpdatePainting()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Prefer raycast against the path mesh; if the mouse slips off the mesh while dragging,
        // fall back to ray-plane intersection and still compute projected progress along AB.
        Vector3 worldPoint = default;
        bool hasPoint = false;

        if (Physics.Raycast(ray, out hit, 100f, pathLayer))
        {
            if (hit.collider.transform.IsChildOf(currentPath.transform) || hit.collider.gameObject == currentPath.gameObject)
            {
                worldPoint = hit.point;
                hasPoint = true;
            }
        }

        if (!hasPoint)
        {
            Plane pathPlane = new Plane(currentPath.transform.up, currentPath.transform.position);
            float enter;
            if (pathPlane.Raycast(ray, out enter))
            {
                worldPoint = ray.GetPoint(enter);
                hasPoint = true;
            }
        }

        if (hasPoint)
        {
            float progress = currentPath.GetProgressAtPoint(worldPoint);
            currentPath.SetProgress(progress);
        }
    }

    void FinishPainting()
    {
        isDragging = false;
        if (currentPath == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool success = false;

        // Check if mouse is near End Point
        // We can use the last progress or raycast again
        // Let's use the progress from the last frame or calculate again if possible
        // But mouse might be off the path now.
        
        // Doc says: "Mouse reach / exceed B point".
        // We can check distance from mouse ray to B point on screen or world plane
        
        // Try to resolve a world point on/near the path to decide completion.
        Vector3 worldMousePos = default;
        bool hasPoint = false;
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, pathLayer))
        {
            if (hit.collider.transform.IsChildOf(currentPath.transform) || hit.collider.gameObject == currentPath.gameObject)
            {
                worldMousePos = hit.point;
                hasPoint = true;
            }
        }

        if (!hasPoint)
        {
            Plane pathPlane = new Plane(currentPath.transform.up, currentPath.transform.position);
            float enter;
            if (pathPlane.Raycast(ray, out enter))
            {
                worldMousePos = ray.GetPoint(enter);
                hasPoint = true;
            }
        }

        if (hasPoint)
        {
            float distToB = Vector3.Distance(worldMousePos, currentPath.endPoint.position);
            float progress = currentPath.GetProgressAtPoint(worldMousePos);
            if (distToB <= currentPath.completionThreshold || progress >= 0.95f)
            {
                success = true;
            }
        }

        if (success)
        {
            currentPath.SetProgress(1.0f);
            currentPath.SetWalkable(true);
            // NavMesh Update is no longer needed if using NavMeshObstacle logic
            // NavMeshUpdater.Instance?.UpdateNavMesh();
        }
        else
        {
            // Revert with reverse gradient fade-out
            currentPath.RetractToHidden();
        }

        currentPath = null;
    }

    // UI Button Callback
    public void OnInkButtonClicked()
    {
        GameManager.Instance.TogglePaintingMode();
    }
}
