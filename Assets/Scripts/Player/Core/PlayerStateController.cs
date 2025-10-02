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
        private Renderer[] playerRenderers;
        private Color[] originalColors;
        private GameObject activeStunVFX;
        
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
            
            // Restore
            isStunned = false;
            stunTimer = 0f;
            rb.linearDamping = originalDrag;
            
            RemoveStunVisuals();
            OnStunEnded?.Invoke();
            
            Debug.Log("Player recovered from stun");
            
            // Sync recovery to other clients
            if (photonView.IsMine)
            {
                photonView.RPC("RPC_SyncStunState", RpcTarget.OthersBuffered, false, 0f);
            }
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
                ApplyStunVisuals();
            }
            else
            {
                isStunned = false;
                stunTimer = 0f;
                RemoveStunVisuals();
            }
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