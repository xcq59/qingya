using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class DraggableBlock : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    public enum PlatformZone
    {
        NearA,
        NearB,
        Middle
    }

    public enum ConnectivityDistanceMode
    {
        Horizontal,
        Vertical,
        ThreeD
    }
    
    [Header("Movement Settings")]
    public Axis dragAxis = Axis.X;
    public float maxMoveDistance = 1.0f;
    public float moveSpeed = 10f;

    [Header("Drag Input")]
    public float dragRaycastDistance = 200f;
    [Tooltip("Optional. If 0/None, raycast hits everything.")]
    public LayerMask dragLayerMask;
    [Tooltip("Mouse movement (in pixels) required to start dragging after clicking the platform.")]
    public float dragStartPixels = 6f;

    [Header("Drag Debug")]
    public bool debugDragLogs = false;
    [Tooltip("If true, clicking UI will not start dragging.")]
    public bool blockWhenPointerOverUI = true;

    [Header("Connectivity Debug")]
    public bool debugConnectivityLogs = false;

    public static bool IsDraggingAny { get; private set; }

    [Header("Connectivity (A/B/P rules)")]
    public Transform anchorA;
    public Transform anchorB;
    public float connectDistance = 1.0f;

    [Tooltip("How to measure distance to anchors. Use Vertical for elevator-like platforms.")]
    public ConnectivityDistanceMode distanceMode = ConnectivityDistanceMode.Horizontal;

    [Tooltip("Only used when distanceMode is Vertical: require XZ distance <= this to be considered for connection.")]
    public float verticalModeHorizontalTolerance = 1.5f;

    [Tooltip("Optional: A<->P OffMeshLink (enabled when platform near A).")]
    public OffMeshLink linkA;
    [Tooltip("Optional: B<->P OffMeshLink (enabled when platform near B).")]
    public OffMeshLink linkB;

    [Tooltip("Optional: where the player stands on the platform.")]
    public Transform standPoint;

    [Header("Boarding Points (on NavMesh)")]
    [Tooltip("A-side approach point on NavMesh (player walks here before boarding when platform near A).")]
    public Transform boardApproachA;
    [Tooltip("B-side approach point on NavMesh (player walks here before boarding when platform near B).")]
    public Transform boardApproachB;

    public PlatformZone CurrentZone { get; private set; } = PlatformZone.Middle;
    public bool IsConnectedToA => CurrentZone == PlatformZone.NearA;
    public bool IsConnectedToB => CurrentZone == PlatformZone.NearB;

    public float LastDistToA { get; private set; }
    public float LastDistToB { get; private set; }

    [Header("Interaction Settings")]
    public float gapThreshold = 0.5f; // For player interaction check

    private Vector3 initialPosition;
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 dragOffset;

    private bool pendingDrag = false;
    private Vector2 pendingDragScreenPos;

    private PlatformZone lastLoggedZone = PlatformZone.Middle;
    private bool hasLoggedZone = false;

    private Rigidbody rb;

    void OnDrawGizmosSelected()
    {
        // Visualize anchors and connect radius in Scene view to help tuning.
        if (anchorA != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(anchorA.position, connectDistance);
            Gizmos.DrawLine(transform.position, anchorA.position);
        }

        if (anchorB != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(anchorB.position, connectDistance);
            Gizmos.DrawLine(transform.position, anchorB.position);
        }

        if (standPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(standPoint.position, 0.2f);
        }

        if (boardApproachA != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(boardApproachA.position, 0.25f);
        }

        if (boardApproachB != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(boardApproachB.position, 0.25f);
        }
    }

    void Start()
    {
        initialPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; // We control movement
    }


    void Update()
    {
        // Only allow drag if NOT in Painting mode
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Painting)
        {
            if (isDragging) EndDrag();
            UpdateConnectivity();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDrag();
        }

        // If we clicked the platform but haven't moved enough yet, wait.
        if (pendingDrag && !isDragging && Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - pendingDragScreenPos;
            if (delta.sqrMagnitude >= dragStartPixels * dragStartPixels)
            {
                // Start actual dragging now.
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                BeginDrag(ray);
            }
        }

        if (isDragging)
        {
            if (Input.GetMouseButton(0))
            {
                HandleDrag();
            }

            if (Input.GetMouseButtonUp(0))
            {
                EndDrag();
            }
        }
        else
        {
            // Mouse released without starting drag -> treat as click (e.g., for boarding)
            if (pendingDrag && Input.GetMouseButtonUp(0))
            {
                pendingDrag = false;
            }
        }

        UpdateConnectivity();
    }

    void TryBeginDrag()
    {
        if (Camera.main == null)
        {
            if (debugDragLogs) Debug.LogWarning("DraggableBlock: Camera.main is null. Tag your main camera as 'MainCamera'.", this);
            return;
        }

        if (blockWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (debugDragLogs) Debug.Log("DraggableBlock: Pointer is over UI, drag blocked.", this);
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        bool didHit = false;
        if (dragLayerMask.value == 0)
        {
            didHit = Physics.Raycast(ray, out hit, dragRaycastDistance);
        }
        else
        {
            didHit = Physics.Raycast(ray, out hit, dragRaycastDistance, dragLayerMask);
        }

        if (!didHit)
        {
            if (debugDragLogs) Debug.Log("DraggableBlock: Raycast did not hit anything.", this);
            return;
        }

        DraggableBlock block = hit.collider.GetComponentInParent<DraggableBlock>();

        if (block == null)
        {
            if (debugDragLogs)
            {
                Debug.Log($"DraggableBlock: Hit '{hit.collider.name}' but it has no DraggableBlock in parents. " +
                          $"(Layer={LayerMask.LayerToName(hit.collider.gameObject.layer)})", this);
            }
            return;
        }

        if (block != this)
        {
            // Hit another platform, not us. Silent return (normal multi-platform scenario).
            return;
        }

        // Common pitfall: using 2D colliders in a 3D raycast setup
        if (hit.collider.GetComponent<Collider2D>() != null)
        {
            if (debugDragLogs) Debug.LogWarning("DraggableBlock: Hit has Collider2D, but dragging uses 3D Physics.Raycast. Use Collider (3D) instead.", this);
            return;
        }

        if (debugDragLogs)
        {
            Debug.Log($"DraggableBlock: BeginDrag hit '{hit.collider.name}' on '{name}'. HitPoint={hit.point}", this);
        }

        // Don't start dragging immediately: allow click to be used for boarding.
        pendingDrag = true;
        pendingDragScreenPos = Input.mousePosition;
    }

    void BeginDrag(Ray ray)
    {
        pendingDrag = false;
        isDragging = true;
        IsDraggingAny = true;

        // Use current position as the new constraint origin for this drag session.
        initialPosition = transform.position;

        // Plane through object, chosen to feel good for the axis
        Vector3 planeNormal = Vector3.up;
        if (dragAxis == Axis.Y && Camera.main != null) planeNormal = Camera.main.transform.forward;
        else planeNormal = Vector3.up;

        dragPlane = new Plane(planeNormal, transform.position);

        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragOffset = transform.position - hitPoint;
        }
    }

    void EndDrag()
    {
        isDragging = false;
        IsDraggingAny = false;
        pendingDrag = false;
        UpdateConnectivity();
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
            
            // Calculate delta before applying
            Vector3 previousPos = transform.position;

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
            
            // Sync Player if on top
            SyncPlayerPosition(transform.position - previousPos);
        }
    }

    void UpdateConnectivity()
    {
        if (anchorA == null || anchorB == null)
        {
            CurrentZone = PlatformZone.Middle;
            if (linkA != null) linkA.activated = false;
            if (linkB != null) linkB.activated = false;

            if (debugConnectivityLogs && (!hasLoggedZone || lastLoggedZone != CurrentZone))
            {
                Debug.LogWarning($"DraggableBlock '{name}': anchorA/anchorB not set, forcing zone=Middle.", this);
                lastLoggedZone = CurrentZone;
                hasLoggedZone = true;
            }
            return;
        }

        float distA = DistanceToAnchor(transform.position, anchorA.position, distanceMode, verticalModeHorizontalTolerance);
        float distB = DistanceToAnchor(transform.position, anchorB.position, distanceMode, verticalModeHorizontalTolerance);

        LastDistToA = distA;
        LastDistToB = distB;

        PlatformZone newZone;
        // Tie-break: if distances are equal and within connectDistance, prefer A
        if (distA <= connectDistance && distA <= distB)
        {
            newZone = PlatformZone.NearA;
        }
        else if (distB <= connectDistance)
        {
            newZone = PlatformZone.NearB;
        }
        else
        {
            newZone = PlatformZone.Middle;
        }

        CurrentZone = newZone;

        if (linkA != null) linkA.activated = (CurrentZone == PlatformZone.NearA);
        if (linkB != null) linkB.activated = (CurrentZone == PlatformZone.NearB);

        if (debugConnectivityLogs && (!hasLoggedZone || lastLoggedZone != CurrentZone))
        {
            Debug.Log($"DraggableBlock '{name}': zone -> {CurrentZone} (mode={distanceMode}, distA={distA:F2}, distB={distB:F2}, connectDistance={connectDistance:F2})", this);
            lastLoggedZone = CurrentZone;
            hasLoggedZone = true;
        }
    }

    static float DistanceToAnchor(Vector3 platformPos, Vector3 anchorPos, ConnectivityDistanceMode mode, float verticalModeHorizontalTolerance)
    {
        switch (mode)
        {
            case ConnectivityDistanceMode.ThreeD:
                return Vector3.Distance(platformPos, anchorPos);

            case ConnectivityDistanceMode.Vertical:
            {
                // Elevator-like behavior: connection depends primarily on Y proximity.
                // Optional guard: only consider it if XZ is also reasonably close.
                float xz = HorizontalDistance(platformPos, anchorPos);
                if (xz > verticalModeHorizontalTolerance) return float.PositiveInfinity;
                return Mathf.Abs(platformPos.y - anchorPos.y);
            }

            case ConnectivityDistanceMode.Horizontal:
            default:
                return HorizontalDistance(platformPos, anchorPos);
        }
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public int GetClosestSideIndex(Vector3 worldPos)
    {
        if (anchorA == null || anchorB == null) return -1;

        // Use the same mode as connectivity so elevator (Vertical) works.
        float distA = DistanceToAnchor(worldPos, anchorA.position, distanceMode, verticalModeHorizontalTolerance);
        float distB = DistanceToAnchor(worldPos, anchorB.position, distanceMode, verticalModeHorizontalTolerance);

        if (float.IsPositiveInfinity(distA) && float.IsPositiveInfinity(distB)) return -1;
        return distA <= distB ? 0 : 1; // 0=A, 1=B
    }

    public Vector3 GetStandPosition()
    {
        if (standPoint != null) return standPoint.position;
        return transform.position + Vector3.up * 1.0f;
    }

    public bool TryGetBoardApproachPosition(out Vector3 approachPos)
    {
        approachPos = default;
        if (CurrentZone == PlatformZone.NearA)
        {
            if (boardApproachA == null) return false;
            approachPos = boardApproachA.position;
            return true;
        }

        if (CurrentZone == PlatformZone.NearB)
        {
            if (boardApproachB == null) return false;
            approachPos = boardApproachB.position;
            return true;
        }

        return false;
    }

    void SyncPlayerPosition(Vector3 deltaMove)
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null || !pc.IsOnPlatform) return;

        // When the player is on the platform we disable the agent and move the transform directly.
        player.transform.position += deltaMove;
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
