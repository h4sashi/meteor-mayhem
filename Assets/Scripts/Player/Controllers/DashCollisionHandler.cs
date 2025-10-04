using UnityEngine;
using Photon.Pun;
using Hanzo.Player.Abilities;
using Hanzo.Player.Core;

namespace Hanzo.Player.Controllers
{
    /// <summary>
    /// Handles collision detection during dash
    /// Applies knockback to both players and destructible objects
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class DashCollisionHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private AbilitySettings abilitySettings;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask destructibleLayer;
        [SerializeField] private float stunDuration = 2f;
        
        [Header("Detection")]
        [SerializeField] private float detectionRadius = 1.5f;
        [SerializeField] private Vector3 detectionOffset = new Vector3(0, 0.5f, 0.5f);
        
        [Header("Destructible Settings")]
        [Tooltip("Force multiplier applied to destructible objects")]
        [SerializeField] private float destructibleForceMultiplier = 1.5f;
        [Tooltip("Upward force component for destructibles (0-1)")]
        [SerializeField] private float destructibleUpwardForce = 0.6f;
        
        [Header("Effects")]
        [SerializeField] private GameObject hitVFXPrefab;
        [SerializeField] private GameObject destructibleHitVFXPrefab;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip destructibleHitSound;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        
        private PhotonView photonView;
        private PlayerAbilityController abilityController;
        private Rigidbody rb;
        private AudioSource audioSource;
        
        // Cooldown to prevent multiple hits in one dash
        private float lastPlayerHitTime = 0f;
        private float lastDestructibleHitTime = 0f;
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
                CheckForDestructibleCollisions();
            }
        }

        private void CheckForPlayerCollisions()
        {
            // Cooldown check
            if (Time.time - lastPlayerHitTime < HIT_COOLDOWN)
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
                
                // IMPORTANT: Call the RPC on the VICTIM's PhotonView
                targetPhotonView.RPC("RPC_ReceiveKnockback", RpcTarget.All, 
                    knockbackDir, knockbackForce, stunDuration, photonView.ViewID);
                
                Debug.Log($"[ATTACKER] Sent RPC_ReceiveKnockback to ViewID {targetPhotonView.ViewID}");
                
                // Spawn hit VFX
                SpawnHitEffect(hitCollider.transform.position, false);
                
                // Play hit sound
                if (hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }
                
                lastPlayerHitTime = Time.time;
                
                // Only hit one player per check
                break;
            }
        }

        private void CheckForDestructibleCollisions()
        {
            // Cooldown check
            if (Time.time - lastDestructibleHitTime < HIT_COOLDOWN)
                return;
            
            // Sphere cast in front of player
            Vector3 detectionPos = transform.position + transform.TransformDirection(detectionOffset);
            Collider[] hitColliders = Physics.OverlapSphere(detectionPos, detectionRadius, destructibleLayer);
            
            foreach (var hitCollider in hitColliders)
            {
                // Check if object has rigidbody
                Rigidbody targetRb = hitCollider.GetComponent<Rigidbody>();
                if (targetRb == null)
                {
                    targetRb = hitCollider.GetComponentInParent<Rigidbody>();
                }
                
                if (targetRb == null)
                {
                    Debug.LogWarning($"Destructible object {hitCollider.name} has no Rigidbody!");
                    continue;
                }
                
                // Calculate knockback direction
                Vector3 knockbackDir = (hitCollider.transform.position - transform.position).normalized;
                
                // Calculate force with multiplier
                float knockbackForce = abilitySettings.KnockbackForce * destructibleForceMultiplier;
                
                // Stack bonus for destructibles too
                if (abilityController.DashAbility.StackLevel >= 2)
                {
                    knockbackForce *= 1.3f;
                }
                if (abilityController.DashAbility.StackLevel >= 3)
                {
                    knockbackForce *= 1.5f;
                }
                
                // Apply force with upward component
                Vector3 forceDirection = knockbackDir;
                forceDirection.y = destructibleUpwardForce;
                forceDirection.Normalize();
                
                Vector3 force = forceDirection * knockbackForce;
                
                // Apply force locally (for local objects)
                targetRb.linearVelocity = Vector3.zero;
                targetRb.AddForce(force, ForceMode.Impulse);
                
                Debug.Log($"Dash hit destructible {hitCollider.name}! Force: {knockbackForce}");
                
                // If object has PhotonView, sync across network
                PhotonView targetPhotonView = hitCollider.GetComponentInParent<PhotonView>();
                if (targetPhotonView != null)
                {
                    // Sync destructible knockback
                    targetPhotonView.RPC("RPC_ReceiveDestructibleKnockback", RpcTarget.OthersBuffered, 
                        forceDirection, knockbackForce);
                }
                
                // Spawn destructible hit VFX
                SpawnHitEffect(hitCollider.transform.position, true);
                
                // Play destructible hit sound
                if (destructibleHitSound != null)
                {
                    audioSource.PlayOneShot(destructibleHitSound);
                }
                else if (hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }
                
                lastDestructibleHitTime = Time.time;
                
                // Can hit multiple destructibles in one check (unlike players)
                // But still track last hit time to prevent spam
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

        /// <summary>
        /// RPC for synchronizing destructible object knockback across network
        /// Attach this as a separate component to destructible objects if needed
        /// </summary>
        [PunRPC]
        private void RPC_ReceiveDestructibleKnockback(Vector3 forceDirection, float forceMagnitude)
        {
            Rigidbody targetRb = GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetRb.linearVelocity = Vector3.zero;
                targetRb.AddForce(forceDirection * forceMagnitude, ForceMode.Impulse);
                
                Debug.Log($"[Remote] Destructible received knockback force: {forceMagnitude}");
            }
        }

        private void SpawnHitEffect(Vector3 position, bool isDestructible)
        {
            GameObject vfxPrefab = isDestructible && destructibleHitVFXPrefab != null 
                ? destructibleHitVFXPrefab 
                : hitVFXPrefab;
            
            if (vfxPrefab != null)
            {
                GameObject vfx = Instantiate(vfxPrefab, position, Quaternion.identity);
                Destroy(vfx, 2f);
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            // Player detection sphere (red)
            Gizmos.color = Color.red;
            Vector3 detectionPos = transform.position + transform.TransformDirection(detectionOffset);
            Gizmos.DrawWireSphere(detectionPos, detectionRadius);
            
            // Destructible detection sphere (yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(detectionPos, detectionRadius);
        }

        private void OnDrawGizmosSelected()
        {
            // Always show when selected
            Gizmos.color = Color.cyan;
            Vector3 detectionPos = transform.position + transform.TransformDirection(detectionOffset);
            Gizmos.DrawWireSphere(detectionPos, detectionRadius);
            
            // Show detection offset
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, detectionPos);
        }
    }
}