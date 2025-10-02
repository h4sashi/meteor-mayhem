using UnityEngine;
using Cinemachine;
using Hanzo.Player.Abilities;

namespace Hanzo.Core.Camera
{
    /// <summary>
    /// Handles camera effects during dash ability
    /// Attach this to the same GameObject as PlayerAbilityController
    /// </summary>
    public class DashCameraEffect : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomAmount = 10f; // How much to reduce FOV (zoom in)
        [SerializeField] private float zoomInDuration = 0.1f; // How fast to zoom in
        [SerializeField] private float zoomOutDuration = 0.2f; // How fast to zoom back out
        [SerializeField] private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Optional: Camera Shake")]
        [SerializeField] private bool enableShake = true;
        [SerializeField] private float shakeAmplitude = 1.2f;
        [SerializeField] private float shakeFrequency = 2.0f;
        [SerializeField] private float shakeDuration = 0.15f;
        
        private CinemachineVirtualCamera virtualCamera;
        private PlayerAbilityController abilityController;
        private CinemachineBasicMultiChannelPerlin noise;
        
        private float originalFOV;
        private float targetFOV;
        private float currentFOV;
        private bool isZooming = false;
        private float zoomTimer = 0f;
        private bool isZoomingIn = true;

        private void Awake()
        {
            abilityController = GetComponent<PlayerAbilityController>();
            if (abilityController == null)
            {
                Debug.LogError("DashCameraEffect: No PlayerAbilityController found!");
                enabled = false;
                return;
            }
            
            // Find the player's Cinemachine camera
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            if (virtualCamera == null)
            {
                Debug.LogError("DashCameraEffect: No CinemachineVirtualCamera found!");
                enabled = false;
                return;
            }
            
            // Store original FOV
            originalFOV = virtualCamera.m_Lens.FieldOfView;
            currentFOV = originalFOV;
            targetFOV = originalFOV;
            
            // Setup camera shake (optional)
            if (enableShake)
            {
                noise = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                if (noise == null)
                {
                    // Add noise component if it doesn't exist
                    noise = virtualCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                }
                
                // Disable shake by default
                noise.m_AmplitudeGain = 0f;
                noise.m_FrequencyGain = 0f;
            }
            
            Debug.Log($"DashCameraEffect initialized. Original FOV: {originalFOV}");
        }

        private void OnEnable()
        {
            if (abilityController != null && abilityController.DashAbility != null)
            {
                // Subscribe to dash events
                SubscribeToDashEvents();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromDashEvents();
        }

        private void SubscribeToDashEvents()
        {
            // We'll check dash state in Update since IAbility doesn't have events
            // Alternatively, you could add events to DashAbility
        }

        private void UnsubscribeFromDashEvents()
        {
            // Cleanup if needed
        }

        private void Update()
        {
            // Check if dash just started
            if (abilityController.DashAbility.IsActive && !isZooming)
            {
                StartDashZoom();
            }
            
            // Check if dash just ended
            if (!abilityController.DashAbility.IsActive && isZooming && !isZoomingIn)
            {
                // Zoom is already transitioning out, let it complete
            }
            
            // Update zoom animation
            if (isZooming)
            {
                UpdateZoom();
            }
        }

        private void StartDashZoom()
        {
            isZooming = true;
            isZoomingIn = true;
            zoomTimer = 0f;
            targetFOV = originalFOV - zoomAmount;
            
            // Trigger camera shake
            if (enableShake && noise != null)
            {
                StartCoroutine(CameraShakeCoroutine());
            }
            
            Debug.Log($"Dash zoom started! Zooming from {currentFOV} to {targetFOV}");
        }

        private void UpdateZoom()
        {
            if (isZoomingIn)
            {
                // Zoom IN (decrease FOV)
                zoomTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(zoomTimer / zoomInDuration);
                float curveValue = zoomCurve.Evaluate(progress);
                
                currentFOV = Mathf.Lerp(originalFOV, targetFOV, curveValue);
                virtualCamera.m_Lens.FieldOfView = currentFOV;
                
                // When zoom-in completes, start monitoring for dash end
                if (progress >= 1f)
                {
                    isZoomingIn = false;
                    zoomTimer = 0f;
                    
                    // If dash already ended, start zooming out immediately
                    if (!abilityController.DashAbility.IsActive)
                    {
                        StartZoomOut();
                    }
                }
            }
            else
            {
                // Wait for dash to end before zooming out
                if (!abilityController.DashAbility.IsActive)
                {
                    // Zoom OUT (increase FOV back to original)
                    zoomTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(zoomTimer / zoomOutDuration);
                    float curveValue = zoomCurve.Evaluate(progress);
                    
                    currentFOV = Mathf.Lerp(targetFOV, originalFOV, curveValue);
                    virtualCamera.m_Lens.FieldOfView = currentFOV;
                    
                    // When zoom-out completes, finish
                    if (progress >= 1f)
                    {
                        isZooming = false;
                        currentFOV = originalFOV;
                        virtualCamera.m_Lens.FieldOfView = originalFOV;
                        Debug.Log("Dash zoom completed, FOV restored");
                    }
                }
            }
        }

        private void StartZoomOut()
        {
            zoomTimer = 0f;
            Debug.Log("Starting zoom out");
        }

        private System.Collections.IEnumerator CameraShakeCoroutine()
        {
            if (noise == null) yield break;
            
            // Apply shake
            noise.m_AmplitudeGain = shakeAmplitude;
            noise.m_FrequencyGain = shakeFrequency;
            
            // Wait for shake duration
            yield return new WaitForSeconds(shakeDuration);
            
            // Smooth fade out shake
            float fadeTime = 0.1f;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeTime;
                
                noise.m_AmplitudeGain = Mathf.Lerp(shakeAmplitude, 0f, t);
                noise.m_FrequencyGain = Mathf.Lerp(shakeFrequency, 0f, t);
                
                yield return null;
            }
            
            // Ensure shake is fully disabled
            noise.m_AmplitudeGain = 0f;
            noise.m_FrequencyGain = 0f;
        }

        private void OnDestroy()
        {
            // Restore original FOV
            if (virtualCamera != null)
            {
                virtualCamera.m_Lens.FieldOfView = originalFOV;
            }
        }

        // Optional: Public method to manually trigger effect
        public void TriggerDashZoom()
        {
            if (!isZooming)
            {
                StartDashZoom();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Validate settings
            zoomAmount = Mathf.Max(0f, zoomAmount);
            zoomInDuration = Mathf.Max(0.01f, zoomInDuration);
            zoomOutDuration = Mathf.Max(0.01f, zoomOutDuration);
            shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
            shakeFrequency = Mathf.Max(0f, shakeFrequency);
            shakeDuration = Mathf.Max(0f, shakeDuration);
        }
#endif
    }
}