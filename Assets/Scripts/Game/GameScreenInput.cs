using System.Collections.Generic;
using UnityEngine;
using StellarCommand.VR;

namespace StellarCommand.Game
{
    /// <summary>
    /// Turns the VR controllers into mouse and keyboard for the real game window.
    /// While a controller's ray is on the game screen it "owns" that hand (see Captures), so the
    /// galaxy map ignores that hand's buttons.
    ///
    ///   Trigger       left click          Grip          right click
    ///   Right stick   mouse wheel (zoom)  Left stick     W A S D (pan the game camera)
    ///   A             Space (pause)       B             Escape
    /// </summary>
    [DefaultExecutionOrder(-50)] // after the controller trackers, before anything that reads Captures()
    public class GameScreenInput : MonoBehaviour
    {
        [Header("References")]
        public GameScreen screen;
        public XRControllerTracker rightHand;
        public XRControllerTracker leftHand;
        [Tooltip("Small marker shown where the controller points on the screen (optional).")]
        public Transform pointerDot;

        [Header("Pointing")]
        public float maxDistance = 15f;

        [Header("Controls")]
        public float wheelNotchesPerSecond = 10f;
        [Range(0.2f, 0.9f)] public float stickThreshold = 0.5f;

        private static readonly HashSet<XRControllerTracker> CapturedHands = new HashSet<XRControllerTracker>();

        /// <summary>True while this hand points at the game screen and drives the game.</summary>
        public static bool Captures(XRControllerTracker hand) => hand != null && CapturedHands.Contains(hand);

        private class HandState
        {
            public bool left, right;
            public float leftDownAt, rightDownAt;
            public bool w, a, s, d;
        }

        [Tooltip("A click is held at least this long. Games that poll the button once per frame can miss a very short tap.")]
        public float minClickSeconds = 0.06f;

        private readonly Dictionary<XRControllerTracker, HandState> _states = new Dictionary<XRControllerTracker, HandState>();
        private float _wheelAccumulator;
        private float _lastFocusAttempt = -10f;

        [Header("Safety")]
        [Tooltip("While off, the controllers never touch the mouse or keyboard. Toggle with the LEFT controller menu button (≡).")]
        public bool inputEnabled = true;
        [Tooltip("Emergency exit: hold Ctrl + Shift + F12 on the keyboard to release the game and stop Play mode.")]
        public bool panicHotkey = true;

        private const int VkShift = 0x10, VkControl = 0x11, VkF12 = 0x7B;

        private void OnDisable()
        {
            foreach (var kv in _states) ReleaseAll(kv.Value);
            CapturedHands.Clear();
        }

