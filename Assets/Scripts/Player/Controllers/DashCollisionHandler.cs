using UnityEngine;
using Photon.Pun;
using Hanzo.Player.Abilities;
using Hanzo.Player.Core;

namespace Hanzo.Player.Controllers
{
    /// <summary>
    /// Handles collision detection during dash and applies knockback to hit players
    /// Place this in Scripts/Player/Controllers/
    /// Attach to player prefab alongside PlayerMovementController
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class DashCollisionHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private AbilitySettings abilitySettings;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float stunDuration = 2f;
        
        [Header("Detection")]
        [SerializeField] private float detectionRadius = 1.5f;
        [SerializeField] private Vector3 detectionOffset = new Vector3(0, 0.5f, 0.5f);
        
        [Header("Effects")]
        [SerializeField] private GameObject hitVFXPrefab;
        [SerializeField] private AudioClip hitSound;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        
        private PhotonView photonView;
        private PlayerAbilityController abilityController;
        private Rigidbody rb;
        private AudioSource audioSource;
        
        // Cooldown to prevent multiple hits in one dash
        private float lastHitTime = 0f;
        private const float HIT_COOLDOWN = 0.2f;

        private void Awake()
        {
            photonView = GetComponent<PhotonView>();
            abilityController = GetComponent<PlayerAbilityController>();
            rb = GetComponent<Rigidbody>();
            
            // Setup audio source
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }

        private void FixedUpdate()
        {
            if (!photonView.IsMine) return;
            
            // Only check collisions while dashing
            if (abilityController != null && abilityController.DashAbility.IsActive)
            {
                CheckForPlayerCollisions();
            }
        }

        private void CheckForPlayerCollisions()
        {
            // Cooldown check
            if (Time.time - lastHitTime < HIT_COOLDOWN)
                return;
            
            // Sphere cast in front of player
            Vector3 detectionPos = transform.position + transform.TransformDirection(detectionOffset);
            Collider[] hitColliders = Physics.OverlapSphere(detectionPos, detectionRadius, playerLayer);
            
            foreach (var hitCollider in hitColliders)
            {
                // Skip self
                if (hitCollider.transform.root == transform.root)
                    continue;
                
                // Get hit player's PhotonView
                PhotonView targetPhotonView = hitCollider.GetComponentInParent<PhotonView>();
                if (targetPhotonView == null || targetPhotonView == photonView)
                    continue;
                
                // Get hit player's state controller
                PlayerStateController targetState = hitCollider.GetComponentInParent<PlayerStateController>();
                if (targetState == null)
                {
                    Debug.LogWarning($"Player {hitCollider.name} hit but has no PlayerStateController!");
                    continue;
                }
                
                // Don't hit already stunned players
                if (targetState.IsStunned)
                    continue;
                
                // Calculate knockback direction
                Vector3 knockbackDir = (hitCollider.transform.position - transform.position).normalized;
                
                // Apply knockback via RPC
                float knockbackForce = abilitySettings.KnockbackForce;
                
                // Stack bonus: higher stacks = more knockback
                if (abilityController.DashAbility.StackLevel >= 2)
                {
                    knockbackForce *= 1.3f;
                }
                if (abilityController.DashAbility.StackLevel >= 3)
                {
                    knockbackForce *= 1.5f;
                }
                
                Debug.Log($"Dash hit player {targetPhotonView.Owner.NickName}! Knockback: {knockbackForce}");
                
                // IMPORTANT: Call the RPC on the VICTIM's PhotonView, not ours
                // This ensures the victim's client handles the physics
                targetPhotonView.RPC("RPC_ReceiveKnockback", RpcTarget.All, 
                    knockbackDir, knockbackForce, stunDuration, photonView.ViewID);
                
                Debug.Log($"[ATTACKER] Sent RPC_ReceiveKnockback to ViewID {targetPhotonView.ViewID}");
                
                // Spawn hit VFX
                SpawnHitEffect(hitCollider.transform.position);
                
                // Play hit sound
                if (hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }
                
                lastHitTime = Time.time;
                
                // Only hit one player per check
                break;
            }
        }

        /// <summary>
        /// RPC called on victim to receive knockback
        /// </summary>
        [PunRPC]
        private void RPC_ReceiveKnockback(Vector3 direction, float force, float stunTime, int attackerViewID)
        {
            PlayerStateController stateController = GetComponent<PlayerStateController>();
            if (stateController != null)
            {
                stateController.ApplyKnockbackAndStun(direction, force, stunTime);
                
                Debug.Log($"[Victim] Received knockback from ViewID {attackerViewID}");
            }
        }

        private void SpawnHitEffect(Vector3 position)
        {
            if (hitVFXPrefab != null)
            {
                GameObject vfx = Instantiate(hitVFXPrefab, position, Quaternion.identity);
                Destroy(vfx, 2f);
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            Gizmos.color = Color.red;
            Vector3 detectionPos = transform.position + transform.TransformDirection(detectionOffset);
            Gizmos.DrawWireSphere(detectionPos, detectionRadius);
        }

        private void OnDrawGizmosSelected()
        {
            // Always show when selected
            Gizmos.color = Color.yellow;
            Vector3 detectionPos = transform.position + transform.TransformDirection(detectionOffset);
            Gizmos.DrawWireSphere(detectionPos, detectionRadius);
        }
    }
}