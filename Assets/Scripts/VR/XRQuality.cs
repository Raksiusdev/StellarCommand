using UnityEngine;
using UnityEngine.XR;

namespace StellarCommand.VR
{
    /// <summary>
    /// Renders each eye above the headset's native resolution (supersampling), which makes small text on the
    /// game screen and thin lines on the map noticeably cleaner. Costs GPU time: lower it if frames drop.
    /// </summary>
    public class XRQuality : MonoBehaviour
    {
        [Tooltip("1 = native. 1.3 is a good balance on a mid-range GPU; 1.6+ needs a strong one.")]
        [Range(0.7f, 2f)] public float eyeResolutionScale = 1.3f;

        private void Start()
        {
            Apply();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }

        private void Apply()
        {
            XRSettings.eyeTextureResolutionScale = eyeResolutionScale;
        }
    }
}
