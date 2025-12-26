using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    public Material highlightMaterial; // Assign a material with outline or emission
    private Material originalMaterial;
    private Renderer rend;
    private NavMeshAgent agent;

    [Header("Settings")]
    public LayerMask groundLayer;
    public float clickRaycastDistance = 100f;

    [Header("Platform (A/B/P rules)")]
    public DraggableBlock platform;
    public float boardHorizontalDistance = 1.5f;
    public float boardVerticalMin = -0.5f;
    public float boardVerticalMax = 2.0f;
    public float boardApproachSampleRadius = 3.0f;
    public float boardArriveEpsilon = 0.15f;

    [Header("Platform Click vs Drag")]
    [Tooltip("If the mouse moves more than this many pixels between down/up, treat it as a drag attempt (do not board platform on click-up).")]
    public float platformClickMaxPixelMove = 10f;

    public bool IsOnPlatform => isOnPlatform;
    private bool isOnPlatform = false;

    private bool pendingBoard = false;
    private Vector3 pendingBoardStandPos;
    private DraggableBlock.PlatformZone pendingBoardZone;

    private DraggableBlock pendingPlatformClick;
    private Vector2 pendingPlatformClickScreenPos;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalMaterial = rend.material;

        // Ensure agent is on a valid NavMesh; otherwise Unity can log:
        // "Failed to create agent because there is no valid NavMesh"
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            NavMeshHit startHit;
            if (NavMesh.SamplePosition(transform.position, out startHit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(startHit.position);
            }
            else
            {
                Debug.LogWarning("NavMeshAgent is not on any NavMesh at Start(). Bake NavMesh for ground A/B or move the player onto NavMesh.", this);
            }
        }

        GameManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnGameStateChanged;
    }

    void OnGameStateChanged(GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.Movement)
        {
            SetHighlight(true);
        }
        else
        {
            SetHighlight(false);
        }
    }

    void Update()
    {
        if (pendingBoard)
        {
            TryCompleteBoarding();
        }

        if (Input.GetMouseButtonDown(0))
        {
            // If the pointer is on a platform, let DraggableBlock own the interaction.
            // We defer "board" to mouse-up so click vs drag can be distinguished reliably.
            DraggableBlock clickedPlatform;
            if (TryGetPlatformUnderPointer(out clickedPlatform))
            {
                pendingPlatformClick = clickedPlatform;
                pendingPlatformClickScreenPos = Input.mousePosition;
                return;
            }

            if (DraggableBlock.IsDraggingAny) return;
            HandleClick();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (pendingPlatformClick != null)
            {
                float pixelMove = Vector2.Distance(pendingPlatformClickScreenPos, (Vector2)Input.mousePosition);
                bool treatAsDrag = DraggableBlock.IsDraggingAny || pixelMove > platformClickMaxPixelMove;

                DraggableBlock clicked = pendingPlatformClick;
                pendingPlatformClick = null;

                if (!treatAsDrag && GameManager.Instance.CurrentState == GameManager.GameState.Movement && !isOnPlatform)
                {
                    AttemptBoardPlatform(clicked);
                }
            }
        }
    }

    bool TryGetPlatformUnderPointer(out DraggableBlock clickedPlatform)
    {
        clickedPlatform = null;

        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float rayDistance = Mathf.Max(clickRaycastDistance, 500f);

        // Use RaycastAll so an unrelated trigger/volume in front won't block platform detection.
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        float bestDist = float.PositiveInfinity;
        DraggableBlock best = null;
        for (int i = 0; i < hits.Length; i++)
        {
            DraggableBlock b = hits[i].collider != null ? hits[i].collider.GetComponentInParent<DraggableBlock>() : null;
            if (b == null) continue;

            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                best = b;
            }
        }

        if (best == null) return false;
        clickedPlatform = best;
        return true;
    }

    void TryCompleteBoarding()
    {
        if (isOnPlatform || platform == null)
        {
            pendingBoard = false;
            return;
        }

        // If platform moved away while we were walking, cancel.
        if (platform.CurrentZone == DraggableBlock.PlatformZone.Middle || platform.CurrentZone != pendingBoardZone)
        {
            pendingBoard = false;
            if (agent != null && agent.enabled) agent.ResetPath();
            return;
        }

        if (agent == null || !agent.enabled)
        {
            pendingBoard = false;
            return;
        }

        if (agent.pathPending) return;
        if (agent.pathStatus != NavMeshPathStatus.PathComplete) return;
        if (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.01f) + boardArriveEpsilon) return;

        agent.ResetPath();
        agent.enabled = false;
        transform.position = pendingBoardStandPos;
        isOnPlatform = true;
        pendingBoard = false;
    }

    void HandleClick()
    {
        if (Camera.main == null)
        {
            Debug.LogError("Main Camera not found! Ensure your camera is tagged 'MainCamera'.");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Player interactions often involve far objects (e.g., elevated platforms). Keep this reasonably large.
        float rayDistance = Mathf.Max(clickRaycastDistance, 500f);

        // 0) Platform input is arbitrated in Update() (down/up) so click vs drag is stable.
        // Keep HandleClick focused on player/ground/path logic.

        if (Physics.Raycast(ray, out hit, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // 1. Check if clicked on Player (support child colliders)
            PlayerController clickedPlayer = hit.collider.GetComponentInParent<PlayerController>();
            if (clickedPlayer == this)
            {
                if (GameManager.Instance.CurrentState == GameManager.GameState.Normal)
                {
                    GameManager.Instance.SetState(GameManager.GameState.Movement);
                }
                else if (GameManager.Instance.CurrentState == GameManager.GameState.Movement)
                {
                    // Already selected, maybe deselect? Or do nothing.
                    // Doc says: "Highlight state continues until active cancellation or condition met"
                }
                return;
            }

            // 1.5 Platform interaction: if player is on platform, only allow disembark to the connected side
            if (GameManager.Instance.CurrentState == GameManager.GameState.Movement && isOnPlatform)
            {
                if (platform == null)
                {
                    Debug.Log("Player is on platform, but platform reference is not set.");
                    return;
                }

                if (platform.CurrentZone == DraggableBlock.PlatformZone.Middle)
                {
                    Debug.Log("Platform is in the middle: cannot disembark.");
                    return;
                }

                // Disembark to the connected side using the platform's approach point (guaranteed to be on that side's NavMesh).
                Vector3 approachTarget;
                if (!platform.TryGetBoardApproachPosition(out approachTarget))
                {
                    Debug.Log("Platform disembark requires boardApproachA/boardApproachB to be set (on NavMesh).", platform);
                    return;
                }

                Vector3 approachBase = approachTarget;
                RaycastHit groundHit;
                int groundMask = (groundLayer.value == 0) ? Physics.DefaultRaycastLayers : groundLayer.value;
                if (Physics.Raycast(approachTarget + Vector3.up * 2.0f, Vector3.down, out groundHit, 1000f, groundMask))
                {
                    approachBase = groundHit.point;
                }

                NavMeshHit disembarkHit;
                if (!NavMesh.SamplePosition(approachBase, out disembarkHit, boardApproachSampleRadius, NavMesh.AllAreas))
                {
                    Debug.Log($"No reachable NavMesh point near disembark approach point. approachTarget={approachTarget}, approachBase={approachBase}, sampleRadius={boardApproachSampleRadius:F2}", platform);
                    return;
                }

                // Re-enable agent and place the player back onto navmesh.
                agent.enabled = true;
                agent.Warp(disembarkHit.position);
                isOnPlatform = false;

                // Optional: if the user clicked a valid navmesh point on the same side, continue walking there.
                NavMeshHit clickedNav;
                if (NavMesh.SamplePosition(hit.point, out clickedNav, 2.0f, NavMesh.AllAreas))
                {
                    NavMeshPath cont = new NavMeshPath();
                    agent.CalculatePath(clickedNav.position, cont);
                    if (cont.status == NavMeshPathStatus.PathComplete)
                    {
                        agent.SetDestination(clickedNav.position);
                    }
                }
                return;
            }

            // 2. If in Movement state, check for movement target
            if (GameManager.Instance.CurrentState == GameManager.GameState.Movement)
            {
                // Check if clicked on valid ground
                // We assume "Ground" or "Path" has a specific layer or tag, or just NavMesh walkable.
                // For now, we check if the point is on NavMesh.
                
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(hit.point, out navHit, 1.0f, NavMesh.AllAreas))
                {
                    // Check if the target is on an unpainted path
                    PaintedPath path = hit.collider.GetComponentInParent<PaintedPath>();
                    if (path != null && !path.isPainted)
                    {
                        Debug.Log("Cannot move: Path is not painted yet.");
                        return;
                    }

                    // Check path validity
                    NavMeshPath navPath = new NavMeshPath();
                    agent.CalculatePath(navHit.position, navPath);

                    if (navPath.status == NavMeshPathStatus.PathComplete)
                    {
                        agent.SetDestination(navHit.position);
                        // Keep highlight
                    }
                    else
                    {
                        Debug.Log("路径不可到达，原因：目标路径未绘制或不可达");
                        // Keep highlight, do not move
                    }
                }
                else
                {
                    // Clicked on invalid area (not on NavMesh)
                    // Doc: "Click invalid area -> Cancel highlight"
                    GameManager.Instance.SetState(GameManager.GameState.Normal);
                }
            }
        }
        else
        {
            // Clicked on nothing (skybox etc). Do not auto-cancel Movement here; it breaks long-distance interactions.
        }
    }

    void AttemptBoardPlatform(DraggableBlock targetPlatform)
    {
        if (targetPlatform == null) return;

        if (targetPlatform.CurrentZone == DraggableBlock.PlatformZone.Middle)
        {
            Debug.Log($"Platform is in the middle: cannot board. distA={targetPlatform.LastDistToA:F2}, distB={targetPlatform.LastDistToB:F2}, connectDistance={targetPlatform.connectDistance:F2}", targetPlatform);
            return;
        }

        Vector3 standPos = targetPlatform.GetStandPosition();

        // If already close enough, board instantly
        Vector3 playerPos = transform.position;
        Vector3 platPos = targetPlatform.transform.position;
        float hDist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(platPos.x, platPos.z));
        float vDist = playerPos.y - platPos.y;
        if (hDist <= boardHorizontalDistance && vDist >= boardVerticalMin && vDist <= boardVerticalMax)
        {
            agent.ResetPath();
            agent.enabled = false;
            transform.position = standPos;
            isOnPlatform = true;
            pendingBoard = false;
            platform = targetPlatform;
            return;
        }

        // Otherwise: move to the platform's approach point (must be on NavMesh), then auto-board
        Vector3 approachTarget;
        if (!targetPlatform.TryGetBoardApproachPosition(out approachTarget))
        {
            Debug.Log("Platform boarding requires boardApproachA/boardApproachB to be set (on NavMesh).", targetPlatform);
            return;
        }

        // If the approach marker is above the ground (e.g., platform moves up/down), raycast down to find ground.
        Vector3 approachBase = approachTarget;
        RaycastHit groundHit;
        int groundMask = (groundLayer.value == 0) ? Physics.DefaultRaycastLayers : groundLayer.value;
        if (Physics.Raycast(approachTarget + Vector3.up * 2.0f, Vector3.down, out groundHit, 1000f, groundMask))
        {
            approachBase = groundHit.point;
        }

        NavMeshHit approach;
        if (!NavMesh.SamplePosition(approachBase, out approach, boardApproachSampleRadius, NavMesh.AllAreas))
        {
            Debug.Log($"No reachable NavMesh point near platform approach point. " +
                      $"approachTarget={approachTarget}, approachBase={approachBase}, sampleRadius={boardApproachSampleRadius:F2}. " +
                      $"(Tip: place boardApproachA/B directly on the baked NavMesh, or increase boardApproachSampleRadius.)", targetPlatform);
            return;
        }

        // Critical rule enforcement: only allow boarding if the player can actually reach the connected side.
        // This replaces heuristic side detection and works for elevator (Vertical) platforms.
        if (agent == null || !agent.enabled)
        {
            Debug.Log("Cannot board: NavMeshAgent is disabled.", this);
            return;
        }
        if (!agent.isOnNavMesh)
        {
            Debug.Log("Cannot board: NavMeshAgent is not on NavMesh.", this);
            return;
        }

        NavMeshPath reachCheck = new NavMeshPath();
        agent.CalculatePath(approach.position, reachCheck);
        if (reachCheck.status != NavMeshPathStatus.PathComplete)
        {
            Debug.Log("Cannot board: approach point is unreachable from your current side (A/B not connected).", targetPlatform);
            return;
        }

        NavMeshPath approachPath = new NavMeshPath();
        agent.CalculatePath(approach.position, approachPath);
        if (approachPath.status != NavMeshPathStatus.PathComplete)
        {
            Debug.Log("Cannot reach platform approach point from current position.", targetPlatform);
            return;
        }

        pendingBoardStandPos = standPos;
        pendingBoardZone = targetPlatform.CurrentZone;
        pendingBoard = true;
        platform = targetPlatform;
        agent.SetDestination(approach.position);
    }

    void SetHighlight(bool active)
    {
        if (rend == null) return;

        if (active && highlightMaterial != null)
        {
            rend.material = highlightMaterial;
        }
        else
        {
            rend.material = originalMaterial;
        }
    }
}