        // Emergency exit that works even while the game window has focus
        private void CheckPanic()
        {
            if (!panicHotkey) return;
            if (!(Win32Input.IsKeyDown(VkControl) && Win32Input.IsKeyDown(VkShift) && Win32Input.IsKeyDown(VkF12))) return;

            inputEnabled = false;
            foreach (var kv in _states) ReleaseAll(kv.Value);
            CapturedHands.Clear();
            Win32Input.FocusThisApp();
            Debug.LogWarning("[StellarCommand] Panic hotkey pressed: game input released.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            CheckPanic();

            // Left menu button toggles whether the controllers drive the game at all
            if (leftHand != null && leftHand.MenuButtonDown)
            {
                inputEnabled = !inputEnabled;
                if (!inputEnabled) foreach (var kv in _states) ReleaseAll(kv.Value);
                if (leftHand != null) leftHand.Haptic(0.5f, inputEnabled ? 0.05f : 0.2f);
                Debug.Log($"[StellarCommand] Game input {(inputEnabled ? "ON" : "OFF")}.");
            }

            CapturedHands.Clear();
            if (!inputEnabled)
            {
                if (pointerDot != null && pointerDot.gameObject.activeSelf) pointerDot.gameObject.SetActive(false);
                return;
            }

            bool pointerShown = false;

            foreach (var hand in new[] { rightHand, leftHand })
            {
                if (hand == null) continue;
                if (!_states.TryGetValue(hand, out var state))
                    _states[hand] = state = new HandState();

                RaycastHit h = default;
                bool onScreen = screen != null && screen.HasWindow && hand.IsTracked && TryHit(hand, out h);

                if (!onScreen)
                {
                    // Pointing away: release anything still held so nothing gets stuck in the game
                    ReleaseAll(state);
                    LogOffScreen(hand);
                    continue;
                }

                CapturedHands.Add(hand);
                hand.SetRayLength(h.distance);
                ShowPointer(h, ref pointerShown);

                Vector2Int desktop = screen.UvToDesktop(h.textureCoord);
                Win32Input.MoveCursor(desktop.x, desktop.y);
                EnsureGameFocus();
                LogOnScreen(hand, h, desktop);

                DriveMouse(hand, state);
                DriveKeys(hand, state);
            }

            if (pointerDot != null && !pointerShown && pointerDot.gameObject.activeSelf)
                pointerDot.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- diagnostics

        [Header("Diagnostics")]
        [Tooltip("Logs what the controller ray is doing every few seconds (filter the Console by 'StellarCommand').")]
        public bool logDiagnostics = false;
        public float logIntervalSeconds = 3f;

        private float _nextOnLog, _nextOffLog;

        // Why is the hand NOT driving the game? Report the first reason in the chain.
        private void LogOffScreen(XRControllerTracker hand)
        {
            if (!logDiagnostics || Time.unscaledTime < _nextOffLog) return;
            _nextOffLog = Time.unscaledTime + logIntervalSeconds;

            string name = hand.name;
            if (screen == null) { Debug.Log($"[StellarCommand] {name}: no GameScreen assigned."); return; }
            if (!screen.HasWindow) { Debug.Log($"[StellarCommand] {name}: the game window is not captured yet."); return; }
            if (!hand.IsTracked) { Debug.Log($"[StellarCommand] {name}: controller not tracked."); return; }

            // Tracked and window ready, so the ray simply misses the screen: say what it hits instead
            var ray = hand.PointerRay;
            string what = Physics.Raycast(ray, out RaycastHit other, maxDistance)
                ? $"hits '{other.collider.name}' at {other.distance:0.0} m"
                : "hits nothing";
            Debug.Log($"[StellarCommand] {name}: ray {what} (origin {ray.origin:0.0}, direction {ray.direction:0.00}); " +
                      "the game screen is not under the ray.");
        }

        private void LogClick(XRControllerTracker hand, string what)
        {
            if (!logDiagnostics) return;
            var cursor = Win32Input.GetCursor();
            Debug.Log($"[StellarCommand] {hand.name}: mouse {what} sent at ({cursor.X},{cursor.Y}); " +
                      $"trigger {hand.Trigger:0.00}, grip {hand.Grip:0.00}; " +
                      $"game has focus: {Win32Input.IsForeground(screen.Window.handle)}.");
        }

        // The ray is on the screen: check that Windows really moved the cursor and who has focus
        private void LogOnScreen(XRControllerTracker hand, RaycastHit hit, Vector2Int target)
        {
            if (!logDiagnostics || Time.unscaledTime < _nextOnLog) return;
            _nextOnLog = Time.unscaledTime + logIntervalSeconds;

            var cursor = Win32Input.GetCursor();
            bool focused = Win32Input.IsForeground(screen.Window.handle);
            Debug.Log($"[StellarCommand] {hand.name} on game screen: uv {hit.textureCoord:0.00}, " +
                      $"target desktop ({target.x},{target.y}), actual cursor ({cursor.X},{cursor.Y}), " +
                      $"game window at ({screen.Window.x},{screen.Window.y}) size {screen.Window.width}x{screen.Window.height}, " +
                      $"game has focus: {focused}, trigger {hand.Trigger:0.00} (held: {hand.TriggerHeld}), grip {hand.Grip:0.00}.");
        }

        private bool TryHit(XRControllerTracker hand, out RaycastHit hit)
        {
            if (Physics.Raycast(hand.PointerRay, out hit, maxDistance) &&
                hit.collider != null && hit.collider.gameObject == screen.gameObject)
                return true;

            hit = default;
            return false;
        }

        private void ShowPointer(RaycastHit hit, ref bool shown)
        {
            if (pointerDot == null) return;
            pointerDot.gameObject.SetActive(true);
            pointerDot.position = hit.point + hit.normal * 0.01f;
            shown = true;
        }

        // The game only reacts to clicks and keys while its window has focus
        private void EnsureGameFocus()
        {
            var handle = screen.Window.handle;
            if (Win32Input.IsForeground(handle)) return;
            if (Time.unscaledTime - _lastFocusAttempt < 1f) return;

            _lastFocusAttempt = Time.unscaledTime;
            Win32Input.BringToFront(handle);
        }

        private void DriveMouse(XRControllerTracker hand, HandState state)
        {
            float now = Time.unscaledTime;

            // Trigger = left button
            if (hand.TriggerHeld && !state.left)
            {
                Win32Input.MouseDown(Win32Input.MouseButton.Left);
                state.left = true;
                state.leftDownAt = now;
                LogClick(hand, "LEFT down");
            }
            else if (!hand.TriggerHeld && state.left && now - state.leftDownAt >= minClickSeconds)
            {
                Win32Input.MouseUp(Win32Input.MouseButton.Left);
                state.left = false;
                LogClick(hand, "LEFT up");
            }

            // Grip = right button
            if (hand.GripHeld && !state.right)
            {
                Win32Input.MouseDown(Win32Input.MouseButton.Right);
                state.right = true;
                state.rightDownAt = now;
                LogClick(hand, "RIGHT down");
            }
            else if (!hand.GripHeld && state.right && now - state.rightDownAt >= minClickSeconds)
            {
                Win32Input.MouseUp(Win32Input.MouseButton.Right);
                state.right = false;
                LogClick(hand, "RIGHT up");
            }

            // Right stick up/down = wheel
            if (hand == rightHand && Mathf.Abs(hand.Stick.y) > stickThreshold)
            {
                _wheelAccumulator += hand.Stick.y * wheelNotchesPerSecond * Time.unscaledDeltaTime;
                int notches = (int)_wheelAccumulator;
                if (notches != 0)
                {
                    Win32Input.Wheel(notches);
                    _wheelAccumulator -= notches;
                }
            }
        }

        private void DriveKeys(XRControllerTracker hand, HandState state)
        {
            if (hand.PrimaryButtonDown) Win32Input.Tap(Win32Input.ScanSpace);
            if (hand.SecondaryButtonDown) Win32Input.Tap(Win32Input.ScanEscape);

            // Left stick = WASD, held while the stick is pushed
            if (hand == leftHand)
            {
                Vector2 s = hand.Stick;
                SetKey(ref state.w, s.y > stickThreshold, Win32Input.ScanW);
                SetKey(ref state.s, s.y < -stickThreshold, Win32Input.ScanS);
                SetKey(ref state.a, s.x < -stickThreshold, Win32Input.ScanA);
                SetKey(ref state.d, s.x > stickThreshold, Win32Input.ScanD);
            }
        }

        private static void SetKey(ref bool held, bool want, ushort scan)
        {
            if (want && !held) { Win32Input.KeyDown(scan); held = true; }
            else if (!want && held) { Win32Input.KeyUp(scan); held = false; }
        }

        private static void ReleaseAll(HandState state)
        {
            if (state.left) { Win32Input.MouseUp(Win32Input.MouseButton.Left); state.left = false; }
            if (state.right) { Win32Input.MouseUp(Win32Input.MouseButton.Right); state.right = false; }
            SetKey(ref state.w, false, Win32Input.ScanW);
            SetKey(ref state.a, false, Win32Input.ScanA);
            SetKey(ref state.s, false, Win32Input.ScanS);
            SetKey(ref state.d, false, Win32Input.ScanD);
        }
    }
}
