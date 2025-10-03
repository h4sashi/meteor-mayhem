// using UnityEngine;
// using UnityEngine.UI;
// using Cinemachine;
// using Hanzo.Player.Abilities;

// namespace Hanzo.Core.Camera
// {
//     /// <summary>
//     /// Handles camera effects during dash ability including zoom and color splash
//     /// Attach this to the same GameObject as PlayerAbilityController
//     /// </summary>
//     public class DashCameraEffect : MonoBehaviour
//     {
//         [Header("Zoom Settings")]
//         [SerializeField] private float zoomAmount = 10f;
//         [SerializeField] private float zoomInDuration = 0.1f;
//         [SerializeField] private float zoomOutDuration = 0.2f;
//         [SerializeField] private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
//         [Header("Color Splash Settings")]
//         [SerializeField] private bool enableColorSplash = true;
//         [SerializeField] private Color splashColor = new Color(0f, 0.8f, 1f, 0.3f); // Cyan with transparency
//         [SerializeField] private float splashFadeInDuration = 0.08f;
//         [SerializeField] private float splashHoldDuration = 0.1f;
//         [SerializeField] private float splashFadeOutDuration = 0.25f;
//         [SerializeField] private AnimationCurve splashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
//         [Header("Optional: Camera Shake")]
//         [SerializeField] private bool enableShake = true;
//         [SerializeField] private float shakeAmplitude = 1.2f;
//         [SerializeField] private float shakeFrequency = 2.0f;
//         [SerializeField] private float shakeDuration = 0.15f;
        
//         [Header("UI References")]
//         [SerializeField] private Canvas uiCanvas;
//         [SerializeField] private Image colorSplashImage;
        
//         private CinemachineVirtualCamera virtualCamera;
//         private PlayerAbilityController abilityController;
//         private CinemachineBasicMultiChannelPerlin noise;
        
//         // Zoom state
//         private float originalFOV;
//         private float targetFOV;
//         private float currentFOV;
//         private bool isZooming = false;
//         private float zoomTimer = 0f;
//         private bool isZoomingIn = true;
        
//         // Color splash state
//         private bool isSplashing = false;
//         private float splashTimer = 0f;
//         private enum SplashPhase { FadeIn, Hold, FadeOut, Complete }
//         private SplashPhase currentSplashPhase = SplashPhase.Complete;

//         private void Awake()
//         {
//             abilityController = GetComponent<PlayerAbilityController>();
//             if (abilityController == null)
//             {
//                 Debug.LogError("DashCameraEffect: No PlayerAbilityController found!");
//                 enabled = false;
//                 return;
//             }
            
//             virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
//             if (virtualCamera == null)
//             {
//                 Debug.LogError("DashCameraEffect: No CinemachineVirtualCamera found!");
//                 enabled = false;
//                 return;
//             }
            
//             originalFOV = virtualCamera.m_Lens.FieldOfView;
//             currentFOV = originalFOV;
//             targetFOV = originalFOV;
            
//             if (enableShake)
//             {
//                 noise = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
//                 if (noise == null)
//                 {
//                     noise = virtualCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
//                 }
//                 noise.m_AmplitudeGain = 0f;
//                 noise.m_FrequencyGain = 0f;
//             }
            
//             // Setup color splash UI
//             SetupColorSplashUI();
            
//             Debug.Log($"DashCameraEffect initialized. Original FOV: {originalFOV}");
//         }

//         private void SetupColorSplashUI()
//         {
//             if (!enableColorSplash) return;
            
//             // Find or create canvas
//             if (uiCanvas == null)
//             {
//                 uiCanvas = FindObjectOfType<Canvas>();
                
//                 if (uiCanvas == null)
//                 {
//                     GameObject canvasObj = new GameObject("DashEffectCanvas");
//                     uiCanvas = canvasObj.AddComponent<Canvas>();
//                     uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
//                     uiCanvas.sortingOrder = 100; // High sorting order to appear on top
                    
//                     canvasObj.AddComponent<CanvasScaler>();
//                     canvasObj.AddComponent<GraphicRaycaster>();
                    
//                     Debug.Log("Created new Canvas for dash effect");
//                 }
//             }
            
//             // Create color splash image if not assigned
//             if (colorSplashImage == null)
//             {
//                 GameObject imageObj = new GameObject("ColorSplashImage");
//                 imageObj.transform.SetParent(uiCanvas.transform, false);
                
//                 colorSplashImage = imageObj.AddComponent<Image>();
                
