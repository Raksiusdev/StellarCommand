using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using uWindowCapture;
using StellarCommand.Game;
using StellarCommand.VR;

namespace StellarCommand.Editor
{
    /// <summary>
    /// Creates the main game screen: a quad showing the live Stellaris window, the pointer dot,
    /// and the component that turns the controllers into mouse and keyboard for the game.
    /// Run "Build Galaxy Map" first: it creates the controllers this connects to.
    /// </summary>
    public static class GameScreenBuilder
    {
        private const string RootName = "Game Screen";
        private const string MaterialPath = "Assets/Materials/GameScreen.mat";

        // The screen is a cylinder section around the player, on the right-hand wall (clear of the front window).
        // Angle is measured from +Z (straight ahead) towards +X (right).
        private const float CenterHeight = 1.75f;
        private const float CenterAngleDegrees = 105f;

        [MenuItem("StellarCommand/Build Game Screen")]
        public static void Build()
        {
            var shader = Shader.Find("StellarCommand/GameScreen");
            if (shader == null)
            {
                Debug.LogError("[StellarCommand] Shader 'StellarCommand/GameScreen' not found or failed to compile. " +
                               "Check Assets/Shaders/GameScreen.shader for errors in the Console.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }
            // Capture textures arrive upside down; flip them so the game reads the right way up
            material.SetFloat("_FlipY", 1f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            // Remove pointer dots left by earlier builds (they are inactive, so Find() cannot see them)
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.gameObject.name == "Game Screen Pointer")
                    Object.DestroyImmediate(t.gameObject);

            // The screen builds its own curved mesh (GameScreen.BuildMesh). The MeshCollider is what the
            // controller ray uses to read the exact texture coordinate it hits.
            var screenGO = new GameObject(RootName);
            screenGO.transform.position = new Vector3(0f, CenterHeight, 0f);
            screenGO.transform.rotation = Quaternion.Euler(0f, CenterAngleDegrees, 0f);
            screenGO.AddComponent<MeshFilter>();
            screenGO.AddComponent<MeshCollider>();
            var renderer = screenGO.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var capture = screenGO.AddComponent<UwcWindowTexture>();
            capture.partialWindowTitle = "Stellaris";
            capture.searchTiming = WindowSearchTiming.Manual;
            capture.scaleControlType = WindowTextureScaleControlType.Manual;
            capture.captureRequestTiming = WindowTextureCaptureTiming.EveryFrame;
            capture.captureFrameRate = 60;
            capture.drawCursor = true;
            capture.updateTitle = false;

            var screen = screenGO.AddComponent<GameScreen>();
            screen.windowTitle = "Stellaris";
            screen.BuildMesh(16f / 9f); // so the placement is visible in edit mode

            // Dot showing where the controller points on the screen
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Game Screen Pointer";
            dot.transform.localScale = Vector3.one * 0.04f;
            Object.DestroyImmediate(dot.GetComponent<Collider>());
            var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BridgeStrip.mat");
            if (glow != null) dot.GetComponent<MeshRenderer>().sharedMaterial = glow;
            dot.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            dot.SetActive(false);

            var input = screenGO.AddComponent<GameScreenInput>();
            input.screen = screen;
            input.pointerDot = dot.transform;
            input.rightHand = FindTracker("Right Controller");
            input.leftHand = FindTracker("Left Controller");
            if (input.rightHand == null || input.leftHand == null)
                Debug.LogWarning("[StellarCommand] Controllers not found. Run 'Build Galaxy Map' first, then 'Build Game Screen' again.");

            // Supersample the headset view for crisper small text
            if (screenGO.GetComponent<XRQuality>() == null) screenGO.AddComponent<XRQuality>();

            EditorUtility.SetDirty(screenGO);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = screenGO;
            Debug.Log("[StellarCommand] Game screen created. Start Stellaris in windowed mode, press Ctrl+S, then Play.");
        }

        private static XRControllerTracker FindTracker(string name)
        {
            foreach (var tracker in Object.FindObjectsByType<XRControllerTracker>())
                if (tracker.gameObject.name == name) return tracker;
            return null;
        }
    }
}
