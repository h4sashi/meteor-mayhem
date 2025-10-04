using UnityEngine;

namespace Hanzo.Player.Abilities
{
    [CreateAssetMenu(fileName = "AbilitySettings", menuName = "Hanzo/Ability Settings")]
    public class AbilitySettings : ScriptableObject
    {
        [Header("Dash Settings - Base (Stack 1)")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.3f;
        [SerializeField] private float dashCooldown = 1.5f;
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Dash Stacking")]
        [Tooltip("Stack 1: Base dash\nStack 2: 1.5x distance\nStack 3: Chain dash (2 dashes)")]
        [SerializeField] private bool enableStacking = true;
        
        [Header("Knockback & Stun Settings - Players")]
        [Tooltip("Force applied to opponents on collision")]
        [SerializeField] private float knockbackForce = 15f;
        [SerializeField] private float knockbackRadius = 1.5f;
        [Tooltip("Duration victim is stunned after being hit (seconds)")]
        [SerializeField] private float stunDuration = 2f;
        [Tooltip("If true, higher dash stacks apply more knockback")]
        [SerializeField] private bool scaleKnockbackWithStack = true;
        
        [Header("Knockback Settings - Destructibles")]
        [Tooltip("Force multiplier for destructible objects (relative to player knockback)")]
        [SerializeField] private float destructibleForceMultiplier = 1.5f;
        [Tooltip("Upward force component for destructibles (0-1)")]
        [SerializeField] private float destructibleUpwardForce = 0.6f;
        [Tooltip("Damage dealt to destructibles with health system")]
        [SerializeField] private int destructibleDamage = 25;
        
        [Header("Trail Settings")]
        [SerializeField] private float trailTime = 0.5f;
        [SerializeField] private float trailWidth = 0.5f;
        [SerializeField] private Gradient trailColor;
        [SerializeField] private Material trailMaterial;

        // Properties - Dash
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public AnimationCurve DashSpeedCurve => dashSpeedCurve;
        public bool EnableStacking => enableStacking;
        
        // Properties - Player Knockback
        public float KnockbackForce => knockbackForce;
        public float KnockbackRadius => knockbackRadius;
        public float StunDuration => stunDuration;
        public bool ScaleKnockbackWithStack => scaleKnockbackWithStack;
        
        // Properties - Destructible Knockback
        public float DestructibleForceMultiplier => destructibleForceMultiplier;
        public float DestructibleUpwardForce => destructibleUpwardForce;
        public int DestructibleDamage => destructibleDamage;
        
        // Properties - Trail
        public float TrailTime => trailTime;
        public float TrailWidth => trailWidth;
        public Gradient TrailColor => trailColor;
        public Material TrailMaterial => trailMaterial;

        private void OnValidate()
        {
            // Initialize default gradient if null
            if (trailColor == null || trailColor.colorKeys.Length == 0)
            {
                trailColor = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[2];
                colorKeys[0] = new GradientColorKey(Color.cyan, 0f);
                colorKeys[1] = new GradientColorKey(Color.blue, 1f);

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(0f, 1f);

                trailColor.SetKeys(colorKeys, alphaKeys);
            }

            // Validate values
            dashSpeed = Mathf.Max(1f, dashSpeed);
            dashDuration = Mathf.Max(0.1f, dashDuration);
            dashCooldown = Mathf.Max(0f, dashCooldown);
            knockbackForce = Mathf.Max(0f, knockbackForce);
            destructibleForceMultiplier = Mathf.Max(0f, destructibleForceMultiplier);
            destructibleUpwardForce = Mathf.Clamp01(destructibleUpwardForce);
            destructibleDamage = Mathf.Max(0, destructibleDamage);
        }
    }
}