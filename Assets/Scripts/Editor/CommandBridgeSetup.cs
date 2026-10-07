using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using StellarCommand.UI;
using StellarCommand.SaveParser;

namespace StellarCommand.Editor
{
    public static class CommandBridgeSetup
    {
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

            // Create resource panel
            CreateResourcePanel(panelsRoot.transform);

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

        private static void CreateResourcePanel(Transform parent)
        {
            // Canvas (World Space)
            // Recreate from scratch so re-running the menu fixes a broken panel
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
            rectTransform.sizeDelta = new Vector2(600, 400);
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one * 0.002f; // 600px -> 1.2m wide

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // CanvasGroup for alpha control
            var canvasGroup = canvasGO.AddComponent<CanvasGroup>();

            // Background panel image
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.02f, 0.08f, 0.15f, 0.85f); // dark blue hologram bg

            // Title
            var titleGO = CreateTMPLabel(canvasGO.transform, "Title", "EMPIRE RESOURCES",
                new Vector2(0, 170), new Vector2(560, 40), 28, FontStyles.Bold);
            titleGO.color = new Color(0.4f, 0.9f, 1f); // cyan

            // Resource labels
            float yStart = 110f;
            float yStep = 40f;

            var energyLabel    = CreateTMPLabel(canvasGO.transform, "EnergyLabel",    "ENERGY       0", new Vector2(0, yStart - yStep * 0), new Vector2(560, 36), 18);
            var mineralsLabel  = CreateTMPLabel(canvasGO.transform, "MineralsLabel",  "MINERALS     0", new Vector2(0, yStart - yStep * 1), new Vector2(560, 36), 18);
            var foodLabel      = CreateTMPLabel(canvasGO.transform, "FoodLabel",      "FOOD         0", new Vector2(0, yStart - yStep * 2), new Vector2(560, 36), 18);
            var alloysLabel    = CreateTMPLabel(canvasGO.transform, "AlloysLabel",    "ALLOYS       0", new Vector2(0, yStart - yStep * 3), new Vector2(560, 36), 18);
            var goodsLabel     = CreateTMPLabel(canvasGO.transform, "GoodsLabel",     "GOODS        0", new Vector2(0, yStart - yStep * 4), new Vector2(560, 36), 18);
            var unityLabel     = CreateTMPLabel(canvasGO.transform, "UnityLabel",     "UNITY        0", new Vector2(-160, yStart - yStep * 5), new Vector2(240, 36), 18);
            var influenceLabel = CreateTMPLabel(canvasGO.transform, "InfluenceLabel", "INFLUENCE    0", new Vector2(160, yStart - yStep * 5), new Vector2(240, 36), 18);
            var dateLabel      = CreateTMPLabel(canvasGO.transform, "DateLabel",      "2200.01.01",     new Vector2(-160, -170), new Vector2(240, 30), 16);
            var nameLabel      = CreateTMPLabel(canvasGO.transform, "NameLabel",      "EMPIRE NAME",    new Vector2(160, -170), new Vector2(300, 30), 16);

            // Apply hologram green tint to resource labels
            var holoColor = new Color(0.3f, 1f, 0.6f);
            energyLabel.color = holoColor;
            mineralsLabel.color = holoColor;
            foodLabel.color = holoColor;
            alloysLabel.color = holoColor;
            goodsLabel.color = holoColor;
            unityLabel.color = new Color(0.8f, 0.6f, 1f);    // purple for unity
            influenceLabel.color = new Color(1f, 0.8f, 0.3f); // gold for influence
            dateLabel.color = new Color(0.6f, 0.6f, 0.6f);
            nameLabel.color = new Color(0.9f, 0.9f, 1f);

            // Wire up ResourcePanel component
            var panel = canvasGO.AddComponent<ResourcePanel>();
            panel.titleLabel      = titleGO;
            panel.canvasGroup     = canvasGroup;
            panel.energyLabel     = energyLabel;
            panel.mineralsLabel   = mineralsLabel;
            panel.foodLabel       = foodLabel;
            panel.alloysLabel     = alloysLabel;
            panel.consumerGoodsLabel = goodsLabel;
            panel.unityLabel      = unityLabel;
            panel.influenceLabel  = influenceLabel;
            panel.empireDateLabel = dateLabel;
            panel.empireNameLabel = nameLabel;

            EditorUtility.SetDirty(canvasGO);
            Debug.Log("[StellarCommand] Resource Panel created.");
        }

        private static TextMeshProUGUI CreateTMPLabel(Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
