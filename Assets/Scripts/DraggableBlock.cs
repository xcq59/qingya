using UnityEngine;

public class DraggableBlock : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    
    [Header("Movement Settings")]
    public Axis dragAxis = Axis.X;
    public float maxMoveDistance = 1.0f;
    public float moveSpeed = 10f;

    [Header("Interaction Settings")]
    public float gapThreshold = 0.5f; // For player interaction check

    private Vector3 initialPosition;
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 dragOffset;

    private Rigidbody rb;

    void Start()
    {
        initialPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; // We control movement
    }


    void OnMouseDown()
    {
        // Only allow drag if NOT in Painting mode (assuming Normal mode allows interaction)
        if (GameManager.Instance.CurrentState == GameManager.GameState.Painting) return;

        isDragging = true;
        
        // Create a plane passing through the object, parallel to the camera or defined by axis
        // For axis movement, we want a plane that contains the axis and faces the camera roughly
        Vector3 planeNormal = Vector3.up;
        if (dragAxis == Axis.Y) planeNormal = Camera.main.transform.forward; // Face camera for Y movement
        else planeNormal = Vector3.up; // Horizontal plane for X/Z

        dragPlane = new Plane(planeNormal, transform.position);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragOffset = transform.position - hitPoint;
        }
    }

    void OnMouseUp()
    {
        isDragging = false;
        NavMeshUpdater.Instance?.UpdateNavMesh();
    }

    void Update()
    {
        if (isDragging)
        {
            HandleDrag();
        }
    }

    void HandleDrag()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 targetPos = hitPoint + dragOffset;

            // Constrain to Axis
            Vector3 constrainedPos = initialPosition;
            float currentDist = 0f;

            switch (dragAxis)
            {
                case Axis.X:
                    float diffX = targetPos.x - initialPosition.x;
                    diffX = Mathf.Clamp(diffX, -maxMoveDistance, maxMoveDistance);
                    constrainedPos.x += diffX;
                    break;
                case Axis.Y:
                    float diffY = targetPos.y - initialPosition.y;
                    diffY = Mathf.Clamp(diffY, -maxMoveDistance, maxMoveDistance);
                    constrainedPos.y += diffY;
                    break;
                case Axis.Z:
                    float diffZ = targetPos.z - initialPosition.z;
                    diffZ = Mathf.Clamp(diffZ, -maxMoveDistance, maxMoveDistance);
                    constrainedPos.z += diffZ;
                    break;
            }

            // Apply position
            if (rb != null)
            {
                rb.MovePosition(Vector3.Lerp(transform.position, constrainedPos, Time.deltaTime * moveSpeed));
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, constrainedPos, Time.deltaTime * moveSpeed);
            }
            
            // TODO: Update NavMesh if required here
        }
    }

    // Helper to check gap (can be called by PlayerController or GameLogic)
    public bool IsGapPassable(Vector3 playerPos, float playerRadius)
    {
        // Simple distance check between colliders would be better, but here is a basic check
        Collider col = GetComponent<Collider>();
        if (col == null) return false;

        Vector3 closestPoint = col.ClosestPoint(playerPos);
        float distance = Vector3.Distance(playerPos, closestPoint);
        
        // "Gap" is distance minus player radius (approx)
        return (distance - playerRadius) <= gapThreshold;
    }
}
