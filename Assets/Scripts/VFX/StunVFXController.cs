using UnityEngine;

namespace Hanzo.VFX
{
    /// <summary>
    /// Simple spinning stars VFX for stunned players
    /// Place this in Scripts/VFX/
    /// Attach to a prefab with particle systems or animated sprites
    /// </summary>
    public class StunVFXController : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 180f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        
        [Header("Bobbing")]
        [SerializeField] private bool enableBobbing = true;
        [SerializeField] private float bobbingSpeed = 2f;
        [SerializeField] private float bobbingAmount = 0.2f;
        
        private Vector3 startPosition;
        private float bobbingTimer = 0f;
        
        private void Start()
        {
            startPosition = transform.localPosition;
            
            // Auto-play any particle systems
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Play();
            }
        }
        
        private void Update()
        {
            // Rotate continuously
            transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
            
            // Bob up and down
            if (enableBobbing)
            {
                bobbingTimer += Time.deltaTime * bobbingSpeed;
                float yOffset = Mathf.Sin(bobbingTimer) * bobbingAmount;
                transform.localPosition = startPosition + Vector3.up * yOffset;
            }
        }
    }
}