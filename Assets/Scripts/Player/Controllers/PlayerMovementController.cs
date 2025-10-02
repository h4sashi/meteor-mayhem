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
    [RequireComponent(typeof(PlayerStateController))]
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
        private PlayerStateController stateController;

        // States
        private IMovementState currentState;
        private IdleState idleState;
        private MovingState movingState;
        private DashingState dashingState;
        private StunnedState stunnedState;

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
            stateController = GetComponent<PlayerStateController>();
            
            if (stateController == null)
            {
                Debug.LogError("PlayerMovementController: PlayerStateController component missing! Add it to player prefab.");
            }
            
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
            
            // Subscribe to stun events to trigger state transitions
            if (stateController != null)
            {
                stateController.OnStunStarted += HandleStunStarted;
                stateController.OnStunEnded += HandleStunEnded;
            }
            
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
            stunnedState = new StunnedState(stateController);
            
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
            
            // Unsubscribe from stun events
            if (stateController != null)
            {
                stateController.OnStunStarted -= HandleStunStarted;
                stateController.OnStunEnded -= HandleStunEnded;
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
                
                // PRIORITY 1: Check if stunned (highest priority)
                // Only transition TO stunned state if not already in it
                if (stateController != null && stateController.IsStunned)
                {
                    // Only change state if we're NOT already in stunned state
                    if (currentState != stunnedState)
                    {
                        Debug.Log("Player is stunned, forcing transition to StunnedState");
                        ChangeState(stunnedState);
                    }
                    
                    // Update stunned state (but don't re-enter)
                    currentState?.Update(this);
                    return; // Don't process other state logic while stunned
                }
                
                // PRIORITY 2: Normal state updates
                UpdateMovementState();
                currentState?.Update(this);
            }
        }

        private void UpdateMovementState()
        {
            // Don't update movement state if stunned (handled in Update())
            if (stateController != null && stateController.IsStunned)
                return;
            
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
        
        /// <summary>
        /// Called when player gets stunned - forces transition to StunnedState
        /// </summary>
        private void HandleStunStarted()
        {
            Debug.Log("HandleStunStarted: Transitioning to StunnedState");
            ChangeState(stunnedState);
        }
        
        /// <summary>
        /// Called when stun ends - returns to appropriate movement state
        /// </summary>
        private void HandleStunEnded()
        {
            Debug.Log("HandleStunEnded: Transitioning from StunnedState");
            
            // Check if player is holding movement input
            if (inputHandler != null && inputHandler.MoveInput.magnitude > 0.1f)
            {
                ChangeState(movingState);
            }
            else
            {
                ChangeState(idleState);
            }
        }

        private void HandleMoveInput(Vector2 moveInput)
        {
            // Block input while stunned
            if (stateController != null && stateController.IsStunned)
                return;
            
            if (movingState != null)
                movingState.SetMoveInput(moveInput);
        }

        private void HandleDashInput()
        {
            // Block dashing while stunned
            if (stateController != null && stateController.IsStunned)
            {
                Debug.Log("Cannot dash: Player is stunned!");
                return;
            }
            
            Debug.Log($"Dash input received! Current state: {currentState?.GetType().Name}");
            
            // Only dash if not already dashing
            if (currentState != dashingState && abilityController != null)
            {
                Debug.Log($"Attempting to activate dash. DashingState null? {dashingState == null}");
                
                if (abilityController.TryActivateDash())
                {
                    Debug.Log("Dash activated successfully!");
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
            // Don't allow manual velocity changes while stunned (knockback uses AddForce)
            if (stateController != null && stateController.IsStunned)
                return;
            
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

            GUILayout.BeginArea(new Rect(10, 10, 300, 250));
            GUILayout.Label($"Current State: {currentState?.GetType().Name}");
            GUILayout.Label($"Speed: {CurrentSpeed:F2} m/s");
            GUILayout.Label($"Input: {inputHandler?.MoveInput}");
            GUILayout.Label($"Velocity: {rb.linearVelocity}");
            GUILayout.Label($"Is Mine: {photonView.IsMine}");
            GUILayout.Label($"Camera Active: {(playerCamera != null ? playerCamera.enabled.ToString() : "No Camera")}");
            
            // Show stun state
            if (stateController != null)
            {
                GUILayout.Label($"Stunned: {stateController.IsStunned}");
                if (stateController.IsStunned)
                {
                    GUILayout.Label($"Stun Time: {stateController.StunTimeRemaining:F2}s");
                }
            }
            
            GUILayout.EndArea();
        }
    }
}