//                 // Setup RectTransform to fill entire screen
//                 RectTransform rect = colorSplashImage.rectTransform;
//                 rect.anchorMin = Vector2.zero;
//                 rect.anchorMax = Vector2.one;
//                 rect.sizeDelta = Vector2.zero;
//                 rect.anchoredPosition = Vector2.zero;
                
//                 // Set initial color (fully transparent)
//                 colorSplashImage.color = new Color(splashColor.r, splashColor.g, splashColor.b, 0f);
                
//                 // Disable raycast target so it doesn't block UI clicks
//                 colorSplashImage.raycastTarget = false;
                
//                 Debug.Log("Created color splash image UI");
//             }
            
//             // Ensure it starts invisible
//             if (colorSplashImage != null)
//             {
//                 Color c = colorSplashImage.color;
//                 c.a = 0f;
//                 colorSplashImage.color = c;
//             }
//         }

//         private void Update()
//         {
//             if (abilityController.DashAbility.IsActive && !isZooming)
//             {
//                 StartDashEffects();
//             }
            
//             if (isZooming)
//             {
//                 UpdateZoom();
//             }
            
//             if (isSplashing && enableColorSplash)
//             {
//                 UpdateColorSplash();
//             }
//         }

//         private void StartDashEffects()
//         {
//             // Start zoom
//             isZooming = true;
//             isZoomingIn = true;
//             zoomTimer = 0f;
//             targetFOV = originalFOV - zoomAmount;
            
//             // Start color splash
//             if (enableColorSplash && colorSplashImage != null)
//             {
//                 isSplashing = true;
//                 splashTimer = 0f;
//                 currentSplashPhase = SplashPhase.FadeIn;
//             }
            
//             // Start camera shake
//             if (enableShake && noise != null)
//             {
//                 StartCoroutine(CameraShakeCoroutine());
//             }
            
//             Debug.Log("Dash effects started!");
//         }

//         private void UpdateZoom()
//         {
//             if (isZoomingIn)
//             {
//                 zoomTimer += Time.deltaTime;
//                 float progress = Mathf.Clamp01(zoomTimer / zoomInDuration);
//                 float curveValue = zoomCurve.Evaluate(progress);
                
//                 currentFOV = Mathf.Lerp(originalFOV, targetFOV, curveValue);
//                 virtualCamera.m_Lens.FieldOfView = currentFOV;
                
//                 if (progress >= 1f)
//                 {
//                     isZoomingIn = false;
//                     zoomTimer = 0f;
                    
//                     if (!abilityController.DashAbility.IsActive)
//                     {
//                         StartZoomOut();
//                     }
//                 }
//             }
//             else
//             {
//                 if (!abilityController.DashAbility.IsActive)
//                 {
//                     zoomTimer += Time.deltaTime;
//                     float progress = Mathf.Clamp01(zoomTimer / zoomOutDuration);
//                     float curveValue = zoomCurve.Evaluate(progress);
                    
//                     currentFOV = Mathf.Lerp(targetFOV, originalFOV, curveValue);
//                     virtualCamera.m_Lens.FieldOfView = currentFOV;
                    
//                     if (progress >= 1f)
//                     {
//                         isZooming = false;
//                         currentFOV = originalFOV;
//                         virtualCamera.m_Lens.FieldOfView = originalFOV;
//                     }
//                 }
//             }
//         }

//         private void UpdateColorSplash()
//         {
//             if (colorSplashImage == null) return;
            
//             splashTimer += Time.deltaTime;
//             Color currentColor = colorSplashImage.color;
            
//             switch (currentSplashPhase)
//             {
//                 case SplashPhase.FadeIn:
//                     float fadeInProgress = Mathf.Clamp01(splashTimer / splashFadeInDuration);
//                     float fadeInCurve = splashCurve.Evaluate(fadeInProgress);
//                     currentColor.a = Mathf.Lerp(0f, splashColor.a, fadeInCurve);
//                     colorSplashImage.color = currentColor;
                    
//                     if (fadeInProgress >= 1f)
//                     {
//                         currentSplashPhase = SplashPhase.Hold;
//                         splashTimer = 0f;
//                     }
//                     break;
                    
//                 case SplashPhase.Hold:
//                     if (splashTimer >= splashHoldDuration)
//                     {
//                         currentSplashPhase = SplashPhase.FadeOut;
//                         splashTimer = 0f;
//                     }
//                     break;
                    
//                 case SplashPhase.FadeOut:
//                     float fadeOutProgress = Mathf.Clamp01(splashTimer / splashFadeOutDuration);
//                     float fadeOutCurve = splashCurve.Evaluate(fadeOutProgress);
//                     currentColor.a = Mathf.Lerp(splashColor.a, 0f, fadeOutCurve);
//                     colorSplashImage.color = currentColor;
                    
