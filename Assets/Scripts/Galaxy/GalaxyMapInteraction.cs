using TMPro;
using UnityEngine;
using StellarCommand.Game;
using StellarCommand.SaveParser;
using StellarCommand.VR;

namespace StellarCommand.Galaxy
{
    /// <summary>
    /// Controller interaction with the galaxy hologram.
    ///   Point        : the ray snaps to the nearest star; a label shows its name, owner and star class.
    ///   Right stick  : X turns the map, Y zooms.
    ///   Left stick   : pans the map across the table.
    ///   Grip (one)   : grab and drag to turn the map; hold both grips and move hands apart/together to zoom.
    ///   Primary (A/X): reset turn, zoom and pan.
    /// </summary>
    public class GalaxyMapInteraction : MonoBehaviour
    {
        [Header("References")]
        public GalaxyMap map;
        public XRControllerTracker rightHand;
        public XRControllerTracker leftHand;
        public TextMeshPro label;
        public Transform marker;

        [Header("Label")]
        [Tooltip("Overall size of the hover label. Lower = smaller text.")]
        public float labelScale = 0.1f;
        [Tooltip("Metres above the star where the label floats.")]
        public float labelHeight = 0.05f;

        [Header("Picking")]
        [Tooltip("Cone half-angle around the ray: a star within this angle can be selected, so far stars are as easy as near ones.")]
        public float pickAngleDegrees = 2.5f;
        [Tooltip("Minimum pick radius in metres, so stars right next to the controller are still easy to hit.")]
        public float minPickRadius = 0.015f;
        [Tooltip("The star already selected stays selected while the ray is within this multiple of the pick cone, " +
                 "so the selection does not flicker between neighbours.")]
        public float stickiness = 1.8f;

        [Header("Stick controls")]
        public float turnDegreesPerSecond = 100f;
        public float zoomPerSecond = 0.9f;
        public float panMetersPerSecond = 0.5f;
        public float stickDeadzone = 0.18f;
        public bool invertTurn = false;
        public Vector2 zoomLimits = new Vector2(0.6f, 6f);

        private int _hovered = -1;
        private float _zoom = 1f;

        // grab state
        private bool _grabbing;
        private XRControllerTracker _grabHand;
        private float _lastGrabAngle;
        private bool _pinching;
        private float _pinchStartDistance, _pinchStartZoom;

        private Camera _camera;

        private void Start()
        {
            ConfigureLabel();
        }

        // Done at runtime so it also fixes labels created by older builds of the scene.
        // The rect is measured in the same units as the font size, so it has to be wide:
        // a narrow rect makes TMP wrap the text letter by letter.
        private void ConfigureLabel()
        {
            if (label == null) return;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(16f, 4f);
        }

        private void Update()
        {
            if (map == null || map.Rotor == null) return;
            if (_camera == null) _camera = Camera.main;

            UpdateHover();
            UpdateSticks();
            UpdateGrab();

            if ((Free(rightHand) && rightHand.PrimaryButtonDown) || (Free(leftHand) && leftHand.PrimaryButtonDown))
                ResetView();
        }

        // A hand pointing at the game screen belongs to the game, not to the map
        private static bool Free(XRControllerTracker hand) => hand != null && !GameScreenInput.Captures(hand);

        // ------------------------------------------------------------------ hover

        private void UpdateHover()
        {
            int best = -1;
            float bestDistance = 0f;
            XRControllerTracker bestHand = null;

            foreach (var hand in new[] { rightHand, leftHand })
            {
                if (hand == null || !hand.IsTracked || GameScreenInput.Captures(hand)) continue;
                if (map.HasData && TryPick(hand, out int index, out float distance))
                {
                    // Prefer the right hand when both point at something
                    if (best < 0) { best = index; bestDistance = distance; bestHand = hand; }
                }
                else
                {
                    hand.ResetRayLength();
                }
            }

            if (best != _hovered)
            {
                _hovered = best;
                if (bestHand != null) bestHand.Haptic(0.2f, 0.02f);
            }

            if (_hovered >= 0 && bestHand != null)
            {
                bestHand.SetRayLength(bestDistance);
                ShowLabel(_hovered);
            }
            else
            {
                HideLabel();
            }
        }

