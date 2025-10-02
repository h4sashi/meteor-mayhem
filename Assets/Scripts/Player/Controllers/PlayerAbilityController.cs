using UnityEngine;
using System.Collections.Generic;
using Hanzo.Core.Interfaces;
using Hanzo.VFX;
using Photon.Pun;

namespace Hanzo.Player.Abilities
{
    public class PlayerAbilityController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private AbilitySettings abilitySettings;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private IMovementController movementController;
        private PhotonView photonView;
        private List<IAbility> abilities = new List<IAbility>();
        
        // Quick access to specific abilities
        private DashAbility dashAbility;
        public DashAbility DashAbility => dashAbility;

        private void Awake()
        {
            movementController = GetComponent<IMovementController>();
            photonView = GetComponent<PhotonView>();
            
            InitializeAbilities();
        }

        private void InitializeAbilities()
        {
            // Create dash ability
            dashAbility = new DashAbility(abilitySettings);
            
            // Wire up VFX controller
            var vfx = GetComponentInChildren<DashVFXController>(true);
            if (vfx != null)
            {
                dashAbility.SetVFXController(vfx);
                Debug.Log("PlayerAbilityController: DashVFXController injected successfully.");
            }
            else
            {
                Debug.LogWarning("PlayerAbilityController: No DashVFXController found. VFX won't play.");
            }
            
            dashAbility.Initialize(movementController);
            abilities.Add(dashAbility);
        }

        private void Update()
        {
            // Only update abilities for local player
            if (!photonView.IsMine) return;

            // Update all abilities
            foreach (var ability in abilities)
            {
                ability.Update();
            }
        }

        public bool TryActivateDash()
        {
            if (!photonView.IsMine) return false;
            
            bool activated = dashAbility.TryActivate();
            
            // VFX is already handled by DashAbility.TryActivate()
            // No need to call it again here
            
            return activated;
        }
        
        /// <summary>
        /// Called when player picks up a dash power-up (GDD stacking system)
        /// </summary>
        public void AddDashStack()
        {
            if (dashAbility != null)
            {
                dashAbility.AddStack();
            }
        }
        
        /// <summary>
        /// Reset dash to base level (e.g., on respawn or round start)
        /// </summary>
        public void ResetDashStacks()
        {
            if (dashAbility != null)
            {
                dashAbility.ResetStacks();
            }
        }

        private void OnDestroy()
        {
            // Cleanup all abilities
            foreach (var ability in abilities)
            {
                ability.Cleanup();
            }
        }

        private void OnGUI()
        {
            if (!showDebugInfo || !photonView.IsMine) return;

            GUILayout.BeginArea(new Rect(10, 230, 300, 180));
            GUILayout.Label("=== ABILITIES ===");
            GUILayout.Label($"Dash Ready: {dashAbility.CanActivate}");
            GUILayout.Label($"Dash Active: {dashAbility.IsActive}");
            GUILayout.Label($"Dash Stack: {dashAbility.StackLevel}/3");
            GUILayout.Label($"Cooldown: {dashAbility.CooldownRemaining:F2}s");
            
            // Show stack effects
            string stackEffect = dashAbility.StackLevel switch
            {
                1 => "Base Dash",
                2 => "Enhanced (1.5x distance)",
                3 => "Chain Dash Ready",
                _ => "Unknown"
            };
            GUILayout.Label($"Effect: {stackEffect}");
            
            GUILayout.EndArea();
        }
    }
}