using UnityEngine;
using Hanzo.Core.Interfaces;
using Hanzo.VFX; // make sure this namespace matches your DashVFXController's namespace

namespace Hanzo.Player.Abilities
{
    public class DashAbility : IAbility
    {
        private IMovementController controller;
        private AbilitySettings settings;
        private TrailRenderer trailRenderer;
        private Animator animator;
        private DashVFXController vfxController; // <-- reference to VFX controller
        
        private bool isActive;
        private float dashTimer;
        private float cooldownTimer;
        private Vector3 dashDirection;
        
        // Animation parameter hash - ONLY use Bool, not Trigger
        private static readonly int IsDashingHash = Animator.StringToHash("DASH");
        
        public string AbilityName => "Dash";
        public bool CanActivate => !isActive && cooldownTimer <= 0f;
        public bool IsActive => isActive;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        
        public DashAbility(AbilitySettings abilitySettings)
        {
            settings = abilitySettings;
        }

        /// <summary>
        /// Optional setter so a MonoBehaviour can inject the VFX controller explicitly.
        /// Recommended: have PlayerAbilityController call this after creating the ability.
        /// </summary>
        public void SetVFXController(DashVFXController vfx)
        {
            vfxController = vfx;
        }
        
        public void Initialize(IMovementController movementController)
        {
            controller = movementController;
            
            // Get the Animator component (prefer controller's animator if exposed)
            animator = controller.Transform.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("DashAbility: No Animator found on player! Dash animations will not play.");
            }
            else
            {
                Debug.Log("DashAbility: Animator found and initialized");
            }

            // Best-effort: find DashVFXController on the player transform (only if not injected)
            if (vfxController == null && controller.Transform != null)
            {
                vfxController = controller.Transform.GetComponentInChildren<DashVFXController>(true);
                if (vfxController != null)
                {
                    Debug.Log("DashAbility: DashVFXController auto-found on player.");
                }
            }
            
            SetupTrailRenderer();
        }
        
        private void SetupTrailRenderer()
        {
            // Create trail renderer on the player
            GameObject trailObject = new GameObject("DashTrail");
            trailObject.transform.SetParent(controller.Transform, false);
            trailObject.transform.localPosition = Vector3.zero;
            
            trailRenderer = trailObject.AddComponent<TrailRenderer>();
            trailRenderer.time = settings.TrailTime;
            trailRenderer.startWidth = settings.TrailWidth;
            trailRenderer.endWidth = 0f;
            trailRenderer.colorGradient = settings.TrailColor;
            trailRenderer.material = settings.TrailMaterial != null 
                ? settings.TrailMaterial 
                : CreateDefaultTrailMaterial();
            trailRenderer.emitting = false;
            
            // URP specific settings
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
        }
        
        private Material CreateDefaultTrailMaterial()
        {
            // Create a default material for the trail
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.cyan);
            return mat;
        }
        
        public bool TryActivate()
        {
            Debug.Log($"DashAbility.TryActivate called. CanActivate: {CanActivate}");
            
            if (!CanActivate) return false;
            
            // Get dash direction from current velocity or forward
            Vector3 horizontalVelocity = new Vector3(controller.Velocity.x, 0, controller.Velocity.z);
            
            if (horizontalVelocity.magnitude > 0.1f)
            {
                dashDirection = horizontalVelocity.normalized;
            }
            else
            {
                dashDirection = controller.Transform.forward;
            }
            
            isActive = true;
            dashTimer = 0f;
            if (trailRenderer != null)
            {
                trailRenderer.emitting = true;
                trailRenderer.Clear();
            }
            
            // SET THE BOOL TO TRUE
            if (animator != null)
            {
                animator.SetBool(IsDashingHash, true);
                Debug.Log("✅ Dash animation BOOL set to TRUE!");
            }
            else
            {
                Debug.LogWarning("⚠️ Animator is null, cannot play dash animation!");
            }
            
            // PLAY DASH VFX (null-safe)
            if (vfxController != null)
            {
                vfxController.Play();
            }
            else
            {
                // optional: log once to know VFX wasn't wired
                Debug.LogWarning("DashAbility: No DashVFXController assigned or found. VFX will not play.");
            }
            
            Debug.Log($"✅ Dash activated! IsActive: {isActive}, Direction: {dashDirection}");
            
            return true;
        }
        
        public void Update()
        {
            // Update cooldown
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }
            
            // Update active dash
            if (isActive)
            {
                dashTimer += Time.deltaTime;
                float normalizedTime = dashTimer / settings.DashDuration;
                
                if (normalizedTime >= 1f)
                {
                    EndDash();
                    return;
                }
                
                // Apply dash force using curve
                float curveValue = settings.DashSpeedCurve.Evaluate(normalizedTime);
                Vector3 dashVelocity = dashDirection * (settings.DashSpeed * curveValue);
                dashVelocity.y = controller.Velocity.y; // Preserve vertical velocity
                
                controller.SetVelocity(dashVelocity);
            }
        }
        
        private void EndDash()
        {
            isActive = false;
            cooldownTimer = settings.DashCooldown;
            if (trailRenderer != null) trailRenderer.emitting = false;
            
            // SET THE BOOL TO FALSE
            if (animator != null)
            {
                animator.SetBool(IsDashingHash, false);
                Debug.Log("✅ Dash animation BOOL set to FALSE!");
            }
            
            Debug.Log($"Dash ended. Cooldown: {cooldownTimer}s");
        }
        
        public void Cleanup()
        {
            if (trailRenderer != null)
            {
                Object.Destroy(trailRenderer.gameObject);
            }
        }
    }
}