        private bool TryPick(XRControllerTracker hand, out int index, out float distance)
        {
            index = -1;
            distance = 0f;

            Transform rotor = map.Rotor;
            Ray ray = hand.PointerRay;
            Vector3 origin = rotor.InverseTransformPoint(ray.origin);
            Vector3 direction = rotor.InverseTransformDirection(ray.direction).normalized;

            float scale = Mathf.Max(0.0001f, rotor.lossyScale.x);
            float tan = Mathf.Tan(pickAngleDegrees * Mathf.Deg2Rad);
            float minLocal = minPickRadius / scale;

            // Score = (miss distance / allowed distance)^2, so 1.0 is the edge of the pick cone.
            // Comparing scores rather than raw distances keeps far stars (wide cone) from stealing
            // the selection from nearer ones.
            float bestScore = 1f;
            int bestIndex = -1;
            float bestAlong = 0f;
            float currentScore = float.MaxValue;
            float currentAlong = 0f;

            for (int i = 0; i < map.SystemCount; i++)
            {
                Vector3 toStar = map.GetLocalPosition(i) - origin;
                float along = Vector3.Dot(toStar, direction);
                if (along < 0f) continue;

                float allowed = Mathf.Max(minLocal, along * tan);
                float score = (toStar.sqrMagnitude - along * along) / (allowed * allowed);
                bool isCurrent = i == _hovered;
                if (score >= bestScore && !(isCurrent && score < stickiness)) continue;

                // ignore stars the clip has faded out
                if (!InsideClip(rotor.TransformPoint(map.GetLocalPosition(i)))) continue;

                if (isCurrent)
                {
                    currentScore = score;
                    currentAlong = along;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                    bestAlong = along;
                }
            }

            // Keep the current selection unless something is clearly closer to the ray
            if (_hovered >= 0 && currentScore < stickiness && (bestIndex < 0 || bestScore > currentScore * 0.5f))
            {
                index = _hovered;
                distance = currentAlong * scale;
                return true;
            }

            index = bestIndex;
            distance = bestAlong * scale;
            return index >= 0;
        }

        private bool InsideClip(Vector3 world)
        {
            Transform root = map.transform;
            Vector3 d = world - root.position;
            Vector3 n = root.up;
            float height = Vector3.Dot(d, n);
            float radial = (d - n * height).magnitude;
            float limit = map.radiusMeters * map.clipRadiusFraction * Mathf.Abs(root.lossyScale.x);
            return radial < limit * 0.95f && Mathf.Abs(height) < map.radiusMeters * 0.5f;
        }

        private void ShowLabel(int index)
        {
            StarSystem system = map.GetSystem(index);
            Vector3 world = map.Rotor.TransformPoint(map.GetLocalPosition(index));

            if (marker != null)
            {
                marker.gameObject.SetActive(true);
                marker.position = world;
            }

            if (label == null) return;

            string owner;
            if (system.OwnerId < 0) owner = "<color=#7A8FA6>Unclaimed</color>";
            else if (map.IsPlayer(system.OwnerId)) owner = "<color=#40F2FF>" + map.GetEmpireName(system.OwnerId) + " (you)</color>";
            else owner = map.GetEmpireName(system.OwnerId);

            label.text = $"<b>{system.Name.ToUpper()}</b>\n{owner}\n<color=#9FB6C8>{StarClassName(system.StarClass)}</color>";
            label.gameObject.SetActive(true);

            Transform t = label.transform;
            Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;
            t.position = world + up * labelHeight;
            if (_camera != null)
            {
                t.rotation = Quaternion.LookRotation(t.position - _camera.transform.position, up);

                // Keep the label about the same apparent size whether the star is near or far
                float distance = Vector3.Distance(t.position, _camera.transform.position);
                t.localScale = Vector3.one * (labelScale * Mathf.Clamp(distance / 1.5f, 0.7f, 1.6f));
            }
            else
            {
                t.localScale = Vector3.one * labelScale;
            }
        }

        private void HideLabel()
        {
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
        }

        private static string StarClassName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Unknown star";
            string s = key.StartsWith("sc_") ? key.Substring(3) : key;
            switch (s)
            {
                case "black_hole": return "Black hole";
                case "neutron_star": return "Neutron star";
                case "pulsar": return "Pulsar";
                case "m_giant": return "Red giant";
                case "binary_1": case "binary_2": case "binary_3": return "Binary system";
                case "trinary_1": case "trinary_2": return "Trinary system";
                case "ringworld_seam_system": return "Ring world";
            }
            if (s.Length == 1) return s.ToUpper() + "-class star";
            return (char.ToUpper(s[0]) + s.Substring(1)).Replace('_', ' ');
        }

        // ------------------------------------------------------------------ sticks

