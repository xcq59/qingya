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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalMaterial = rend.material;

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
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, clickRaycastDistance))
        {
            // 1. Check if clicked on Player
            if (hit.collider.gameObject == gameObject)
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

            // 2. If in Movement state, check for movement target
            if (GameManager.Instance.CurrentState == GameManager.GameState.Movement)
            {
                // Check if clicked on valid ground
                // We assume "Ground" or "Path" has a specific layer or tag, or just NavMesh walkable.
                // For now, we check if the point is on NavMesh.
                
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(hit.point, out navHit, 1.0f, NavMesh.AllAreas))
                {
                    // Check path validity
                    NavMeshPath path = new NavMeshPath();
                    agent.CalculatePath(navHit.position, path);

                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        agent.SetDestination(navHit.position);
                        // Keep highlight
                    }
                    else
                    {
                        Debug.Log("路径不可到达，原因：目标路径未绘制");
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
            // Clicked on nothing (skybox etc)
            if (GameManager.Instance.CurrentState == GameManager.GameState.Movement)
            {
                GameManager.Instance.SetState(GameManager.GameState.Normal);
            }
        }
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
