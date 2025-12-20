using UnityEngine;
using UnityEngine.UI;

public class PaintingSystem : MonoBehaviour
{
    public LayerMask pathLayer; // Layer for PaintedPaths
    public float startPointClickRadius = 1.0f;

    private PaintedPath currentPath;
    private bool isDragging = false;

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

    void TryStartPainting()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, pathLayer))
        {
            PaintedPath path = hit.collider.GetComponent<PaintedPath>();
            if (path != null && !path.isPainted)
            {
                // Check if clicked near Start Point
                if (Vector3.Distance(hit.point, path.startPoint.position) <= startPointClickRadius)
                {
                    currentPath = path;
                    isDragging = true;
                }
            }
        }
    }

    void UpdatePainting()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // We need to raycast against the path collider to get the position
        if (Physics.Raycast(ray, out hit, 100f, pathLayer))
        {
            if (hit.collider.gameObject == currentPath.gameObject)
            {
                float progress = currentPath.GetProgressAtPoint(hit.point);
                currentPath.SetProgress(progress);
            }
        }
    }

    void FinishPainting()
    {
        isDragging = false;
        if (currentPath == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool success = false;

        // Check if mouse is near End Point
        // We can use the last progress or raycast again
        // Let's use the progress from the last frame or calculate again if possible
        // But mouse might be off the path now.
        
        // Doc says: "Mouse reach / exceed B point".
        // We can check distance from mouse ray to B point on screen or world plane
        
        Plane pathPlane = new Plane(Vector3.up, currentPath.transform.position); // Assuming flat path
        float enter;
        if (pathPlane.Raycast(ray, out enter))
        {
            Vector3 worldMousePos = ray.GetPoint(enter);
            float distToB = Vector3.Distance(worldMousePos, currentPath.endPoint.position);
            
            // Or check progress
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
            // Trigger NavMesh Update here if needed
            NavMeshUpdater.Instance?.UpdateNavMesh();
        }
        else
        {
            // Revert
            currentPath.SetProgress(0f);
        }

        currentPath = null;
    }

    // UI Button Callback
    public void OnInkButtonClicked()
    {
        GameManager.Instance.TogglePaintingMode();
    }
}
