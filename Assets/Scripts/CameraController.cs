using System;
using System.Linq;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target; // The player (Point O)
    public float radius = 5f; // Initial Radius R
    public float heightOffset = 1.5f; // Offset to look at player's center/head

    [Header("Rotation Settings")]
    public float rotationSpeedX = 2f;
    public float rotationSpeedY = 2f;
    public float minYAngle = -20f;
    public float maxYAngle = 80f;

    [Header("Input Settings")]
    [Tooltip("When true, in the Editor Device Simulator the mouse will be treated like a touch input automatically.")]
    public bool treatMouseAsTouchInDeviceSimulator = true;
    [Tooltip("Multiplier applied to Touch.deltaPosition to make touch feel similar to mouse-based rotation.")]
    public float touchSensitivity = 0.15f;
    [Tooltip("Multiplier applied when simulating touch from mouse in the Device Simulator.")]
    public float simulatedMouseMultiplier = 1f;

    [Header("Occlusion Settings")]
    public LayerMask occlusionLayers;
    public float occlusionCheckRadius = 0.2f; // Radius for sphere cast or just clearance
    public float collisionBuffer = 0.1f; // Buffer distance from obstacle

    [Header("Initial Settings")]
    public float initialPitch = 45f; // Initial X rotation (looking down)

    private float currentYaw = 0f;
    private float currentPitch = 0f;
    private float currentDistance;

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        currentDistance = radius;
        currentPitch = initialPitch;
        currentYaw = 180f; // Facing the player initially as per doc (X=0, Y=180 relative to something, usually back)
        
        // Align camera initially
        UpdateCameraPosition();
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleInput();
        HandleOcclusion();
        UpdateCameraPosition();
    }

    void HandleInput()
    {
        // If any object requested exclusive dragging, skip camera input so object dragging has priority
        if (InputLockManager.IsDragging || DraggableBlock.IsDraggingAny) return;

        // Check if PaintingSystem is handling the input or if we are clicking a start point
        bool isSimulatorActive = IsDeviceSimulatorActive();

        if (PaintingSystem.Instance != null)
        {
            if (PaintingSystem.Instance.IsDraggingPath) return;
            bool pointerDown = Input.GetMouseButtonDown(0);
#if UNITY_EDITOR
            // If simulator is active and we want to treat mouse as touch, consider mouse down as touch begin
            if (treatMouseAsTouchInDeviceSimulator && isSimulatorActive && Input.GetMouseButtonDown(0)) pointerDown = true;
#endif
            if (pointerDown && PaintingSystem.Instance.IsMouseOverStartPoint()) return;
        }

        Vector2 delta = Vector2.zero;
        bool gotInput = false;

#if UNITY_EDITOR
        bool simulateMouseAsTouch = treatMouseAsTouchInDeviceSimulator && isSimulatorActive;
#else
        bool simulateMouseAsTouch = false;
#endif

        // Prioritize real touch input when available
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                delta = t.deltaPosition * touchSensitivity;
                gotInput = true;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (simulateMouseAsTouch)
            {
                // Treat mouse motion like touch delta (scaled)
                delta = new Vector2(mouseX, mouseY) * simulatedMouseMultiplier;
            }
            else
            {
                delta = new Vector2(mouseX, mouseY);
            }
            gotInput = true;
        }

        if (gotInput)
        {
            currentYaw += delta.x * rotationSpeedX;
            currentPitch -= delta.y * rotationSpeedY;
            currentPitch = Mathf.Clamp(currentPitch, minYAngle, maxYAngle);
        }
    }

    void HandleOcclusion()
    {
        Vector3 targetPos = GetTargetPosition();
        Vector3 dirToCamera = (transform.position - targetPos).normalized;
        
        // We calculate the desired position based on full radius
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 desiredPos = targetPos + rotation * Vector3.back * radius;
        
        RaycastHit hit;
        Vector3 direction = desiredPos - targetPos;
        float distance = direction.magnitude;

        // Raycast from Target to Camera
        if (Physics.Raycast(targetPos, direction.normalized, out hit, radius, occlusionLayers))
        {
            // If hit, set distance to hit distance minus buffer
            currentDistance = Mathf.Clamp(hit.distance - collisionBuffer, 0.5f, radius);
        }
        else
        {
            // No occlusion, return to full radius
            currentDistance = Mathf.Lerp(currentDistance, radius, Time.deltaTime * 10f); // Smooth return
        }
    }

    void UpdateCameraPosition()
    {
        Vector3 targetPos = GetTargetPosition();
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        
        // Calculate position based on current (possibly occluded) distance
        Vector3 position = targetPos + rotation * Vector3.back * currentDistance;

        transform.position = position;
        transform.LookAt(targetPos);
    }

    Vector3 GetTargetPosition()
    {
        return target.position + Vector3.up * heightOffset;
    }

    // Detect whether Unity Device Simulator window is active (Editor only). Falls back to Inspector override.
    bool IsDeviceSimulatorActive()
    {
#if UNITY_EDITOR
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type found = null;
                try { found = asm.GetTypes().FirstOrDefault(t => t.Name == "DeviceSimulatorWindow" || t.Name == "DeviceSimulationWindow" || t.Name.Contains("DeviceSimulator")); } catch { }
                if (found != null)
                {
                    var objs = UnityEngine.Resources.FindObjectsOfTypeAll(found);
                    if (objs != null && objs.Length > 0) return true;
                }
            }
        }
        catch { }
#endif
        return false;
    }
}
