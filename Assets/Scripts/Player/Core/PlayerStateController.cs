using UnityEngine;
using Photon.Pun;
using System.Collections;

namespace Hanzo.Player.Core
{
    /// <summary>
    /// Manages player state including stun, knockback, and recovery
    /// Place this in Scripts/Player/Core/
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PlayerStateController : MonoBehaviour
    {
        [Header("State Settings")]
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private float knockbackDrag = 8f; // Higher = stops faster
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject stunVFXPrefab;
        [SerializeField] private Color stunTintColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        // State
        private bool isStunned = false;
        private float stunTimer = 0f;
        private Coroutine stunCoroutine;
        
        // Components
        private PhotonView photonView;
        private Rigidbody rb;
        private Animator animator;
        private Renderer[] playerRenderers;
        private Color[] originalColors;
        private GameObject activeStunVFX;
        
        // Animation parameter hashes
        private static readonly int StunnedHash = Animator.StringToHash("STUNNED");
        private static readonly int GetUpHash = Animator.StringToHash("GETUP");
        
        // Properties
        public bool IsStunned => isStunned;
        public float StunTimeRemaining => stunTimer;
        
        // Events
        public event System.Action OnStunStarted;
        public event System.Action OnStunEnded;
        public event System.Action<Vector3, float> OnKnockbackReceived;

        private void Awake()
        {
            photonView = GetComponent<PhotonView>();
            rb = GetComponent<Rigidbody>();
            animator = GetComponentInChildren<Animator>(true);
            
            if (animator == null)
            {
                Debug.LogWarning("PlayerStateController: No Animator found. Stun animations will not play.");
            }
            
            // Cache all renderers for color tinting
            playerRenderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[playerRenderers.Length];
            
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i].material.HasProperty("_BaseColor"))
                {
                    originalColors[i] = playerRenderers[i].material.GetColor("_BaseColor");
                }
            }
        }

        /// <summary>
        /// Apply knockback and stun to this player (called via RPC)
        /// </summary>
        public void ApplyKnockbackAndStun(Vector3 knockbackDirection, float knockbackForce, float duration)
        {
            if (!photonView.IsMine) return;
            
            Debug.Log($"[LOCAL] ApplyKnockbackAndStun called - Direction: {knockbackDirection}, Force: {knockbackForce}");
            
            // Apply knockback force - CRITICAL: Use Impulse for immediate effect
            if (rb != null)
            {
                // Stop current movement first
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                // Calculate knockback vector
                Vector3 knockbackVelocity = knockbackDirection.normalized * knockbackForce;
                knockbackVelocity.y = knockbackForce * 0.4f; // Add upward component
                
                // Apply directly to velocity for instant effect
                rb.linearVelocity = knockbackVelocity;
                
                Debug.Log($"[PHYSICS] Knockback velocity set to: {rb.linearVelocity}");
                Debug.Log($"[PHYSICS] Rigidbody isKinematic: {rb.isKinematic}, mass: {rb.mass}");
            }
            else
            {
                Debug.LogError("[PHYSICS] Rigidbody is null! Cannot apply knockback.");
            }
            
            // Apply stun
            StartStun(duration);
            
            // Notify listeners
            OnKnockbackReceived?.Invoke(knockbackDirection, knockbackForce);
            
            // Sync to other clients
            photonView.RPC("RPC_SyncStunState", RpcTarget.OthersBuffered, true, duration);
        }

        private void StartStun(float duration)
        {
            // Prevent re-entry if already stunned
            if (isStunned)
            {
                Debug.LogWarning("StartStun called but player is already stunned. Ignoring.");
                return;
            }
            
            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
            }
            
            stunCoroutine = StartCoroutine(StunCoroutine(duration));
        }

        private IEnumerator StunCoroutine(float duration)
        {
            isStunned = true;
            stunTimer = duration;
            
            Debug.Log($"Player stunned for {duration}s");
            
            // SET STUNNED ANIMATION
            if (animator != null)
            {
                animator.SetBool(StunnedHash, true);
                Debug.Log("✅ STUNNED animation parameter set to TRUE");
            }
            
            // Apply visual effects
            ApplyStunVisuals();
            
            // Temporarily increase drag to slow down faster after knockback
            float originalDrag = rb.linearDamping;
            rb.linearDamping = knockbackDrag;
            
            OnStunStarted?.Invoke();
            
            // Wait for stun duration
            while (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                yield return null;
            }
            
            // START RECOVERY - Trigger Get Up animation
            Debug.Log("Stun ended, starting Get Up animation");
            
            if (animator != null)
            {
                animator.SetBool(StunnedHash, false);
                animator.SetBool(GetUpHash, true);
                Debug.Log("✅ GETUP animation parameter set to TRUE");
            }
            
            // Wait for Get Up animation to complete
            // You can adjust this duration based on your Get Up animation length
            float getUpDuration = GetAnimationLength("Get Up"); // We'll implement this helper
            
            if (getUpDuration <= 0)
            {
                getUpDuration = 0.8f; // Fallback duration
                Debug.LogWarning("Could not determine Get Up animation length, using fallback: 0.8s");
            }
            
            yield return new WaitForSeconds(getUpDuration);
            
            // RECOVERY COMPLETE
            isStunned = false;
            stunTimer = 0f;
            rb.linearDamping = originalDrag;
            
            // Turn off Get Up animation
            if (animator != null)
            {
                animator.SetBool(GetUpHash, false);
                Debug.Log("✅ GETUP animation parameter set to FALSE");
            }
            
            RemoveStunVisuals();
            OnStunEnded?.Invoke();
            
            Debug.Log("Player fully recovered from stun");
            
            // Sync recovery to other clients
            if (photonView.IsMine)
            {
                photonView.RPC("RPC_SyncStunState", RpcTarget.OthersBuffered, false, 0f);
            }
        }
        
        /// <summary>
        /// Helper to get animation clip length by state name
        /// </summary>
        private float GetAnimationLength(string stateName)
        {
            if (animator == null) return 0f;
            
            var controller = animator.runtimeAnimatorController;
            if (controller == null) return 0f;
            
            foreach (var clip in controller.animationClips)
            {
                if (clip.name.Contains(stateName) || stateName.Contains(clip.name))
                {
                    Debug.Log($"Found animation clip '{clip.name}' with length: {clip.length}s");
                    return clip.length;
                }
            }
            
            return 0f;
        }

        private void ApplyStunVisuals()
        {
            // Tint player mesh
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i].material.HasProperty("_BaseColor"))
                {
                    playerRenderers[i].material.SetColor("_BaseColor", stunTintColor);
                }
            }
            
            // Spawn stun VFX
            if (stunVFXPrefab != null)
            {
                activeStunVFX = Instantiate(stunVFXPrefab, transform);
                activeStunVFX.transform.localPosition = Vector3.up * 1.5f;
            }
        }

        private void RemoveStunVisuals()
        {
            // Restore original colors
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i].material.HasProperty("_BaseColor"))
                {
                    playerRenderers[i].material.SetColor("_BaseColor", originalColors[i]);
                }
            }
            
            // Destroy stun VFX
            if (activeStunVFX != null)
            {
                Destroy(activeStunVFX);
            }
        }

        [PunRPC]
        private void RPC_SyncStunState(bool stunned, float duration)
        {
            // Sync stun state for remote players (visual only)
            if (stunned)
            {
                isStunned = true;
                stunTimer = duration;
                
                if (animator != null)
                {
                    animator.SetBool(StunnedHash, true);
                }
                
                ApplyStunVisuals();
                
                // Start a coroutine to handle get-up for remote players
                StartCoroutine(RemoteStunVisualCoroutine(duration));
            }
            else
            {
                isStunned = false;
                stunTimer = 0f;
                
                if (animator != null)
                {
                    animator.SetBool(StunnedHash, false);
                    animator.SetBool(GetUpHash, false);
                }
                
                RemoveStunVisuals();
            }
        }
        
        /// <summary>
        /// Handles animation states for remote players viewing the stunned player
        /// </summary>
        private IEnumerator RemoteStunVisualCoroutine(float stunDuration)
        {
            // Wait for stun duration
            yield return new WaitForSeconds(stunDuration);
            
            // Trigger get-up animation
            if (animator != null)
            {
                animator.SetBool(StunnedHash, false);
                animator.SetBool(GetUpHash, true);
            }
            
            // Wait for get-up animation
            float getUpDuration = GetAnimationLength("Get Up");
            if (getUpDuration <= 0) getUpDuration = 0.8f;
            
            yield return new WaitForSeconds(getUpDuration);
            
            // Complete recovery
            if (animator != null)
            {
                animator.SetBool(GetUpHash, false);
            }
            
            RemoveStunVisuals();
        }

        private void OnGUI()
        {
            if (!showDebugInfo || !photonView.IsMine) return;
            
            GUILayout.BeginArea(new Rect(10, 400, 300, 100));
            GUILayout.Label("=== PLAYER STATE ===");
            GUILayout.Label($"Stunned: {isStunned}");
            if (isStunned)
            {
                GUILayout.Label($"Recovery in: {stunTimer:F2}s");
            }
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
            }
        }
    }
}