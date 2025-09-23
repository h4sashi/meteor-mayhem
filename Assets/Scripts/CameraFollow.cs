using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [Tooltip("Optional: assign target's Rigidbody to get a cleaner velocity reading.")]
    private Rigidbody targetRigidbody;

    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 6f, -6f);
    [SerializeField] private float followSpeed = 8f; // higher = snappier

    [Header("Rotation Settings")]
    [SerializeField] private float yawSpeed = 120f;   // kept for compatibility
    [SerializeField] private float pitchSpeed = 80f;  // kept for compatibility
    [SerializeField] private float minPitch = 15f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Auto-Rotate Behind Player")]
    [SerializeField] private bool autoRotateBehindPlayer = true;
    [SerializeField] private float rotateBehindLerpSpeed = 5f;

    [Header("Look Ahead Settings")]
    [SerializeField] private bool enableLookAhead = true;
    [Tooltip("Distance ahead of player to look.")]
    [SerializeField] private float lookAheadDistance = 2f;
    [Tooltip("Seconds used by SmoothDamp for lookAhead smoothing. Larger = slower.")]
    [SerializeField] private float lookAheadSmoothTime = 0.15f;
    [Tooltip("Don't apply lookahead until player speed exceeds this (world units/sec).")]
    [SerializeField] private float minMoveThreshold = 0.1f;

    [Header("Smoothing")]
    [Tooltip("Seconds used to smooth camera rotation (exponential lerp) toward the yaw/pitch target.")]
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [Tooltip("Seconds used to smooth camera position.")]
    [SerializeField] private float positionSmoothTime = 0.125f;

    // internal state
    private float currentYaw;
    private float currentPitch = 30f;
    private Vector3 lastPlayerPos;
    private Vector3 lookAheadOffset;
    private Vector3 lookAheadVelocity;
    private Vector3 posVelocity;

    // quaternion used exactly like in your original script
    private Quaternion smoothRotation;

    void Start()
    {
        targetRigidbody = targetRigidbody ? targetRigidbody : target?.GetComponent<Rigidbody>();
        if (target) lastPlayerPos = target.position;
        smoothRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    void LateUpdate()
    {
        if (!target) return;

        float dt = Mathf.Max(Time.deltaTime, 1e-6f);

        // --- Auto rotate behind player if enabled ---
        if (autoRotateBehindPlayer)
        {
            Vector3 playerMovement = target.position - lastPlayerPos;
            if (playerMovement.sqrMagnitude > 0.001f) // if moving
            {
                float moveYaw = Mathf.Atan2(playerMovement.x, playerMovement.z) * Mathf.Rad2Deg;
                currentYaw = Mathf.LerpAngle(currentYaw, moveYaw, rotateBehindLerpSpeed * dt);
            }
        }

        // --- Rotation smoothing (reverted to original behavior) ---
        Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        // same exponential-style smoothing used in your original script
        smoothRotation = Quaternion.Lerp(smoothRotation, targetRotation, 1 - Mathf.Exp(-rotationSmoothTime * dt));

        // --- Camera position (smoothed) ---
        Vector3 desiredPosition = target.position + smoothRotation * offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref posVelocity, positionSmoothTime);

        // --- Look Ahead Calculation (keeps the improved behavior) ---
        Vector3 worldVelocity;
        if (targetRigidbody != null)
        {
            worldVelocity = targetRigidbody.linearVelocity;
        }
        else
        {
            worldVelocity = (target.position - lastPlayerPos) / dt;
        }

        Vector3 desiredLookAhead = Vector3.zero;
        if (enableLookAhead && worldVelocity.sqrMagnitude > minMoveThreshold * minMoveThreshold)
        {
            Vector3 horizontalVel = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
            if (horizontalVel.sqrMagnitude > 0.001f)
                desiredLookAhead = horizontalVel.normalized * lookAheadDistance;
        }

        // keep SmoothDamp for look-ahead to avoid jitter
        lookAheadOffset = Vector3.SmoothDamp(lookAheadOffset, desiredLookAhead, ref lookAheadVelocity, lookAheadSmoothTime);

        // --- Reverted rotation application: LookAt target + lookAhead (exactly like original) ---
        Vector3 lookTarget = target.position + Vector3.up * 1.5f + lookAheadOffset;
        transform.LookAt(lookTarget);

        // store last player pos for next frame
        lastPlayerPos = target.position;
    }

    void OnDrawGizmosSelected()
    {
        if (!target) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(target.position + Vector3.up * 1.5f + lookAheadOffset, 0.12f);
    }
}