//                     if (fadeOutProgress >= 1f)
//                     {
//                         currentSplashPhase = SplashPhase.Complete;
//                         isSplashing = false;
//                         currentColor.a = 0f;
//                         colorSplashImage.color = currentColor;
//                     }
//                     break;
//             }
//         }

//         private void StartZoomOut()
//         {
//             zoomTimer = 0f;
//         }

//         private System.Collections.IEnumerator CameraShakeCoroutine()
//         {
//             if (noise == null) yield break;
            
//             noise.m_AmplitudeGain = shakeAmplitude;
//             noise.m_FrequencyGain = shakeFrequency;
            
//             yield return new WaitForSeconds(shakeDuration);
            
//             float fadeTime = 0.1f;
//             float elapsed = 0f;
            
//             while (elapsed < fadeTime)
//             {
//                 elapsed += Time.deltaTime;
//                 float t = elapsed / fadeTime;
                
//                 noise.m_AmplitudeGain = Mathf.Lerp(shakeAmplitude, 0f, t);
//                 noise.m_FrequencyGain = Mathf.Lerp(shakeFrequency, 0f, t);
                
//                 yield return null;
//             }
            
//             noise.m_AmplitudeGain = 0f;
//             noise.m_FrequencyGain = 0f;
//         }

//         private void OnDestroy()
//         {
//             if (virtualCamera != null)
//             {
//                 virtualCamera.m_Lens.FieldOfView = originalFOV;
//             }
            
//             if (colorSplashImage != null && colorSplashImage.gameObject != null)
//             {
//                 Destroy(colorSplashImage.gameObject);
//             }
//         }

// #if UNITY_EDITOR
//         private void OnValidate()
//         {
//             zoomAmount = Mathf.Max(0f, zoomAmount);
//             zoomInDuration = Mathf.Max(0.01f, zoomInDuration);
//             zoomOutDuration = Mathf.Max(0.01f, zoomOutDuration);
//             shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
//             shakeFrequency = Mathf.Max(0f, shakeFrequency);
//             shakeDuration = Mathf.Max(0f, shakeDuration);
//             splashFadeInDuration = Mathf.Max(0.01f, splashFadeInDuration);
//             splashHoldDuration = Mathf.Max(0f, splashHoldDuration);
//             splashFadeOutDuration = Mathf.Max(0.01f, splashFadeOutDuration);
            
//             // Clamp alpha
//             Color c = splashColor;
//             c.a = Mathf.Clamp01(c.a);
//             splashColor = c;
//         }
// #endif
//     }
// }

using UnityEngine;
using UnityEngine.UI;
using Cinemachine;
using Hanzo.Player.Abilities;

namespace Hanzo.Player.Camera
{
    /// <summary>
    /// Handles camera effects during dash ability including zoom, shake, and color splash
    /// Uses Perlin noise via CinemachineBasicMultiChannelPerlin (Cinemachine's built-in component)
    /// Attach this to the same GameObject as PlayerAbilityController
    /// </summary>
    public class DashCameraEffect : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomAmount = 10f;
        [SerializeField] private float zoomInDuration = 0.1f;
        [SerializeField] private float zoomOutDuration = 0.2f;
        [SerializeField] private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Color Splash Settings")]
        [SerializeField] private bool enableColorSplash = true;
        [SerializeField] private Color splashColor = new Color(0f, 0.8f, 1f, 0.3f);
        [SerializeField] private float splashFadeInDuration = 0.08f;
        [SerializeField] private float splashHoldDuration = 0.1f;
        [SerializeField] private float splashFadeOutDuration = 0.25f;
        [SerializeField] private AnimationCurve splashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Camera Shake (Perlin Noise)")]
        [SerializeField] private bool enableShake = true;
        [SerializeField] private float shakeAmplitude = 1.5f;
        [SerializeField] private float shakeFrequency = 2.0f;
        [SerializeField] private float shakeDuration = 0.2f;
        
