using UnityEngine;
using Hanzo.Core.Interfaces;
using Hanzo.Player.Core;
using Hanzo.Player.Movement.States;
using Hanzo.Player.Input;
using Hanzo.Player.Abilities;
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
    [RequireComponent(typeof(PlayerAbilityController))]
    public class PlayerMovementController : MonoBehaviour, IMovementController
    {
        [Header("Settings")]
        [SerializeField] private MovementSettings movementSettings;
        [SerializeField] private PhotonView photonView;

        [Header("Camera")]
        [SerializeField] private CinemachineVirtualCamera playerCamera;
        [SerializeField] private Transform playerHead;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Components
        private Rigidbody rb;
        public Animator animator;
        private PlayerInputHandler inputHandler;
        private PlayerAbilityController abilityController;

        // States
        private IMovementState currentState;
        private IdleState idleState;
        private MovingState movingState;
        private DashingState dashingState;

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
            abilityController = GetComponent<PlayerAbilityController>();
            
            // Try to get animator if not assigned
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>();
                }
            }
            
            Debug.Log($"PlayerMovementController Awake: Animator found? {animator != null}");
            
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
                    playerCamera.Priority = 10;
                    playerCamera.enabled = true;
                    playerCamera.LookAt = playerHead;
                    playerCamera.Follow = this.transform;
                    playerCamera.transform.parent = null;
                    Debug.Log("Local player camera enabled");
                }
                else
                {
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
            
            // Initialize dashing state after ability controller is ready
            // This will be done in Start() or after ability is initialized
            currentState = idleState;
            currentState.Enter(this);
        }

        private void OnEnable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnMoveInput += HandleMoveInput;
                inputHandler.OnDashInput += HandleDashInput;
            }
        }

        private void OnDisable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnMoveInput -= HandleMoveInput;
                inputHandler.OnDashInput -= HandleDashInput;
            }
        }

        private void Update()
        {
            if (photonView.IsMine)
            {
                // Initialize dashing state if not already done
                if (dashingState == null && abilityController != null && abilityController.DashAbility != null)
                {
                    dashingState = new DashingState(abilityController.DashAbility);
                    Debug.Log("DashingState initialized in Update");
                }
                
                UpdateMovementState(); // Check state BEFORE updating current state
                currentState?.Update(this);
            }
        }

        private void UpdateMovementState()
        {
           
            // Priority 1: Check if dashing
            if (dashingState != null && abilityController != null && abilityController.DashAbility.IsActive)
            {
               
                
                if (currentState != dashingState)
                {
                    Debug.Log("CHANGING TO DASHING STATE NOW!");
                    ChangeState(dashingState);
                }
                return;
            }
            
            // Priority 2: Return to appropriate state after dash
            if (currentState == dashingState && !abilityController.DashAbility.IsActive)
            {
                // Debug.Log("Dash finished, returning to normal state");
                if (inputHandler.MoveInput.magnitude > 0.1f)
                {
                    ChangeState(movingState);
                }
                else
                {
                    ChangeState(idleState);
                }
                return;
            }
            
            // Priority 3: Normal movement state logic
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

        private void HandleDashInput()
        {
            Debug.Log($"Dash input received! Current state: {currentState?.GetType().Name}");
            
            // Only dash if not already dashing
            if (currentState != dashingState && abilityController != null)
            {
                Debug.Log($"Attempting to activate dash. DashingState null? {dashingState == null}");
                
                if (abilityController.TryActivateDash())
                {
                    Debug.Log("Dash activated successfully!");
                    
                    // Don't manually change state here - let UpdateMovementState handle it
                    // This ensures the state changes AFTER the ability is marked as active
                }
                else
                {
                    Debug.LogWarning("Dash activation failed - likely on cooldown");
                }
            }
            else
            {
                Debug.LogWarning($"Cannot dash: Already dashing? {currentState == dashingState}, AbilityController null? {abilityController == null}");
            }
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
            Debug.Log($"ChangeState called: {currentState?.GetType().Name} -> {newState?.GetType().Name}");
            
            if (currentState?.CanTransitionTo(newState) == false)
            {
                Debug.LogWarning($"State transition BLOCKED: {currentState?.GetType().Name} cannot transition to {newState?.GetType().Name}");
                return;
            }

            currentState?.Exit(this);
            currentState = newState;
            currentState?.Enter(this);
            
            Debug.Log($"State changed successfully to: {currentState?.GetType().Name}");
        }

        private void OnDrawGizmos()
        {
            if (!showDebugInfo || !Application.isPlaying) return;

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