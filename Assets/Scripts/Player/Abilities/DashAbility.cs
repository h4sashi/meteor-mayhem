using UnityEngine;
using Hanzo.Core.Interfaces;
using Hanzo.VFX;

namespace Hanzo.Player.Abilities
{
    public class DashAbility : IAbility
    {
        private IMovementController controller;
        private AbilitySettings settings;
        private TrailRenderer trailRenderer;
        private Animator animator;
        private DashVFXController vfxController;
        
        private bool isActive;
        private float dashTimer;
        private float cooldownTimer;
        private Vector3 dashDirection;
        
        // STACKING SYSTEM
        private int stackLevel = 1; // 1 = base, 2 = enhanced, 3 = chain
        private int chainDashesRemaining = 0;
        private float chainDashWindow = 0.5f; // Time window to activate chain dash
        private float chainDashTimer = 0f;
        
        private static readonly int IsDashingHash = Animator.StringToHash("DASH");
        
        public string AbilityName => "Dash";
        public bool CanActivate => !isActive && (cooldownTimer <= 0f || chainDashesRemaining > 0);
        public bool IsActive => isActive;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public int StackLevel => stackLevel;
        
        public DashAbility(AbilitySettings abilitySettings)
        {
            settings = abilitySettings;
        }

        public void SetVFXController(DashVFXController vfx)
        {
            vfxController = vfx;
        }
        
        public void Initialize(IMovementController movementController)
        {
            controller = movementController;
            
            animator = controller.Transform.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("DashAbility: No Animator found on player!");
            }

            if (vfxController == null && controller.Transform != null)
            {
                vfxController = controller.Transform.GetComponentInChildren<DashVFXController>(true);
            }
            
            SetupTrailRenderer();
        }
        
        private void SetupTrailRenderer()
        {
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
            
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
        }
        
        private Material CreateDefaultTrailMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.cyan);
            return mat;
        }
        
        /// <summary>
        /// Add a stack level to the dash ability (called when picking up power-up)
        /// </summary>
        public void AddStack()
        {
            if (stackLevel < 3)
            {
                stackLevel++;
                Debug.Log($"Dash stack increased to level {stackLevel}");
                
                // Update trail visual based on stack
                UpdateTrailForStack();
            }
        }
        
        /// <summary>
        /// Reset stacks to base level
        /// </summary>
        public void ResetStacks()
        {
            stackLevel = 1;
            chainDashesRemaining = 0;
            UpdateTrailForStack();
        }
        
        private void UpdateTrailForStack()
        {
            if (trailRenderer == null) return;
            
            // Make trail more intense with higher stacks
            switch (stackLevel)
            {
                case 1:
                    trailRenderer.time = settings.TrailTime;
                    trailRenderer.startWidth = settings.TrailWidth;
                    break;
                case 2:
                    trailRenderer.time = settings.TrailTime * 1.3f;
                    trailRenderer.startWidth = settings.TrailWidth * 1.2f;
                    break;
                case 3:
                    trailRenderer.time = settings.TrailTime * 1.5f;
                    trailRenderer.startWidth = settings.TrailWidth * 1.4f;
                    break;
            }
        }
        
        public bool TryActivate()
        {
            Debug.Log($"DashAbility.TryActivate - Stack: {stackLevel}, ChainRemaining: {chainDashesRemaining}");
            
            // Check if we can dash (normal cooldown OR chain dash available)
            if (isActive) return false;
            
            bool canDashNormally = cooldownTimer <= 0f;
            bool canChainDash = chainDashesRemaining > 0 && chainDashTimer > 0f;
            
            if (!canDashNormally && !canChainDash) return false;
            
            // Get dash direction
            Vector3 horizontalVelocity = new Vector3(controller.Velocity.x, 0, controller.Velocity.z);
            
            if (horizontalVelocity.magnitude > 0.1f)
            {
                dashDirection = horizontalVelocity.normalized;
            }
            else
            {
                dashDirection = controller.Transform.forward;
            }
            
            // Consume chain dash if using it
            if (canChainDash)
            {
                chainDashesRemaining--;
                Debug.Log($"Chain dash used! Remaining: {chainDashesRemaining}");
            }
            
            isActive = true;
            dashTimer = 0f;
            
            if (trailRenderer != null)
            {
                trailRenderer.emitting = true;
                trailRenderer.Clear();
            }
            
            if (animator != null)
            {
                animator.SetBool(IsDashingHash, true);
            }
            
            if (vfxController != null)
            {
                vfxController.Play();
            }
            
            return true;
        }
        
        public void Update()
        {
            // Update cooldown
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }
            
            // Update chain dash window
            if (chainDashesRemaining > 0)
            {
                chainDashTimer -= Time.deltaTime;
                if (chainDashTimer <= 0f)
                {
                    chainDashesRemaining = 0;
                    Debug.Log("Chain dash window expired");
                }
            }
            
            // Update active dash
            if (isActive)
            {
                dashTimer += Time.deltaTime;
                
                // Calculate duration and speed based on stack level
                float actualDuration = GetDashDuration();
                float normalizedTime = dashTimer / actualDuration;
                
                if (normalizedTime >= 1f)
                {
                    EndDash();
                    return;
                }
                
                // Apply dash force with stack multiplier
                float curveValue = settings.DashSpeedCurve.Evaluate(normalizedTime);
                float speedMultiplier = GetSpeedMultiplier();
                Vector3 dashVelocity = dashDirection * (settings.DashSpeed * speedMultiplier * curveValue);
                dashVelocity.y = controller.Velocity.y;
                
                controller.SetVelocity(dashVelocity);
            }
        }
        
        private float GetDashDuration()
        {
            // Stack 2 (2x): Travels further = longer duration
            return stackLevel == 2 ? settings.DashDuration * 1.4f : settings.DashDuration;
        }
        
        private float GetSpeedMultiplier()
        {
            // Stack 2 (2x): Travels further = faster speed
            return stackLevel == 2 ? 1.5f : 1f;
        }
        
        private void EndDash()
        {
            isActive = false;
            cooldownTimer = settings.DashCooldown;
            
            // Stack 3 (3x): Enable chain dash
            if (stackLevel == 3 && chainDashesRemaining == 0)
            {
                chainDashesRemaining = 1; // Allow 1 additional dash (2 total)
                chainDashTimer = chainDashWindow;
                Debug.Log("Chain dash ready! Press dash again within 0.5s");
            }
            
            if (trailRenderer != null) trailRenderer.emitting = false;
            
            if (animator != null)
            {
                animator.SetBool(IsDashingHash, false);
            }
            
            Debug.Log($"Dash ended. Cooldown: {cooldownTimer}s, Stack: {stackLevel}");
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