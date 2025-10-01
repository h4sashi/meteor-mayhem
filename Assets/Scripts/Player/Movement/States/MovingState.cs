using UnityEngine;
using Hanzo.Core.Interfaces;
using Hanzo.Player.Core;

namespace Hanzo.Player.Movement.States
{
     public class MovingState : IMovementState
    {
        private MovementSettings settings;
        private Vector2 moveInput;
        private Vector2 smoothedInput;
        private Vector2 inputVelocity;
        
        public MovingState(MovementSettings movementSettings)
        {
            settings = movementSettings;
        }
        
        public void SetMoveInput(Vector2 input)
        {
            moveInput = input;
        }

        public void Enter(IMovementController controller)
        {
            controller.Rigidbody.linearDamping = settings.GroundDrag;
        }
        
        public void Update(IMovementController controller)
        {
            // Smooth input for better feel
            smoothedInput = Vector2.SmoothDamp(smoothedInput, moveInput, ref inputVelocity, settings.InputSmoothing);
            
            if (smoothedInput.magnitude < 0.1f) return;
            
            // Calculate movement direction in world space
            // X input = left/right, Y input = forward/back
            Vector3 moveDirection = new Vector3(smoothedInput.x, 0, smoothedInput.y).normalized;
            
            // Apply movement force
            Vector3 targetVelocity = moveDirection * settings.MoveSpeed;
            Vector3 currentVelocity = new Vector3(controller.Velocity.x, 0, controller.Velocity.z);
            Vector3 velocityDiff = targetVelocity - currentVelocity;
            
            // Apply acceleration force
            Vector3 force = velocityDiff * settings.Acceleration;
            controller.AddForce(force, ForceMode.Acceleration);
            
            // Rotate towards movement direction (camera will follow this rotation)
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                controller.Transform.rotation = Quaternion.RotateTowards(
                    controller.Transform.rotation, 
                    targetRotation, 
                    settings.RotationSpeed * Time.deltaTime
                );
            }
        }
        
        
        public void Exit(IMovementController controller)
        {
            // Reset input smoothing
            smoothedInput = Vector2.zero;
            inputVelocity = Vector2.zero;
        }
        
        public bool CanTransitionTo(IMovementState newState)
        {
            // Can transition to idle from moving
            return newState is IdleState;
        }
    }
}