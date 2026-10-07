using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace StellarCommand.VR
{
    /// <summary>
    /// Root VR rig for the command bridge. Manages head + hands tracking
    /// and provides the world-space anchor for all holographic panels.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VRRig : MonoBehaviour
    {
        public static VRRig Instance { get; private set; }

        [Header("Rig Components")]
        public Transform cameraOffset;
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("Settings")]
        [Tooltip("Height offset applied when playing in non-VR fallback mode.")]
        public float fallbackEyeHeight = 1.65f;

        // XRSettings.isDeviceActive is the legacy API and reports false under OpenXR,
        // so ask the XR Management loader instead.
        public bool IsVRActive =>
            XRGeneralSettings.Instance != null &&
            XRGeneralSettings.Instance.Manager != null &&
            XRGeneralSettings.Instance.Manager.activeLoader != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (!IsVRActive)
            {
                Debug.LogWarning("[VRRig] XR not active — running in desktop fallback mode.");
                if (headTransform != null)
                    headTransform.localPosition = new Vector3(0, fallbackEyeHeight, 0);
            }
        }

        /// <summary>Returns the forward direction of the player's head (for panel placement).</summary>
        public Vector3 HeadForward => headTransform != null ? headTransform.forward : transform.forward;

        /// <summary>Returns a point in front of the player at the given distance and optional vertical offset.</summary>
        public Vector3 GetAnchorPoint(float distance, float verticalOffset = 0f)
        {
            if (headTransform == null) return transform.position + Vector3.forward * distance;
            var origin = headTransform.position + Vector3.up * verticalOffset;
            var flat = new Vector3(HeadForward.x, 0, HeadForward.z).normalized;
            return origin + flat * distance;
        }
    }
}
