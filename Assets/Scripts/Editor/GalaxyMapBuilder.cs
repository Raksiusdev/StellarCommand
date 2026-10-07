using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using StellarCommand.Galaxy;
using StellarCommand.UI;

namespace StellarCommand.Editor
{
    /// <summary>
    /// Creates the "Galaxy Map" hologram in the scene (hovering over the console, in front of the player),
    /// its two materials, and connects it to the CommandBridgeManager so it fills when a save loads.
    /// The mesh itself is generated at runtime from the save, so the object looks empty in edit mode.
    /// </summary>
    public static class GalaxyMapBuilder
    {
        private const string RootName = "Galaxy Map";
        private const string MaterialFolder = "Assets/Materials";

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

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            // Hovering above the console's inner edge, tilted so the far side rises towards the viewer's eyes
            var root = new GameObject(RootName);
            root.transform.position = new Vector3(0f, 1.0f, 0.95f);
            root.transform.rotation = Quaternion.Euler(-18f, 0f, 0f);

            var rotor = new GameObject("Rotor");
            rotor.transform.SetParent(root.transform, false);

            var starsFilter = CreateMeshObject("Stars", rotor.transform, starMaterial);
            var lanesFilter = CreateMeshObject("Lanes", rotor.transform, laneMaterial);

            var map = root.AddComponent<GalaxyMap>();
            map.rotor = rotor.transform;
            map.starsFilter = starsFilter;
            map.lanesFilter = lanesFilter;

            // Connect to the manager
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

            // Lift the resource panel so the map does not hide its lower edge
            var panels = GameObject.Find("Holographic Panels");
            if (panels != null) panels.transform.position = new Vector3(0f, 1.65f, 2f);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("[StellarCommand] Galaxy map created. Press Ctrl+S, then Play (the map fills once the save loads).");
        }

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
