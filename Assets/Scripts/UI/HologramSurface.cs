using UnityEngine;
using UnityEngine.UI;

namespace StellarCommand.UI
{
    /// <summary>
    /// Gives a RawImage its own instance of the hologram material so the reveal wipe
    /// can be animated per panel without touching the shared asset.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class HologramSurface : MonoBehaviour
    {
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");

        private Material _instance;

        private void Awake()
        {
            var image = GetComponent<RawImage>();
            if (image.material != null && image.material != image.defaultMaterial)
            {
                _instance = new Material(image.material);
                image.material = _instance;
            }
        }

        /// <summary>0 = fully hidden, 1 = fully revealed.</summary>
        public void SetReveal(float value)
        {
            if (_instance != null) _instance.SetFloat(RevealId, Mathf.Clamp01(value));
        }

        private void OnDestroy()
        {
            if (_instance != null) Destroy(_instance);
        }
    }
}
