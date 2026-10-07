using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using StellarCommand.UI;

namespace StellarCommand.Editor
{
    public static class CommandBridgeSetup
    {
        private const string MaterialPath = "Assets/Materials/HologramPanel.mat";
        private const string VolumeProfilePath = "Assets/Settings/HologramVolumeProfile.asset";

        private const float PanelWidth = 600f;
        private const float PanelHeight = 400f;

        private static readonly Color CyanText = new Color(0.55f, 0.95f, 1f);
        private static readonly Color DimText = new Color(0.45f, 0.7f, 0.8f);
        private static readonly Color LineColor = new Color(0.25f, 0.9f, 1f, 0.55f);

        [MenuItem("StellarCommand/Setup Holographic Panels")]
        public static void SetupHolographicPanels()
        {
            // Find or create the panels root
            var panelsRoot = GameObject.Find("Holographic Panels");
            if (panelsRoot == null)
            {
                panelsRoot = new GameObject("Holographic Panels");
                panelsRoot.transform.position = new Vector3(0, 1.5f, 2f);
            }

            var material = EnsureHologramMaterial();
            if (material == null) return;

            CreateResourcePanel(panelsRoot.transform, material);
            EnsureBloomVolume();

            // Wire up CommandBridgeManager
            var managerGO = GameObject.Find("Game Manager");
            if (managerGO != null)
            {
                var manager = managerGO.GetComponent<CommandBridgeManager>();
                if (manager != null)
                {
                    var panels = panelsRoot.GetComponentsInChildren<HolographicPanel>(true);
                    manager.panels.Clear();
                    foreach (var p in panels)
                        manager.panels.Add(p);
                    EditorUtility.SetDirty(managerGO);
                }
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[StellarCommand] Holographic panels created. Press Ctrl+S to save.");
        }

        // ------------------------------------------------------------------ assets

        private static Material EnsureHologramMaterial()
        {
            var shader = Shader.Find("StellarCommand/HologramUI");
            if (shader == null)
            {
                Debug.LogError("[StellarCommand] Shader 'StellarCommand/HologramUI' not found. " +
                               "Check Assets/Shaders/HologramUI.shader for compile errors.");
                return null;
            }

            EnsureFolder("Assets/Materials");
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

            material.SetFloat("_Aspect", PanelWidth / PanelHeight);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Creates/updates the global post-processing volume: bloom for the hologram glow and a
        /// FIXED exposure. The room and hologram shaders are self-lit, so HDRP's default automatic
        /// exposure would keep pumping the whole image brighter/darker as the head turns.
        /// </summary>
        public static void EnsureBloomVolume()
        {
            EnsureFolder("Assets/Settings");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.65f);

            // The backdrop is our own star sphere: turn off HDRP's daylight sky, clouds and fog.
            if (!profile.TryGet(out VisualEnvironment environment))
                environment = profile.Add<VisualEnvironment>(true);
            environment.skyType.Override(0);   // 0 = none
            environment.cloudType.Override(0); // 0 = none

            if (!profile.TryGet(out Fog fog))
                fog = profile.Add<Fog>(true);
            fog.enabled.Override(false);

            // EV100 = 0 gives a ~1.0 multiplier, so self-lit shader output is shown as authored.
            if (!profile.TryGet(out Exposure exposure))
                exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(0f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = GameObject.Find("Hologram Volume");
            if (go == null) go = new GameObject("Hologram Volume");
            var volume = go.GetComponent<Volume>();
            if (volume == null) volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(go);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        // ------------------------------------------------------------------ panel

        private static void CreateResourcePanel(Transform parent, Material hologramMaterial)
        {
            // Recreate from scratch so re-running the menu always produces the current look
            var existing = parent.Find("Resource Panel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var canvasGO = new GameObject("Resource Panel");
            canvasGO.layer = 0; // Default
            canvasGO.transform.SetParent(parent, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            // Adding the Canvas swaps Transform for RectTransform and resets it,
            // so size/scale/pivot must be set AFTER it exists.
            var rectTransform = canvasGO.GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one * 0.002f; // 600px -> 1.2m wide

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            var canvasGroup = canvasGO.AddComponent<CanvasGroup>();

            // Holographic body: shader draws tint, scanlines, border and corner brackets
            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            var bg = bgGO.AddComponent<RawImage>();
            bg.material = hologramMaterial;
            bg.raycastTarget = false;
            bgGO.AddComponent<HologramSurface>();

            // Header
            var title = CreateLabel(canvasGO.transform, "Title", "EMPIRE RESOURCES",
                new Vector2(0, 158), new Vector2(520, 36), 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, CyanText);
            title.characterSpacing = 6f;

            var nameLabel = CreateLabel(canvasGO.transform, "NameLabel", "EMPIRE NAME",
                new Vector2(0, 124), new Vector2(520, 26), 17, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white);
            var dateLabel = CreateLabel(canvasGO.transform, "DateLabel", "2200.01.01",
                new Vector2(0, 124), new Vector2(520, 26), 17, FontStyles.Normal, TextAlignmentOptions.MidlineRight, DimText);

            CreateLine(canvasGO.transform, "HeaderLine", 106);

            // Resource rows (rich-text tab stops are filled in by ResourcePanel.Refresh)
            const float rowStart = 78f;
            const float rowStep = 38f;
            var energyLabel   = CreateRow(canvasGO.transform, "EnergyLabel",   "ENERGY",   rowStart - rowStep * 0);
            var mineralsLabel = CreateRow(canvasGO.transform, "MineralsLabel", "MINERALS", rowStart - rowStep * 1);
            var foodLabel     = CreateRow(canvasGO.transform, "FoodLabel",     "FOOD",     rowStart - rowStep * 2);
            var alloysLabel   = CreateRow(canvasGO.transform, "AlloysLabel",   "ALLOYS",   rowStart - rowStep * 3);
            var goodsLabel    = CreateRow(canvasGO.transform, "GoodsLabel",    "GOODS",    rowStart - rowStep * 4);

            CreateLine(canvasGO.transform, "FooterLine", -112);

            var unityLabel = CreateLabel(canvasGO.transform, "UnityLabel", "UNITY",
                new Vector2(-135, -146), new Vector2(250, 34), 20, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white);
            var influenceLabel = CreateLabel(canvasGO.transform, "InfluenceLabel", "INFLUENCE",
                new Vector2(135, -146), new Vector2(250, 34), 20, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white);

            // Wire up ResourcePanel component
            var panel = canvasGO.AddComponent<ResourcePanel>();
            panel.titleLabel         = title;
            panel.canvasGroup        = canvasGroup;
            panel.energyLabel        = energyLabel;
            panel.mineralsLabel      = mineralsLabel;
            panel.foodLabel          = foodLabel;
            panel.alloysLabel        = alloysLabel;
            panel.consumerGoodsLabel = goodsLabel;
            panel.unityLabel         = unityLabel;
            panel.influenceLabel     = influenceLabel;
            panel.empireDateLabel    = dateLabel;
            panel.empireNameLabel    = nameLabel;

            EditorUtility.SetDirty(canvasGO);
            Debug.Log("[StellarCommand] Resource Panel created.");
        }

        private static TextMeshProUGUI CreateRow(Transform parent, string name, string text, float y)
        {
            return CreateLabel(parent, name, text, new Vector2(0, y), new Vector2(520, 34), 21,
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white);
        }

        private static void CreateLine(Transform parent, string name, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(520, 2);
            var img = go.AddComponent<RawImage>();
            img.color = LineColor;
            img.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles style,
            TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
