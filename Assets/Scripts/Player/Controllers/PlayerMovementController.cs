using UnityEngine;
using Hanzo.Core.Interfaces;
using Hanzo.Player.Movement.Core;
using Hanzo.Player.Movement.States;
using Hanzo.Player.Input;
using Photon.Pun;
using Cinemachine;

enum Locomotion
{
    RUN
}
namespace Hanzo.Player.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovementController : MonoBehaviour, IMovementController
    {
        [Header("Settings")]
        [SerializeField] private MovementSettings movementSettings;
        [SerializeField] private PhotonView photonView;

        [Header("Camera")]
        [SerializeField] private CinemachineVirtualCamera playerCamera; // Assign in inspector or find in children
        [SerializeField] private Transform playerHead;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Components
        private Rigidbody rb;

        public Animator animator;

        private PlayerInputHandler inputHandler;

        // States
        private IMovementState currentState;
        private IdleState idleState;
        private MovingState movingState;

        // Properties from IMovementController
        public Vector3 Position => transform.position;
        public Vector3 Velocity => rb.linearVelocity;
        public Transform Transform => transform;
        public Rigidbody Rigidbody => rb;

        public Animator Animator => animator;

        // Debug properties
        public IMovementState CurrentState => currentState;
        public float CurrentSpeed => new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            inputHandler = GetComponent<PlayerInputHandler>();
            
            // Find camera if not assigned
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            }
            
            InitializeStates();
        }

        private void Start()
        {
            photonView = GetComponent<PhotonView>();
            SetupCamera();
        }

        private void SetupCamera()
        {
            if (playerCamera != null)
            {
                if (photonView.IsMine)
                {
                    // This is the local player - enable camera
                    playerCamera.Priority = 10;
                    playerCamera.enabled = true;
                    playerCamera.LookAt = playerHead;
                    playerCamera.Follow = this.transform;
                    playerCamera.transform.parent = null; // Detach camera from player to avoid inheriting rotation
                    Debug.Log("Local player camera enabled");
                }
                else
                {
                    // This is a remote player - disable camera
                    playerCamera.Priority = 0;
                    playerCamera.enabled = false;
                    Debug.Log("Remote player camera disabled");
                }
            }
            else
            {
                Debug.LogWarning("No CinemachineVirtualCamera found for player: " + gameObject.name);
            }
        }

        private void InitializeStates()
        {
            idleState = new IdleState();
            movingState = new MovingState(movementSettings);

            // Start in idle state
            currentState = idleState;
            currentState.Enter(this);
        }

        private void OnEnable()
        {
            if (inputHandler != null)
                inputHandler.OnMoveInput += HandleMoveInput;
        }

        private void OnDisable()
        {
            if (inputHandler != null)
                inputHandler.OnMoveInput -= HandleMoveInput;
        }

        private void Update()
        {
            // Only process input and state updates for local player
            if (photonView.IsMine)
            {
                currentState?.Update(this);
                UpdateMovementState();
            }
        }

        private void UpdateMovementState()
        {
            // Determine what state we should be in based on input
            if (inputHandler.MoveInput.magnitude > 0.1f)
            {
                if (currentState != movingState)
                {
                    ChangeState(movingState);
                    if (animator != null)
                        animator.SetBool(Locomotion.RUN.ToString(), true);
                }
            }
            else if (inputHandler.MoveInput.magnitude <= 0.1f)
            {
                if (currentState != idleState)
                {
                    ChangeState(idleState);
                    if (animator != null)
                        animator.SetBool(Locomotion.RUN.ToString(), false);
                }
            }
        }

        private void HandleMoveInput(Vector2 moveInput)
        {
            if (movingState != null)
                movingState.SetMoveInput(moveInput);
        }

        // IMovementController implementation
        public void SetVelocity(Vector3 velocity)
        {
            rb.linearVelocity = velocity;
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            rb.AddForce(force, mode);
        }

        public void ChangeState(IMovementState newState)
        {
            if (currentState?.CanTransitionTo(newState) == false)
                return;

            currentState?.Exit(this);
            currentState = newState;
            currentState?.Enter(this);
        }

        private void OnDrawGizmos()
        {
            if (!showDebugInfo || !Application.isPlaying) return;

            // Draw velocity vector
            Gizmos.color = Color.green;
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Gizmos.DrawRay(transform.position + Vector3.up, horizontalVelocity);
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Current State: {currentState?.GetType().Name}");
            GUILayout.Label($"Speed: {CurrentSpeed:F2} m/s");
            GUILayout.Label($"Input: {inputHandler?.MoveInput}");
            GUILayout.Label($"Velocity: {rb.linearVelocity}");
            GUILayout.Label($"Is Mine: {photonView.IsMine}");
            GUILayout.Label($"Camera Active: {(playerCamera != null ? playerCamera.enabled.ToString() : "No Camera")}");
            GUILayout.EndArea();
        }
    }
}