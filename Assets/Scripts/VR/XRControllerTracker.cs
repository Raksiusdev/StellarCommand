using UnityEngine;
using UnityEngine.XR;

namespace StellarCommand.VR
{
    /// <summary>
    /// Follows one tracked controller using Unity's XR input API (works with OpenXR / Meta Quest Link,
    /// no Input System package needed) and exposes its buttons, thumbstick and a pointer ray.
    /// Put it on an object that is a sibling of the head camera, so tracking-space poses line up.
    /// </summary>
    [DefaultExecutionOrder(-100)] // read the controller before anything that uses its state this frame
    public class XRControllerTracker : MonoBehaviour
    {
        // OpenXR also reports the "aim" pose under these names; when missing we fall back to the grip pose.
        private static readonly InputFeatureUsage<Vector3> PointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        private static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");

        [Header("Setup")]
        public XRNode node = XRNode.RightHand;
        [Tooltip("Object moved to the pointer pose each frame (parent of the ray line).")]
        public Transform pointer;
        public LineRenderer ray;

        [Header("Pointer fallback")]
        [Tooltip("Used only when the runtime does not report an aim pose. Tilts the ray relative to the grip pose; " +
                 "adjust until the ray points where the controller points.")]
        public float fallbackPitchDegrees = 35f;
        public float rayLength = 3f;

        public bool IsTracked { get; private set; }
        public Vector2 Stick { get; private set; }
        public float Trigger { get; private set; }
        public float Grip { get; private set; }

        /// <summary>Analogue value above which trigger/grip count as pressed when no pressed flag is reported.</summary>
        private const float PressThreshold = 0.75f;
        public bool TriggerHeld { get; private set; }
        public bool TriggerDown { get; private set; }
        public bool GripHeld { get; private set; }
        public bool GripDown { get; private set; }
        public bool GripUp { get; private set; }
        public bool PrimaryButtonDown { get; private set; }
        public bool SecondaryButtonDown { get; private set; }
        public bool MenuButtonDown { get; private set; }
        public Ray PointerRay { get; private set; }

        private InputDevice _device;
        private bool _wasTrigger, _wasGrip, _wasPrimary, _wasSecondary, _wasMenu;
        private bool _deviceHasTrigger;
        private float _nextDeviceCheck;

        // GetDeviceAtXRNode returns one device per hand, but a runtime can expose several for the same hand
        // (the controller, a hand-tracking "aim" device, ...) and the one it picks may have no trigger or grip.
        // So prefer a controller that actually reports a trigger, and keep looking until we have one.
        private InputDevice FindDevice()
        {
            var side = node == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | side, devices);

            foreach (var d in devices)
                if (d.TryGetFeatureValue(CommonUsages.trigger, out _)) return d;

            return devices.Count > 0 ? devices[0] : InputDevices.GetDeviceAtXRNode(node);
        }

        private void LogDevice(InputDevice device)
        {
            var usages = new System.Collections.Generic.List<InputFeatureUsage>();
            device.TryGetFeatureUsages(usages);
            var names = new System.Collections.Generic.List<string>();
            foreach (var u in usages) names.Add(u.name);
            Debug.Log($"[StellarCommand] {name} uses XR device '{device.name}' ({device.characteristics}). " +
                      $"Features: {string.Join(", ", names)}.");
        }

        private void Update()
        {
            if (!_device.isValid || (!_deviceHasTrigger && Time.unscaledTime >= _nextDeviceCheck))
            {
                _nextDeviceCheck = Time.unscaledTime + 2f;
                var found = FindDevice();
                if (found.isValid && (!_device.isValid || found.name != _device.name))
                {
                    _device = found;
                    LogDevice(_device);
                }
                _deviceHasTrigger = _device.isValid && _device.TryGetFeatureValue(CommonUsages.trigger, out _);
            }

            if (!_device.isValid)
            {
                MarkLost();
                return;
            }

            IsTracked = _device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
            if (!IsTracked)
            {
                MarkLost();
                return;
            }

            if (_device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 gripPos) &&
                _device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion gripRot))
            {
                transform.localPosition = gripPos;
                transform.localRotation = gripRot;
            }

            UpdatePointer(transform.localPosition, transform.localRotation);
            ReadButtons();
        }

        private void UpdatePointer(Vector3 gripLocalPos, Quaternion gripLocalRot)
        {
            Vector3 localPos = gripLocalPos;
            Quaternion localRot = gripLocalRot * Quaternion.Euler(fallbackPitchDegrees, 0f, 0f);

            if (_device.TryGetFeatureValue(PointerPosition, out Vector3 aimPos) &&
                _device.TryGetFeatureValue(PointerRotation, out Quaternion aimRot))
            {
                localPos = aimPos;
                localRot = aimRot;
            }

            Transform space = transform.parent;
            Vector3 worldPos = space != null ? space.TransformPoint(localPos) : localPos;
            Quaternion worldRot = space != null ? space.rotation * localRot : localRot;

            PointerRay = new Ray(worldPos, worldRot * Vector3.forward);
            if (pointer != null) pointer.SetPositionAndRotation(worldPos, worldRot);
        }

        private void ReadButtons()
        {
            _device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            Stick = stick;

            _device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            Trigger = trigger;

            _device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton);
            _device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton);

            // Some runtimes only report the analogue value, never the pressed flag, so accept either
            _device.TryGetFeatureValue(CommonUsages.grip, out float gripValue);
            Grip = gripValue;
            triggerButton |= trigger > PressThreshold;
            gripButton |= gripValue > PressThreshold;
            _device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary);
            _device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary);
            _device.TryGetFeatureValue(CommonUsages.menuButton, out bool menu);

            MenuButtonDown = menu && !_wasMenu;
            _wasMenu = menu;
            TriggerHeld = triggerButton;
            TriggerDown = triggerButton && !_wasTrigger;
            GripHeld = gripButton;
            GripDown = gripButton && !_wasGrip;
            GripUp = !gripButton && _wasGrip;
            PrimaryButtonDown = primary && !_wasPrimary;
            SecondaryButtonDown = secondary && !_wasSecondary;

            _wasTrigger = triggerButton;
            _wasGrip = gripButton;
            _wasPrimary = primary;
            _wasSecondary = secondary;
        }

        private void MarkLost()
        {
            IsTracked = false;
            Stick = Vector2.zero;
            Trigger = 0f;
            TriggerHeld = TriggerDown = GripHeld = GripDown = PrimaryButtonDown = SecondaryButtonDown = MenuButtonDown = false;
            GripUp = _wasGrip;
            _wasTrigger = _wasGrip = _wasPrimary = _wasSecondary = _wasMenu = false;
            if (pointer != null && pointer.gameObject.activeSelf) pointer.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (pointer != null && IsTracked && !pointer.gameObject.activeSelf)
                pointer.gameObject.SetActive(true);
        }

        /// <summary>Shortens or lengthens the visible ray (e.g. to stop at a hovered star).</summary>
        public void SetRayLength(float length)
        {
            if (ray != null) ray.SetPosition(1, new Vector3(0f, 0f, length));
        }

        public void ResetRayLength() => SetRayLength(rayLength);

        /// <summary>Short vibration for feedback; silently ignored if the device cannot buzz.</summary>
        public void Haptic(float amplitude = 0.25f, float seconds = 0.03f)
        {
            if (!_device.isValid) return;
            _device.TryGetHapticCapabilities(out HapticCapabilities caps);
            if (caps.supportsImpulse) _device.SendHapticImpulse(0, amplitude, seconds);
        }
    }
}
