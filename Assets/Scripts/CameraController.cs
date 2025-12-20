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
        if (Input.GetMouseButton(0)) // Dragging screen (assuming Left Mouse Button for now, or Touch)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            currentYaw += mouseX * rotationSpeedX;
            currentPitch -= mouseY * rotationSpeedY;
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
}
