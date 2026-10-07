using UnityEngine;
using uWindowCapture;

namespace StellarCommand.Game
{
    /// <summary>
    /// The main screen of the bridge: shows the live Stellaris window (captured with uWindowCapture).
    /// The window is found by its exact title. The screen builds its own mesh, a section of a cylinder
    /// centred on the player (so every point is the same distance from the eyes), and rebuilds it whenever
    /// the game window changes size, so any game resolution works without configuration.
    ///
    /// Place the object at the height of the screen's centre, at the room's centre, and turn it (Y rotation)
    /// to choose which direction the screen sits in.
    /// </summary>
    [RequireComponent(typeof(UwcWindowTexture), typeof(MeshFilter), typeof(MeshCollider))]
    public class GameScreen : MonoBehaviour
    {
        [Header("Window")]
        [Tooltip("Exact title of the game window.")]
        public string windowTitle = "Stellaris";
        [Tooltip("How often to look for the game window while it is not captured.")]
        public float searchIntervalSeconds = 1f;

        [Header("Shape")]
        [Tooltip("Curve the screen around the player. Off = a flat panel.")]
        public bool curved = true;
        [Tooltip("Distance from the player to the screen, in metres.")]
        public float radiusMeters = 3.4f;
        [Tooltip("How much of the circle the screen covers when curved. The height follows the game's aspect ratio.")]
        public float arcDegrees = 70f;
        [Tooltip("Width of the screen in metres when it is flat.")]
        public float flatWidthMeters = 4.8f;
        [Range(8, 128)] public int segments = 48;

        public UwcWindowTexture Source { get; private set; }
        public UwcWindow Window => Source != null ? Source.window : null;
        public bool HasWindow => Source != null && Source.isValid && Window != null && Window.width > 0 && Window.height > 0;

        private MeshFilter _filter;
        private MeshCollider _collider;
        private Mesh _mesh;
        private float _searchTimer;
        private bool _loggedMissing;
        private int _lastWidth, _lastHeight;

        private void Awake()
        {
            Source = GetComponent<UwcWindowTexture>();
            _filter = GetComponent<MeshFilter>();
            _collider = GetComponent<MeshCollider>();

            // We pick the window ourselves (exact title), so the library must not search on its own
            Source.searchTiming = WindowSearchTiming.Manual;
            Source.scaleControlType = WindowTextureScaleControlType.Manual;

            BuildMesh(16f / 9f);
        }

        private void Start()
        {
            // Window titles must be refreshed regularly or the game window can never be matched
            UwcManager.instance.windowTitlesUpdateTiming = WindowTitlesUpdateTiming.AlwaysAltTabWindows;
        }

        private void Update()
        {
            if (!HasWindow || !IsTargetWindow(Window))
            {
                _searchTimer -= Time.unscaledDeltaTime;
                if (_searchTimer <= 0f)
                {
                    _searchTimer = searchIntervalSeconds;
                    FindGameWindow();
                }
            }

            if (HasWindow) UpdateSize();
        }

        private void FindGameWindow()
        {
            foreach (var window in UwcManager.windows.Values)
            {
                if (window == null || !window.isValid || window.isChild || !window.isAltTabWindow) continue;
                if (!IsTargetWindow(window)) continue;

                Source.window = window;
                _loggedMissing = false;
                Debug.Log($"[StellarCommand] Capturing '{window.title}' ({window.width}x{window.height}).");
                return;
            }

            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Debug.LogWarning($"[StellarCommand] No window titled '{windowTitle}' found. " +
                                 "Start Stellaris in windowed mode (not exclusive fullscreen) and it will be picked up automatically.");
            }
        }

        private bool IsTargetWindow(UwcWindow window)
        {
            return window != null && window.title != null &&
                   string.Equals(window.title.Trim(), windowTitle, System.StringComparison.OrdinalIgnoreCase);
        }

        // Follow the game's resolution: rebuild the screen whenever the window size changes
        private void UpdateSize()
        {
            int w = Window.width, h = Window.height;
            if (w == _lastWidth && h == _lastHeight) return;

            _lastWidth = w;
            _lastHeight = h;
            BuildMesh((float)w / h);
            Debug.Log($"[StellarCommand] Game window is {w}x{h}; screen rebuilt ({_mesh.bounds.size.x:0.##} x {_mesh.bounds.size.y:0.##} m).");
        }

        /// <summary>
        /// Builds the screen mesh for a window aspect ratio (width / height). Local +Z points from the
        /// player towards the screen's centre, and the surface faces the player.
        /// </summary>
        public void BuildMesh(float aspect)
        {
            // Also called from the editor builder, where Awake has not run yet
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_collider == null) _collider = GetComponent<MeshCollider>();

            int seg = curved ? Mathf.Max(2, segments) : 1;
            float arc = arcDegrees * Mathf.Deg2Rad;
            float width = curved ? radiusMeters * arc : flatWidthMeters;
            float height = width / Mathf.Max(0.1f, aspect);

            var verts = new Vector3[(seg + 1) * 2];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 6];

            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                float x, z;
                Vector3 normal;
                if (curved)
                {
                    float phi = (t - 0.5f) * arc;
                    x = Mathf.Sin(phi) * radiusMeters;
                    z = Mathf.Cos(phi) * radiusMeters;
                    normal = new Vector3(-Mathf.Sin(phi), 0f, -Mathf.Cos(phi)); // towards the player
                }
                else
                {
                    x = (t - 0.5f) * width;
                    z = radiusMeters;
                    normal = Vector3.back;
                }

                int b = i * 2;
                verts[b] = new Vector3(x, -height * 0.5f, z);
                verts[b + 1] = new Vector3(x, height * 0.5f, z);
                normals[b] = normals[b + 1] = normal;
                uvs[b] = new Vector2(t, 0f);
                uvs[b + 1] = new Vector2(t, 1f);
            }

            // Clockwise when seen from the player's side, which Unity treats as the front face
            for (int i = 0; i < seg; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                int k = i * 6;
                tris[k] = b0; tris[k + 1] = t0; tris[k + 2] = b1;
                tris[k + 3] = t0; tris[k + 4] = t1; tris[k + 5] = b1;
            }

            if (_mesh == null) _mesh = new Mesh { name = "GameScreen" };
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();

            _filter.sharedMesh = _mesh;
            _collider.sharedMesh = null; // force the collider to re-bake the new shape
            _collider.sharedMesh = _mesh;
        }

        /// <summary>Converts a point on the screen (0..1, origin at the bottom-left) to desktop pixels.</summary>
        public Vector2Int UvToDesktop(Vector2 uv)
        {
            var window = Window;
            int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * window.width), 0, window.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((1f - uv.y) * window.height), 0, window.height - 1);
            return new Vector2Int(window.x + x, window.y + y);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
