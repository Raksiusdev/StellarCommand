using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using StellarCommand.Galaxy;
using StellarCommand.UI;
using StellarCommand.VR;

namespace StellarCommand.Editor
{
    /// <summary>
    /// Creates the galaxy hologram over the holo table, the two tracked controllers (with pointer rays),
    /// the hover label and the interaction component, and connects the map to the CommandBridgeManager.
    /// The map mesh itself is generated at runtime from the save, so it looks empty in edit mode.
    /// Run "Build Bridge Room" first (it creates the table and the materials used here).
    /// </summary>
    public static class GalaxyMapBuilder
    {
        private const string RootName = "Galaxy Map";
        private const string MaterialFolder = "Assets/Materials";

        // Hovering above the table centre; the far side is raised so the disc faces the player's eyes
        private static readonly Vector3 MapPosition = new Vector3(0f, 1.25f, 1.6f);
        private const float MapTiltDegrees = -30f;

        [MenuItem("StellarCommand/Build Galaxy Map")]
        public static void Build()
        {
            var starShader = Shader.Find("StellarCommand/MapStar");
            var laneShader = Shader.Find("StellarCommand/MapLines");
            if (starShader == null || laneShader == null)
            {
                Debug.LogError("[StellarCommand] MapStar / MapLines shader not found or failed to compile. " +
                               "Check Assets/Shaders for errors in the Console.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets", "Materials");
            var starMaterial = EnsureMaterial("GalaxyStars", starShader, 3f);
            var laneMaterial = EnsureMaterial("GalaxyLanes", laneShader, 1.6f);
            var rayMaterial = EnsureMaterial("ControllerRay", laneShader, 1.4f);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/BridgeTrim.mat");
            var glowMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/BridgeStrip.mat");

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            // ---- map hierarchy: root (position + tilt) / Rotor (turns, zooms, pans) / Stars + Lanes
            var root = new GameObject(RootName);
            root.transform.position = MapPosition;
            root.transform.rotation = Quaternion.Euler(MapTiltDegrees, 0f, 0f);

            var rotor = new GameObject("Rotor");
            rotor.transform.SetParent(root.transform, false);

            var starsFilter = CreateMeshObject("Stars", rotor.transform, starMaterial);
            var lanesFilter = CreateMeshObject("Lanes", rotor.transform, laneMaterial);

            var map = root.AddComponent<GalaxyMap>();
            map.rotor = rotor.transform;
            map.starsFilter = starsFilter;
            map.lanesFilter = lanesFilter;

            // ---- controllers
            Transform trackingSpace = FindTrackingSpace();
            var right = CreateController("Right Controller", XRNode.RightHand, trackingSpace, rayMaterial, bodyMaterial);
            var left = CreateController("Left Controller", XRNode.LeftHand, trackingSpace, rayMaterial, bodyMaterial);

            // ---- hover label and marker (world space, positioned at runtime)
            var labelGO = new GameObject("System Label");
            var label = labelGO.AddComponent<TextMeshPro>();
            label.fontSize = 2.6f; // ~2.6 cm tall lines in world units
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.richText = true;
            label.rectTransform.sizeDelta = new Vector2(0.6f, 0.12f);
            labelGO.SetActive(false);

            var markerGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerGO.name = "System Marker";
            markerGO.transform.localScale = Vector3.one * 0.03f;
            Object.DestroyImmediate(markerGO.GetComponent<Collider>());
            if (glowMaterial != null) markerGO.GetComponent<MeshRenderer>().sharedMaterial = glowMaterial;
            markerGO.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            markerGO.SetActive(false);

            // ---- interaction
            var interaction = root.AddComponent<GalaxyMapInteraction>();
            interaction.map = map;
            interaction.rightHand = right;
            interaction.leftHand = left;
            interaction.label = label;
            interaction.marker = markerGO.transform;

            // ---- connect to the manager
            var managerGO = GameObject.Find("Game Manager");
            if (managerGO != null)
            {
                var manager = managerGO.GetComponent<CommandBridgeManager>();
                if (manager != null)
                {
                    manager.galaxyMap = map;
                    EditorUtility.SetDirty(manager);
                }
            }
            else
            {
                Debug.LogWarning("[StellarCommand] 'Game Manager' not found; assign GalaxyMap to the CommandBridgeManager by hand.");
            }

            // Data panels go to the player's left so the table in front stays clear
            var panels = GameObject.Find("Holographic Panels");
            if (panels != null) panels.transform.position = new Vector3(-1.6f, 1.55f, 1.5f);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("[StellarCommand] Galaxy map and controllers created. Press Ctrl+S, then Play.");
        }

        // ------------------------------------------------------------------ controllers

        // Controllers must live in the same space as the head camera, i.e. share its parent.
        private static Transform FindTrackingSpace()
        {
            var cam = Camera.main;
            if (cam != null && cam.transform.parent != null) return cam.transform.parent;

            var rig = GameObject.Find("VR Rig");
            if (rig != null) return rig.transform;

            Debug.LogWarning("[StellarCommand] Could not find the camera offset / VR Rig; controllers created at scene root.");
            return null;
        }

        private static XRControllerTracker CreateController(string name, XRNode node, Transform parent,
            Material rayMaterial, Material bodyMaterial)
        {
            if (parent != null)
            {
                var existing = parent.Find(name);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            var tracker = go.AddComponent<XRControllerTracker>();
            tracker.node = node;

            // Simple controller stand-in so you can see where your hands are
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            body.transform.localScale = new Vector3(0.035f, 0.035f, 0.12f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            if (bodyMaterial != null) body.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Pointer pose follows the aim pose; the ray is a child of it
            var pointer = new GameObject("Pointer");
            pointer.transform.SetParent(go.transform, false);

            var line = pointer.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, new Vector3(0f, 0f, tracker.rayLength));
            line.startWidth = 0.004f;
            line.endWidth = 0.0015f;
            line.startColor = new Color(0.3f, 0.95f, 1f, 1f);
            line.endColor = new Color(0.04f, 0.2f, 0.3f, 1f);
            line.sharedMaterial = rayMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;

            tracker.pointer = pointer.transform;
            tracker.ray = line;
            return tracker;
        }

        // ------------------------------------------------------------------ helpers

        private static MeshFilter CreateMeshObject(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return filter;
        }

        private static Material EnsureMaterial(string name, Shader shader, float intensity)
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
            material.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
