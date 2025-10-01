using UnityEngine;
using Hanzo.Core.Interfaces;

namespace Hanzo.Player.Movement.States
{
    public class DashingState : IMovementState
    {
        private IAbility dashAbility;

        public DashingState(IAbility ability)
        {
            dashAbility = ability;
        }

        public void Enter(IMovementController controller)
        {
            Debug.Log("DashingState ENTERED");

            // Reduce drag during dash for smoother movement
            controller.Rigidbody.linearDamping = 0f;

            // Turn OFF RUN animation when entering dash
            if (controller.Animator != null)
            {
                controller.Animator.SetBool("RUN", false);
            }
        }

        public void Update(IMovementController controller)
        {
            // No animation toggles here — ability is authoritative.
            // Optionally monitor ability state to request transitions.
        }

        public void Exit(IMovementController controller)
        {
            Debug.Log("DashingState EXITED");

            // Restore normal drag
            controller.Rigidbody.linearDamping = 6f;

            // State should not do animation resetting; the ability already turned IsDashing off in EndDash.
        }

        public bool CanTransitionTo(IMovementState newState)
        {
            // Can transition out of dash when ability is no longer active
            return !dashAbility.IsActive && (newState is IdleState || newState is MovingState);
        }
    }
}