        private void UpdateSticks()
        {
            float dt = Time.deltaTime;
            Transform rotor = map.Rotor;

            if (Free(rightHand) && rightHand.IsTracked)
            {
                // Pushing the stick straight up always leaks a little sideways; keep only the dominant
                // axis so zooming does not also turn the map (and turning does not zoom).
                Vector2 s = DominantAxis(Deadzone(rightHand.Stick));
                if (s.x != 0f)
                    rotor.Rotate(0f, (invertTurn ? -1f : 1f) * s.x * turnDegreesPerSecond * dt, 0f, Space.Self);
                if (s.y != 0f)
                {
                    // Zoom towards the selected star (or the map centre when nothing is selected)
                    Vector3 pivot = _hovered >= 0
                        ? rotor.TransformPoint(map.GetLocalPosition(_hovered))
                        : rotor.position;
                    SetZoom(_zoom * (1f + s.y * zoomPerSecond * dt), pivot);
                }
            }

            if (Free(leftHand) && leftHand.IsTracked)
            {
                Vector2 s = Deadzone(leftHand.Stick);
                if (s != Vector2.zero)
                {
                    rotor.localPosition += new Vector3(s.x, 0f, s.y) * panMetersPerSecond * dt;
                    ClampPan();
                }
            }
        }

        // The further you zoom in, the further the map may be dragged from the table centre
        private void ClampPan()
        {
            Transform rotor = map.Rotor;
            float limit = map.radiusMeters * 0.9f * Mathf.Max(1f, _zoom);
            Vector3 pos = rotor.localPosition;
            if (pos.magnitude > limit) rotor.localPosition = pos.normalized * limit;
        }

        private Vector2 Deadzone(Vector2 v)
        {
            if (v.magnitude < stickDeadzone) return Vector2.zero;
            return v;
        }

        // Keeps only the stronger axis unless the stick is clearly pushed diagonally
        private static Vector2 DominantAxis(Vector2 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y);
            if (y > x * 1.6f) return new Vector2(0f, v.y);
            if (x > y * 1.6f) return new Vector2(v.x, 0f);
            return v;
        }

        private void SetZoom(float zoom, Vector3? pivotWorld = null)
        {
            float old = _zoom;
            _zoom = Mathf.Clamp(zoom, zoomLimits.x, zoomLimits.y);
            float ratio = _zoom / old;
            map.Rotor.localScale = Vector3.one * _zoom;

            if (Mathf.Approximately(ratio, 1f)) return;

            if (ratio > 1f && pivotWorld.HasValue)
            {
                // Zooming in: scaling happens around the rotor origin, so shift the origin to keep the
                // pivot point (the selected star) fixed under the ray
                Vector3 p = map.transform.InverseTransformPoint(pivotWorld.Value);
                map.Rotor.localPosition = p - (p - map.Rotor.localPosition) * ratio;
            }
            else
            {
                // Zooming out: let the offset shrink with the zoom, so the map drifts back to the table
                // centre instead of staying where the last zoom-in left it
                map.Rotor.localPosition *= ratio;
            }

            // At normal zoom (or less) the whole galaxy fits on the table, so it is always centred
            if (_zoom <= 1f) map.Rotor.localPosition = Vector3.zero;
            ClampPan();
        }

        // ------------------------------------------------------------------ grab

        private void UpdateGrab()
        {
            bool right = Free(rightHand) && rightHand.IsTracked && rightHand.GripHeld;
            bool left = Free(leftHand) && leftHand.IsTracked && leftHand.GripHeld;

            if (right && left)
            {
                _grabbing = false;
                float distance = Vector3.Distance(rightHand.transform.position, leftHand.transform.position);
                if (!_pinching)
                {
                    _pinching = true;
                    _pinchStartDistance = Mathf.Max(0.05f, distance);
                    _pinchStartZoom = _zoom;
                }
                else
                {
                    SetZoom(_pinchStartZoom * (distance / _pinchStartDistance));
                }
                return;
            }

            _pinching = false;

            XRControllerTracker hand = right ? rightHand : left ? leftHand : null;
            if (hand == null)
            {
                _grabbing = false;
                return;
            }

            float angle = HandAngle(hand);
            if (!_grabbing || _grabHand != hand)
            {
                _grabbing = true;
                _grabHand = hand;
                _lastGrabAngle = angle;
                hand.Haptic(0.3f, 0.03f);
                return;
            }

            float delta = Mathf.DeltaAngle(_lastGrabAngle, angle);
            _lastGrabAngle = angle;
            map.Rotor.Rotate(0f, delta, 0f, Space.Self);
        }

        // Angle of the hand around the map centre, measured in the map's own plane
        private float HandAngle(XRControllerTracker hand)
        {
            Vector3 p = map.transform.InverseTransformPoint(hand.transform.position);
            return Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
        }

        private void ResetView()
        {
            map.Rotor.localRotation = Quaternion.identity;
            map.Rotor.localPosition = Vector3.zero;
            SetZoom(1f);
        }
    }
}
