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

            // Try to find a DashVFXController on the player prefab (explicit wiring)
            var vfx = GetComponentInChildren<Hanzo.VFX.DashVFXController>(true);
            if (vfx != null)
            {
                dashAbility.SetVFXController(vfx);
                Debug.Log("PlayerAbilityController: Injected DashVFXController into DashAbility.");
            }
            else
            {
                Debug.LogWarning("PlayerAbilityController: No DashVFXController found on player. VFX will be auto-searched at init or not play.");
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
            return dashAbility.TryActivate();
            GetComponent<DashVFXController>().Play();
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

            GUILayout.BeginArea(new Rect(10, 230, 300, 150));
            GUILayout.Label("=== ABILITIES ===");
            GUILayout.Label($"Dash Ready: {dashAbility.CanActivate}");
            GUILayout.Label($"Dash Active: {dashAbility.IsActive}");
            GUILayout.Label($"Cooldown: {dashAbility.CooldownRemaining:F2}s");
            GUILayout.EndArea();
        }
    }

}