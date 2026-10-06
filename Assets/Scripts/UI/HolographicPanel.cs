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

        private Camera _mainCam;
        private float _timer;
        private bool _active;

        protected virtual void Awake()
        {
            _mainCam = Camera.main;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (titleLabel != null) titleLabel.text = panelTitle;
        }

        protected virtual void OnEnable()
        {
            // Start invisible, fade in after delay
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            _active = false;
            _timer = 0f;
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
                canvasGroup.alpha = Mathf.Lerp(pulseMinAlpha, 1f, pulse);
            }
        }

        protected virtual void OnPanelActivated()
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        /// <summary>Called when new game state arrives. Override to refresh panel content.</summary>
        public abstract void Refresh(SaveParser.GameState state);
    }
}