        [Header("UI References")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private Image colorSplashImage;
        
        private CinemachineVirtualCamera virtualCamera;
        private PlayerAbilityController abilityController;
        private CinemachineBasicMultiChannelPerlin noiseComponent;
        
        // Zoom state
        private float originalFOV;
        private float targetFOV;
        private float currentFOV;
        private bool isZooming = false;
        private float zoomTimer = 0f;
        private bool isZoomingIn = true;
        
        // Color splash state
        private bool isSplashing = false;
        private float splashTimer = 0f;
        private enum SplashPhase { FadeIn, Hold, FadeOut, Complete }
        private SplashPhase currentSplashPhase = SplashPhase.Complete;
        
        // Shake state
        private bool isShaking = false;
        private float shakeTimer = 0f;

        private void Awake()
        {
            abilityController = GetComponent<PlayerAbilityController>();
            if (abilityController == null)
            {
                Debug.LogError("DashCameraEffect: No PlayerAbilityController found!");
                enabled = false;
                return;
            }
            
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            if (virtualCamera == null)
            {
                Debug.LogError("DashCameraEffect: No CinemachineVirtualCamera found!");
                enabled = false;
                return;
            }
            
            originalFOV = virtualCamera.m_Lens.FieldOfView;
            currentFOV = originalFOV;
            targetFOV = originalFOV;
            
            // Setup Perlin noise component for camera shake
            SetupPerlinNoise();
            
            SetupColorSplashUI();
            
            Debug.Log($"DashCameraEffect initialized. Original FOV: {originalFOV}");
        }

        private void SetupPerlinNoise()
        {
            if (!enableShake) return;
            
            // Get or add CinemachineBasicMultiChannelPerlin component
            noiseComponent = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            
            if (noiseComponent == null)
            {
                noiseComponent = virtualCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                Debug.Log("Added CinemachineBasicMultiChannelPerlin component to virtual camera");
            }
            
            // The noise profile will be assigned automatically by Cinemachine or you can assign it in Inspector
            // If no profile is assigned, Cinemachine uses a default noise pattern
            
            // Initialize with no shake
            noiseComponent.m_AmplitudeGain = 0f;
            noiseComponent.m_FrequencyGain = 0f;
            
            Debug.Log("Perlin noise shake system initialized");
        }

        private void SetupColorSplashUI()
        {
            if (!enableColorSplash) return;
            
            if (uiCanvas == null)
            {
                uiCanvas = FindObjectOfType<Canvas>();
                
                if (uiCanvas == null)
                {
                    GameObject canvasObj = new GameObject("DashEffectCanvas");
                    uiCanvas = canvasObj.AddComponent<Canvas>();
                    uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    uiCanvas.sortingOrder = 100;
                    
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                    
                    Debug.Log("Created new Canvas for dash effect");
                }
            }
            
            if (colorSplashImage == null)
            {
                GameObject imageObj = new GameObject("ColorSplashImage");
                imageObj.transform.SetParent(uiCanvas.transform, false);
                
                colorSplashImage = imageObj.AddComponent<Image>();
                
                RectTransform rect = colorSplashImage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                
                colorSplashImage.color = new Color(splashColor.r, splashColor.g, splashColor.b, 0f);
                colorSplashImage.raycastTarget = false;
                
                Debug.Log("Created color splash image UI");
            }
            
            if (colorSplashImage != null)
            {
                Color c = colorSplashImage.color;
                c.a = 0f;
                colorSplashImage.color = c;
            }
        }

        private void Update()
        {
            if (abilityController.DashAbility.IsActive && !isZooming)
            {
                StartDashEffects();
            }
            
            if (isZooming)
            {
                UpdateZoom();
            }
            
            if (isSplashing && enableColorSplash)
            {
                UpdateColorSplash();
            }
            
            if (isShaking && enableShake)
            {
                UpdateCameraShake();
            }
        }

        private void StartDashEffects()
        {
            isZooming = true;
            isZoomingIn = true;
            zoomTimer = 0f;
            targetFOV = originalFOV - zoomAmount;
            
            if (enableColorSplash && colorSplashImage != null)
            {
                isSplashing = true;
                splashTimer = 0f;
                currentSplashPhase = SplashPhase.FadeIn;
            }
            
            if (enableShake && noiseComponent != null)
            {
                isShaking = true;
                shakeTimer = 0f;
                noiseComponent.m_AmplitudeGain = shakeAmplitude;
                noiseComponent.m_FrequencyGain = shakeFrequency;
                Debug.Log($"Camera shake started: Amplitude={shakeAmplitude}, Frequency={shakeFrequency}");
            }
            
            Debug.Log("Dash effects started!");
        }

        private void UpdateZoom()
        {
            if (isZoomingIn)
            {
                zoomTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(zoomTimer / zoomInDuration);
                float curveValue = zoomCurve.Evaluate(progress);
                
                currentFOV = Mathf.Lerp(originalFOV, targetFOV, curveValue);
                virtualCamera.m_Lens.FieldOfView = currentFOV;
                
                if (progress >= 1f)
                {
                    isZoomingIn = false;
                    zoomTimer = 0f;
                    
                    if (!abilityController.DashAbility.IsActive)
                    {
                        StartZoomOut();
                    }
                }
            }
            else
            {
                if (!abilityController.DashAbility.IsActive)
                {
                    zoomTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(zoomTimer / zoomOutDuration);
                    float curveValue = zoomCurve.Evaluate(progress);
                    
                    currentFOV = Mathf.Lerp(targetFOV, originalFOV, curveValue);
                    virtualCamera.m_Lens.FieldOfView = currentFOV;
                    
                    if (progress >= 1f)
                    {
                        isZooming = false;
                        currentFOV = originalFOV;
                        virtualCamera.m_Lens.FieldOfView = originalFOV;
                    }
                }
            }
        }

        private void UpdateColorSplash()
        {
            if (colorSplashImage == null) return;
            
            splashTimer += Time.deltaTime;
            Color currentColor = colorSplashImage.color;
            
            switch (currentSplashPhase)
            {
                case SplashPhase.FadeIn:
                    float fadeInProgress = Mathf.Clamp01(splashTimer / splashFadeInDuration);
                    float fadeInCurve = splashCurve.Evaluate(fadeInProgress);
                    currentColor.a = Mathf.Lerp(0f, splashColor.a, fadeInCurve);
                    colorSplashImage.color = currentColor;
                    
                    if (fadeInProgress >= 1f)
                    {
                        currentSplashPhase = SplashPhase.Hold;
                        splashTimer = 0f;
                    }
                    break;
                    
                case SplashPhase.Hold:
                    if (splashTimer >= splashHoldDuration)
                    {
                        currentSplashPhase = SplashPhase.FadeOut;
                        splashTimer = 0f;
                    }
                    break;
                    
                case SplashPhase.FadeOut:
                    float fadeOutProgress = Mathf.Clamp01(splashTimer / splashFadeOutDuration);
                    float fadeOutCurve = splashCurve.Evaluate(fadeOutProgress);
                    currentColor.a = Mathf.Lerp(splashColor.a, 0f, fadeOutCurve);
                    colorSplashImage.color = currentColor;
                    
                    if (fadeOutProgress >= 1f)
                    {
                        currentSplashPhase = SplashPhase.Complete;
                        isSplashing = false;
                        currentColor.a = 0f;
                        colorSplashImage.color = currentColor;
                    }
                    break;
            }
        }

        private void UpdateCameraShake()
        {
            if (noiseComponent == null) return;
            
            shakeTimer += Time.deltaTime;
            
            if (shakeTimer >= shakeDuration)
            {
                // Fade out shake
                isShaking = false;
                noiseComponent.m_AmplitudeGain = 0f;
                noiseComponent.m_FrequencyGain = 0f;
                Debug.Log("Camera shake ended");
                return;
            }
            
            // Optional: Fade out shake over time for smoother end
            float remainingTime = shakeDuration - shakeTimer;
            if (remainingTime < 0.1f)
            {
                float fadeProgress = remainingTime / 0.1f;
                noiseComponent.m_AmplitudeGain = shakeAmplitude * fadeProgress;
                noiseComponent.m_FrequencyGain = shakeFrequency * fadeProgress;
            }
        }

        private void StartZoomOut()
        {
            zoomTimer = 0f;
        }

        private void OnDestroy()
        {
            if (virtualCamera != null)
            {
                virtualCamera.m_Lens.FieldOfView = originalFOV;
            }
            
            if (noiseComponent != null)
            {
                noiseComponent.m_AmplitudeGain = 0f;
                noiseComponent.m_FrequencyGain = 0f;
            }
            
            if (colorSplashImage != null && colorSplashImage.gameObject != null)
            {
                Destroy(colorSplashImage.gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            zoomAmount = Mathf.Max(0f, zoomAmount);
            zoomInDuration = Mathf.Max(0.01f, zoomInDuration);
            zoomOutDuration = Mathf.Max(0.01f, zoomOutDuration);
            shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
            shakeFrequency = Mathf.Max(0f, shakeFrequency);
            shakeDuration = Mathf.Max(0f, shakeDuration);
            splashFadeInDuration = Mathf.Max(0.01f, splashFadeInDuration);
            splashHoldDuration = Mathf.Max(0f, splashHoldDuration);
            splashFadeOutDuration = Mathf.Max(0.01f, splashFadeOutDuration);
            
            Color c = splashColor;
            c.a = Mathf.Clamp01(c.a);
            splashColor = c;
        }
#endif
    }
}