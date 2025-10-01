

using UnityEngine;

namespace Hanzo.Player.Abilities
{
    [CreateAssetMenu(fileName = "AbilitySettings", menuName = "Hanzo/Ability Settings")]
    public class AbilitySettings : ScriptableObject
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.3f;
        [SerializeField] private float dashCooldown = 1.5f;
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Trail Settings")]
        [SerializeField] private float trailTime = 0.5f;
        [SerializeField] private float trailWidth = 0.5f;
        [SerializeField] private Gradient trailColor;
        [SerializeField] private Material trailMaterial;

        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public AnimationCurve DashSpeedCurve => dashSpeedCurve;
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
        }
    }


}