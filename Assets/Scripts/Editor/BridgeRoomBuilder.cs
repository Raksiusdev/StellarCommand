using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace StellarCommand.Editor
{
    /// <summary>
    /// Builds the command bridge room out of primitives and the procedural shaders in Assets/Shaders.
    /// Layout (player at the origin looking down +Z):
    ///   rear half  - solid wall with light strips
    ///   front half - open viewport: low sill, pillars and a lintel, looking out into space
    ///   centre     - curved console in front of the player, holographic panels float above it
    /// Re-running the menu rebuilds everything from scratch.
    /// </summary>
    public static class BridgeRoomBuilder
    {
        private const string RootName = "Bridge Room";
        private const string MaterialFolder = "Assets/Materials";

        private const float Radius = 7f;
        private const float Height = 4.5f;
        private const int Slots = 24;
        private const float SlotAngle = 360f / Slots;

        private static readonly Color HullColor = new Color(0.045f, 0.065f, 0.095f);
        private static readonly Color TrimColor = new Color(0.08f, 0.11f, 0.15f);
        private static readonly Color Accent = new Color(0.20f, 0.85f, 1.00f);

        private static Material _hull, _trim, _strip, _sky, _planet, _moon;
        private static Transform _root;

        [MenuItem("StellarCommand/Build Bridge Room")]
        public static void Build()
        {
            if (!LoadMaterials()) return;

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            _root = new GameObject(RootName).transform;
            _root.position = Vector3.zero;

            BuildShell();
            BuildWalls();
            BuildConsole();
            BuildSpaceScenery();

            // Fixed exposure + bloom; without it HDRP auto-exposure pumps the image as the head turns
            CommandBridgeSetup.EnsureBloomVolume();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[StellarCommand] Bridge room built. Press Ctrl+S to save.");
        }

        // ------------------------------------------------------------------ materials

        private static bool LoadMaterials()
        {
            var surface = Shader.Find("StellarCommand/BridgeSurface");
            var skybox = Shader.Find("StellarCommand/SpaceSkybox");
            var planet = Shader.Find("StellarCommand/PlanetProcedural");
            if (surface == null || skybox == null || planet == null)
            {
                Debug.LogError("[StellarCommand] A bridge shader was not found or failed to compile. " +
                               "Check Assets/Shaders (BridgeSurface, SpaceSkybox, PlanetProcedural) for errors in the Console.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets", "Materials");

            _hull = EnsureMaterial("BridgeHull", surface, m =>
            {
                m.SetColor("_BaseColor", HullColor);
                m.SetColor("_AccentColor", Accent);
                m.SetFloat("_Emission", 0f);
            });
            _trim = EnsureMaterial("BridgeTrim", surface, m =>
            {
                m.SetColor("_BaseColor", TrimColor);
                m.SetColor("_AccentColor", Accent);
                m.SetFloat("_Emission", 0f);
                m.SetFloat("_SeamScale", 0.6f);
                m.SetFloat("_SeamGlow", 0.8f);
            });
            _strip = EnsureMaterial("BridgeStrip", surface, m =>
            {
                m.SetColor("_AccentColor", Accent);
                m.SetFloat("_Emission", 1f);
                m.SetFloat("_EmissionIntensity", 3f);
            });
            _sky = EnsureMaterial("SpaceSkybox", skybox, m =>
            {
                // Set explicitly: shader default changes do not reach an already-saved material asset.
                m.SetColor("_NebulaColorA", new Color(0.12f, 0.30f, 0.90f));
                m.SetColor("_NebulaColorB", new Color(0.75f, 0.15f, 0.55f));
                m.SetFloat("_NebulaIntensity", 1.6f);
                m.SetFloat("_StarIntensity", 4f);
                m.SetFloat("_StarDensity", 0.25f);
            });
            _planet = EnsureMaterial("PlanetGasGiant", planet, m => { });
            _moon = EnsureMaterial("PlanetMoon", planet, m =>
            {
                m.SetColor("_ColorA", new Color(0.62f, 0.60f, 0.58f));
                m.SetColor("_ColorB", new Color(0.30f, 0.29f, 0.30f));
                m.SetFloat("_BandFreq", 3f);
                m.SetFloat("_Turbulence", 3.2f);
                m.SetFloat("_Seed", 7.1f);
                m.SetFloat("_AtmoIntensity", 0.15f);
            });

            AssetDatabase.SaveAssets();
            return true;
        }

        private static Material EnsureMaterial(string name, Shader shader, System.Action<Material> configure)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ geometry

        private static void BuildShell()
        {
            float diameter = (Radius + 0.4f) * 2f;
            Make(PrimitiveType.Cylinder, "Floor", new Vector3(0, -0.05f, 0), Quaternion.identity,
                new Vector3(diameter, 0.05f, diameter), _hull);
            Make(PrimitiveType.Cylinder, "Ceiling", new Vector3(0, Height + 0.05f, 0), Quaternion.identity,
                new Vector3(diameter, 0.05f, diameter), _hull);

            float slotWidth = SlotWidth(Radius);

            // Light rings in the floor and ceiling
            for (int i = 0; i < Slots; i++)
            {
                float a = i * SlotAngle;
                Make(PrimitiveType.Cube, "FloorRing", RadialPoint(Radius - 0.7f, 0.006f, a), Tangent(a),
                    new Vector3(slotWidth * 0.8f, 0.012f, 0.06f), _strip);
                Make(PrimitiveType.Cube, "CeilingRing", RadialPoint(3.2f, Height - 0.01f, a), Tangent(a),
                    new Vector3(SlotWidth(3.2f) * 0.85f, 0.04f, 0.14f), _strip);
                Make(PrimitiveType.Cube, "CeilingRingOuter", RadialPoint(Radius - 0.6f, Height - 0.01f, a), Tangent(a),
                    new Vector3(slotWidth * 0.85f, 0.04f, 0.08f), _strip);
            }
        }

        private static void BuildWalls()
        {
            float slotWidth = SlotWidth(Radius);

            for (int i = 0; i < Slots; i++)
            {
                float a = i * SlotAngle;
                bool front = IsFront(a);

                if (!front)
                {
                    // Solid wall, full height, with two light strips
                    Make(PrimitiveType.Cube, "Wall", RadialPoint(Radius + 0.2f, Height * 0.5f, a), Tangent(a),
                        new Vector3(slotWidth, Height, 0.4f), _hull);
                    Make(PrimitiveType.Cube, "WallStripLow", RadialPoint(Radius - 0.005f, 1.1f, a), Tangent(a),
                        new Vector3(slotWidth * 0.96f, 0.06f, 0.04f), _strip);
                    Make(PrimitiveType.Cube, "WallStripHigh", RadialPoint(Radius - 0.005f, 3.6f, a), Tangent(a),
                        new Vector3(slotWidth * 0.96f, 0.06f, 0.04f), _strip);
                }
                else
                {
                    // Viewport: low sill and lintel with light strips along their inner edges
                    Make(PrimitiveType.Cube, "Sill", RadialPoint(Radius + 0.2f, 0.45f, a), Tangent(a),
                        new Vector3(slotWidth, 0.9f, 0.4f), _trim);
                    Make(PrimitiveType.Cube, "SillStrip", RadialPoint(Radius + 0.05f, 0.91f, a), Tangent(a),
                        new Vector3(slotWidth * 0.96f, 0.04f, 0.2f), _strip);
                    Make(PrimitiveType.Cube, "Lintel", RadialPoint(Radius + 0.2f, 4.25f, a), Tangent(a),
                        new Vector3(slotWidth, 0.5f, 0.4f), _trim);
                    Make(PrimitiveType.Cube, "LintelStrip", RadialPoint(Radius + 0.05f, 3.98f, a), Tangent(a),
                        new Vector3(slotWidth * 0.96f, 0.04f, 0.2f), _strip);
                }
            }

            // Pillars between window panes (none dead ahead, so the view straight out stays clear)
            foreach (float a in new[] { -82.5f, -55f, -27.5f, 27.5f, 55f, 82.5f })
            {
                Make(PrimitiveType.Cube, "Pillar", RadialPoint(Radius + 0.05f, Height * 0.5f, a), Tangent(a),
                    new Vector3(0.3f, Height, 0.45f), _trim);
                Make(PrimitiveType.Cube, "PillarStrip", RadialPoint(Radius - 0.18f, Height * 0.5f, a), Tangent(a),
                    new Vector3(0.04f, Height - 1.1f, 0.03f), _strip);
            }
        }

        private static void BuildConsole()
        {
            const float consoleRadius = 1.5f;
            float width = SlotWidthDegrees(consoleRadius, SlotAngle) * 1.03f;

            for (float a = -60f; a <= 60.01f; a += SlotAngle)
            {
                Make(PrimitiveType.Cube, "ConsoleBody", RadialPoint(consoleRadius + 0.25f, 0.45f, a), Tangent(a),
                    new Vector3(width, 0.9f, 0.5f), _hull);
                Make(PrimitiveType.Cube, "ConsoleTopStrip", RadialPoint(consoleRadius + 0.01f, 0.91f, a), Tangent(a),
                    new Vector3(width * 0.96f, 0.03f, 0.04f), _strip);
                Make(PrimitiveType.Cube, "ConsoleBaseStrip", RadialPoint(consoleRadius - 0.005f, 0.05f, a), Tangent(a),
                    new Vector3(width * 0.96f, 0.04f, 0.03f), _strip);
            }
        }

        private static void BuildSpaceScenery()
        {
            // Star sphere around everything, seen from the inside
            var sky = Make(PrimitiveType.Sphere, "Space", Vector3.zero, Quaternion.identity,
                Vector3.one * 900f, _sky);
            sky.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Gas giant off to the left, small moon to the right
            Make(PrimitiveType.Sphere, "Planet", new Vector3(-75f, 22f, 190f), Quaternion.identity,
                Vector3.one * 80f, _planet);
            Make(PrimitiveType.Sphere, "Moon", new Vector3(70f, 38f, 150f), Quaternion.identity,
                Vector3.one * 9f, _moon);
        }

        // ------------------------------------------------------------------ helpers

        // A slot is "front" (open viewport) when it faces +Z within +-82.5 degrees.
        private static bool IsFront(float angleDeg)
        {
            float a = Mathf.Repeat(angleDeg + 180f, 360f) - 180f; // -180..180
            return Mathf.Abs(a) < 89f;
        }

        // Angle is measured from +Z towards +X.
        private static Vector3 RadialPoint(float radius, float y, float angleDeg)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r) * radius, y, Mathf.Cos(r) * radius);
        }

        // Rotation whose local +Z points outward along the radial direction (local X is the tangent).
        private static Quaternion Tangent(float angleDeg) => Quaternion.Euler(0f, angleDeg, 0f);

        private static float SlotWidth(float radius) => SlotWidthDegrees(radius, SlotAngle) * 1.02f;

        private static float SlotWidthDegrees(float radius, float degrees) =>
            2f * radius * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad);

        private static GameObject Make(PrimitiveType type, string name, Vector3 position, Quaternion rotation,
            Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;

            // Pure scenery: no physics
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
