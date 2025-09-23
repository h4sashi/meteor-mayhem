using UnityEngine;
using Hanzo.Core.Interfaces;
using Hanzo.Player.Movement.Core;
using Hanzo.Player.Movement.States;
using Hanzo.Player.Input;

namespace Hanzo.Player.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovementController : MonoBehaviour, IMovementController
    {
        [Header("Settings")]
        [SerializeField] private MovementSettings movementSettings;
        
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        // Components
        private Rigidbody rb;
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
        
        // Debug properties
        public IMovementState CurrentState => currentState;
        public float CurrentSpeed => new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            inputHandler = GetComponent<PlayerInputHandler>();
            
            // Find camera if not assigned
            if (!cameraTransform)
            {
                cameraTransform = Camera.main?.transform;
            }
            
            InitializeStates();
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
            inputHandler.OnMoveInput += HandleMoveInput;
        }
        
        private void OnDisable()
        {
            inputHandler.OnMoveInput -= HandleMoveInput;
        }
        
        private void Update()
        {
            currentState?.Update(this);
            UpdateMovementState();
        }
        
        
        private void UpdateMovementState()
        {
            // Determine what state we should be in based on input
            if (inputHandler.MoveInput.magnitude > 0.1f)
            {
                if (currentState != movingState)
                {
                    ChangeState(movingState);
                }
            }
            else if (inputHandler.MoveInput.magnitude <= 0.1f)
            {
                if (currentState != idleState)
                {
                    ChangeState(idleState);
                }
            }
        }
        
        private void HandleMoveInput(Vector2 moveInput)
        {
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
            
            // Draw input direction
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label($"Current State: {currentState?.GetType().Name}");
            GUILayout.Label($"Speed: {CurrentSpeed:F2} m/s");
            GUILayout.Label($"Input: {inputHandler.MoveInput}");
            GUILayout.Label($"Velocity: {rb.linearVelocity}");
            GUILayout.EndArea();
        }
    }
}
