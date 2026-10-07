using System.Collections.Generic;
using UnityEngine;
using StellarCommand.SaveParser;

namespace StellarCommand.Galaxy
{
    /// <summary>
    /// 3D holographic galaxy: one glowing point per star system, hyperlanes between them,
    /// coloured by owning empire, plus faint guide rings. Rebuilt whenever a new save state arrives.
    /// Hierarchy: [GalaxyMap root: position + tilt] / Rotor (spins) / Stars + Lanes (mesh renderers).
    /// </summary>
    public class GalaxyMap : MonoBehaviour
    {
        [Header("Scene References")]
        public Transform rotor;
        public MeshFilter starsFilter;
        public MeshFilter lanesFilter;

        [Header("Layout")]
        [Tooltip("Radius of the hologram in metres.")]
        public float radiusMeters = 0.38f;
        [Tooltip("Random vertical scatter (fraction of the radius) so the disc has some depth.")]
        public float depthScatter = 0.03f;
        [Tooltip("Degrees per second the hologram turns. 0 = still.")]
        public float spinDegreesPerSecond = 3f;

        [Header("Star look")]
        public float unownedSize = 0.0035f;
        public float ownedSize = 0.0060f;
        public float playerSize = 0.0095f;
        public Color unownedColor = new Color(0.28f, 0.42f, 0.62f);
        public Color playerColor = new Color(0.25f, 0.95f, 1f);

        [Header("Lane look")]
        public Color neutralLaneColor = new Color(0.05f, 0.11f, 0.18f);
        [Range(0f, 1f)] public float ownedLaneStrength = 0.55f;
        public Color ringColor = new Color(0.04f, 0.16f, 0.2f);

        private Mesh _starMesh;
        private Mesh _laneMesh;

        private void Update()
        {
            if (rotor != null && spinDegreesPerSecond != 0f)
                rotor.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        public void Refresh(GameState state)
        {
            if (state == null || !state.IsValid || state.Systems.Count == 0) return;
            Build(state);
        }

        private void Build(GameState state)
        {
            var systems = state.Systems;

            // Centre and fit the galaxy into the requested radius
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var s in systems)
            {
                if (s.X < minX) minX = s.X;
                if (s.X > maxX) maxX = s.X;
                if (s.Y < minY) minY = s.Y;
                if (s.Y > maxY) maxY = s.Y;
            }
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            float maxR = 1f;
            foreach (var s in systems)
            {
                float dx = s.X - cx, dy = s.Y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > maxR) maxR = r;
            }
            float scale = radiusMeters / maxR;

            var positions = new Dictionary<int, Vector3>(systems.Count);
            foreach (var s in systems)
            {
                float jitter = (Hash01(s.Id) - 0.5f) * 2f * depthScatter * radiusMeters;
                positions[s.Id] = new Vector3((s.X - cx) * scale, jitter, (s.Y - cy) * scale);
            }

            BuildStars(systems, positions, state.PlayerCountryId);
            BuildLanes(systems, positions, state.PlayerCountryId, radiusMeters);
        }

        // ------------------------------------------------------------------ stars

        private void BuildStars(List<StarSystem> systems, Dictionary<int, Vector3> positions, int playerId)
        {
            int n = systems.Count;
            var verts = new List<Vector3>(n * 4);
            var uvs = new List<Vector2>(n * 4);
            var sizes = new List<Vector2>(n * 4); // uv1.x = quad radius in metres
            var colors = new List<Color>(n * 4);
            var tris = new List<int>(n * 6);

            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };

            foreach (var s in systems)
            {
                bool player = s.OwnerId >= 0 && s.OwnerId == playerId;
                bool owned = s.OwnerId >= 0;

                Color color = player ? playerColor : owned ? OwnerColor(s.OwnerId) : unownedColor;
                float size = player ? playerSize : owned ? ownedSize : unownedSize;
                color.a = 1f;

                int baseIndex = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(positions[s.Id]);
                    uvs.Add(corners[k]);
                    sizes.Add(new Vector2(size, 0f));
                    colors.Add(color);
                }
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
                tris.Add(baseIndex); tris.Add(baseIndex + 3); tris.Add(baseIndex + 2);
            }

            if (_starMesh == null) _starMesh = new Mesh { name = "GalaxyStars" };
            _starMesh.Clear();
            _starMesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _starMesh.SetVertices(verts);
            _starMesh.SetUVs(0, uvs);
            _starMesh.SetUVs(1, sizes);
            _starMesh.SetColors(colors);
            _starMesh.SetTriangles(tris, 0);
            _starMesh.RecalculateBounds();
            if (starsFilter != null) starsFilter.sharedMesh = _starMesh;
        }

        // ------------------------------------------------------------------ lanes + rings

        private void BuildLanes(List<StarSystem> systems, Dictionary<int, Vector3> positions, int playerId, float radius)
        {
            var ownerOf = new Dictionary<int, int>(systems.Count);
            foreach (var s in systems) ownerOf[s.Id] = s.OwnerId;

            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            foreach (var s in systems)
            {
                if (string.IsNullOrEmpty(s.HyperlaneTargets)) continue;
                foreach (var token in s.HyperlaneTargets.Split(','))
                {
                    if (!int.TryParse(token, out int other) || other <= s.Id) continue; // draw each lane once
                    if (!positions.TryGetValue(other, out var pb)) continue;

                    int ownerA = s.OwnerId;
                    int ownerB = ownerOf.TryGetValue(other, out int ob) ? ob : -1;

                    Color color = neutralLaneColor;
                    if (ownerA >= 0 && ownerA == ownerB)
                    {
                        Color own = ownerA == playerId ? playerColor : OwnerColor(ownerA);
                        color = own * ownedLaneStrength;
                    }
                    color.a = 1f;

                    indices.Add(verts.Count); verts.Add(positions[s.Id]); colors.Add(color);
                    indices.Add(verts.Count); verts.Add(pb); colors.Add(color);
                }
            }

            // Guide rings on the galactic plane
            foreach (float fraction in new[] { 0.25f, 0.5f, 0.75f, 1f })
                AddRing(verts, colors, indices, radius * fraction * 1.02f, ringColor);

            if (_laneMesh == null) _laneMesh = new Mesh { name = "GalaxyLanes" };
            _laneMesh.Clear();
            _laneMesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _laneMesh.SetVertices(verts);
            _laneMesh.SetColors(colors);
            _laneMesh.SetIndices(indices, MeshTopology.Lines, 0);
            _laneMesh.RecalculateBounds();
            if (lanesFilter != null) lanesFilter.sharedMesh = _laneMesh;
        }

        private static void AddRing(List<Vector3> verts, List<Color> colors, List<int> indices, float radius, Color color)
        {
            const int segments = 96;
            color.a = 1f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                indices.Add(verts.Count); verts.Add(new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius)); colors.Add(color);
                indices.Add(verts.Count); verts.Add(new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius)); colors.Add(color);
            }
        }

        // ------------------------------------------------------------------ helpers

        // Stable, well-separated colour per empire (golden-ratio hue steps)
        private static Color OwnerColor(int ownerId)
        {
            float hue = Mathf.Repeat(ownerId * 0.6180339887f + 0.07f, 1f);
            return Color.HSVToRGB(hue, 0.65f, 1f);
        }

        private static float Hash01(int id)
        {
            unchecked
            {
                uint h = (uint)id * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private void OnDestroy()
        {
            if (_starMesh != null) Destroy(_starMesh);
            if (_laneMesh != null) Destroy(_laneMesh);
        }
    }
}
