using UnityEngine;
using TMPro;

namespace StellarCommand.UI
{
    /// <summary>
    /// Base class for all holographic data panels in the command bridge.
    /// Handles billboard facing, pulse animation, and data binding lifecycle.
    /// </summary>
    public abstract class HolographicPanel : MonoBehaviour
    {
        [Header("Panel Config")]
        public string panelTitle = "Panel";
        public bool faceCamera = true;
        public float activationDelay = 0f;

        [Header("References")]
        public TextMeshProUGUI titleLabel;
        public CanvasGroup canvasGroup;

        [Header("Animation")]
        public float pulseSpeed = 1.2f;
        public float pulseMinAlpha = 0.85f;
        [Tooltip("Seconds for the scan-in wipe when the panel appears.")]
        public float revealDuration = 0.9f;

        private Camera _mainCam;
        private float _timer;
        private float _revealT;
        private bool _active;
        private HologramSurface[] _surfaces;

        protected virtual void Awake()
        {
            _mainCam = Camera.main;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (titleLabel != null) titleLabel.text = panelTitle;
            _surfaces = GetComponentsInChildren<HologramSurface>(true);
        }

        protected virtual void OnEnable()
        {
            // Start invisible, scan in after delay
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            _active = false;
            _timer = 0f;
            _revealT = 0f;
            ApplyReveal(0f);
        }

        private void ApplyReveal(float t)
        {
            if (_surfaces == null) return;
            foreach (var s in _surfaces)
                if (s != null) s.SetReveal(t);
        }

        protected virtual void Update()
        {
            if (!_active)
            {
                _timer += Time.deltaTime;
                if (_timer >= activationDelay)
                {
                    _active = true;
                    OnPanelActivated();
                }
                return;
            }

            // Scan-in wipe: surface reveals bottom to top, content fades in with it
            if (_revealT < 1f)
            {
                _revealT = revealDuration <= 0f ? 1f : Mathf.Min(1f, _revealT + Time.deltaTime / revealDuration);
                ApplyReveal(Mathf.SmoothStep(0f, 1f, _revealT));
            }

            // Billboard: always face the camera
            if (faceCamera && _mainCam != null)
            {
                var dir = _mainCam.transform.position - transform.position;
                dir.y = 0;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(-dir);
            }

            // Subtle alpha pulse
            if (canvasGroup != null)
            {
                float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
                canvasGroup.alpha = Mathf.Lerp(pulseMinAlpha, 1f, pulse) * Mathf.Clamp01(_revealT * 2f);
            }
        }

        protected virtual void OnPanelActivated()
        {
            // Alpha is driven from Update (pulse * reveal); nothing to do by default.
        }

        /// <summary>Called when new game state arrives. Override to refresh panel content.</summary>
        public abstract void Refresh(SaveParser.GameState state);
    }
}
