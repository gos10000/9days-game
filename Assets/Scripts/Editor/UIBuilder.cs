using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成 UI 预制体与 UIScene（不含主菜单）。
    /// 已存在的资源跳过，避免覆盖在场景 / 预制体上的手改内容。
    /// 菜单：9Days/Build/UI
    /// </summary>
    internal static class UIBuilder
    {
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float Margin = 24f;

        private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.17f, 0.97f);
        private static readonly Color SlotColor = new Color(0.20f, 0.24f, 0.32f, 0.85f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color AccentColor = new Color(0.95f, 0.70f, 0.28f);

        private static TMP_FontAsset font;

        internal static void Build()
        {
            font = LoadFont();
            Sprite square = EditorAssets.LoadSquareSprite();
            Sprite circle = EditorAssets.LoadCircleSprite();

            BuildButton(square);
            BuildHealthBar(square);
            BuildTurnBanner(square);
            BuildAimPower(circle);
            BuildHud();
            BuildPausePanel();
            BuildItemSelectPanel(square);
            BuildPlacementPanel(square);
            BuildScreen();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UIBuilder] UI 预制体生成完成");
        }

        internal static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorAssets.UIScenePath) != null)
            {
                Debug.Log($"[UIBuilder] 已存在，跳过场景: {EditorAssets.UIScenePath}");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);

            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            var inputModule = eventGo.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            GameObject screenPrefab = Load(EditorAssets.UIScreenPath);
            GameObject screen = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab);
            screen.name = "UI_Screen";

            EditorAssets.EnsureFolder(EditorAssets.SceneRoot);
            EditorSceneManager.SaveScene(scene, EditorAssets.UIScenePath);
            EditorSceneManager.OpenScene(EditorAssets.UIScenePath);
            Debug.Log($"[UIBuilder] UI 场景: {EditorAssets.UIScenePath}");
        }

        internal static void Validate()
        {
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIButtonPath) != null, "UI_Button 缺失");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIHealthBarPath) != null, "UI_HealthBar 缺失");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIHudPath) != null, "UI_HUD 缺失");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIPausePanelPath) != null, "UI_PausePanel 缺失");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIItemSelectPanelPath) != null, "UI_ItemSelectPanel 缺失");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIPlacementPanelPath) != null, "UI_PlacementPanel 缺失");

            GameObject screen = AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIScreenPath);
            Require(screen != null, "UI_Screen 缺失");

            var canvas = screen.GetComponent<Canvas>();
            Require(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay, "UI_Screen 缺 Overlay Canvas");

            var scaler = screen.GetComponent<CanvasScaler>();
            Require(scaler != null && Mathf.Approximately(scaler.referenceResolution.x, RefWidth),
                    "UI_Screen 参考分辨率不是 1920x1080");
            Require(scaler.matchWidthOrHeight == 0.5f, "UI_Screen Match 应为 0.5");

            Require(screen.transform.Find("HUD") != null, "UI_Screen 缺 HUD");
            Require(screen.transform.Find("Panels/PausePanel") != null, "UI_Screen 缺 Panels/PausePanel");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorAssets.UIScenePath) != null, "UIScene 场景缺失");

            Debug.Log("[UIBuilder] UI 结构校验通过");
        }

        private static void BuildButton(Sprite square)
        {
            if (Exists(EditorAssets.UIButtonPath)) return;

            var root = NewUI("UI_Button", null);
            Rect(root).sizeDelta = new Vector2(240f, 72f);

            var background = AddImage(root, square, Color.white);
            background.type = Image.Type.Sliced;

            var labelGo = NewUI("Label", root.transform);
            Stretch(Rect(labelGo), 12f, 6f, 12f, 6f);
            AddText(labelGo, "按钮", 30f, Color.white, TextAlignmentOptions.Center);

            var button = root.AddComponent<Button>();
            button.targetGraphic = background;

            // 三种状态：待机 / 悬停 / 选中（颜色由 ColorBlock 决定，改预制体即可调）
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.20f, 0.28f);
            colors.highlightedColor = new Color(0.28f, 0.35f, 0.47f);
            colors.pressedColor = new Color(0.09f, 0.11f, 0.16f);
            colors.selectedColor = AccentColor;
            colors.disabledColor = new Color(0.20f, 0.20f, 0.20f, 0.45f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            Save(root, EditorAssets.UIButtonPath);
        }

        private static void BuildHealthBar(Sprite square)
        {
            if (Exists(EditorAssets.UIHealthBarPath)) return;

            var root = NewUI("UI_HealthBar", null);
            Rect(root).sizeDelta = new Vector2(480f, 48f);

            var background = AddImage(root, square, new Color(0.05f, 0.06f, 0.09f, 0.95f));
            background.type = Image.Type.Sliced;

            var fillGo = NewUI("Fill", root.transform);
            Stretch(Rect(fillGo), 4f, 4f, 4f, 4f);
            var fill = AddImage(fillGo, square, new Color(0.85f, 0.28f, 0.28f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            var labelGo = NewUI("Label", root.transform);
            Stretch(Rect(labelGo), 8f, 2f, 8f, 2f);
            AddText(labelGo, "100 / 100", 26f, Color.white, TextAlignmentOptions.Center);

            Save(root, EditorAssets.UIHealthBarPath);
        }

        private static void BuildTurnBanner(Sprite square)
        {
            if (Exists(EditorAssets.UITurnBannerPath)) return;

            var root = NewUI("UI_TurnBanner", null);
            Rect(root).sizeDelta = new Vector2(560f, 64f);

            var background = AddImage(root, square, new Color(0.12f, 0.15f, 0.22f, 0.92f));
            background.type = Image.Type.Sliced;

            var labelGo = NewUI("Label", root.transform);
            Stretch(Rect(labelGo), 16f, 4f, 16f, 4f);
            AddText(labelGo, "PLAYER 1 的回合", 28f, Color.white, TextAlignmentOptions.Center);

            Save(root, EditorAssets.UITurnBannerPath);
        }

        private static void BuildAimPower(Sprite circle)
        {
            if (Exists(EditorAssets.UIBulletTypePath)) return;

            var root = NewUI("UI_BulletType", null);
            RectTransform rootRect = Rect(root);
            rootRect.sizeDelta = new Vector2(360f, 72f);

            var background = AddImage(root, EditorAssets.LoadSquareSprite(), new Color(0.05f, 0.06f, 0.09f, 0.95f));
            background.type = Image.Type.Sliced;

            var iconGo = NewUI("BulletIcon", root.transform);
            Place(Rect(iconGo), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(56f, 56f));
            var icon = AddImage(iconGo, circle, new Color(0.95f, 0.70f, 0.28f));
            icon.raycastTarget = false;

            var labelGo = NewUI("Label", root.transform);
            Place(Rect(labelGo), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(240f, 40f));
            var label = AddText(labelGo, "普通子弹", 24f, Color.white, TextAlignmentOptions.Left);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;

            Save(root, EditorAssets.UIBulletTypePath);
        }

        private static void BuildHud()
        {
            if (Exists(EditorAssets.UIHudPath)) return;

            var root = NewUI("UI_HUD", null);
            Stretch(Rect(root), 0f, 0f, 0f, 0f);

            GameObject bar1 = Instantiate(EditorAssets.UIHealthBarPath, root.transform, "HealthBar_P1");
            Place(Rect(bar1), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(480f, 48f));

            GameObject bar2 = Instantiate(EditorAssets.UIHealthBarPath, root.transform, "HealthBar_P2");
            Place(Rect(bar2), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(480f, 48f));

            GameObject banner = Instantiate(EditorAssets.UITurnBannerPath, root.transform, "TurnBanner");
            Place(Rect(banner), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Margin), new Vector2(560f, 64f));

            GameObject aim = Instantiate(EditorAssets.UIBulletTypePath, root.transform, "BulletType");
            Place(Rect(aim), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Margin), new Vector2(360f, 72f));

            GameObject pause = Instantiate(EditorAssets.UIButtonPath, root.transform, "PauseButton");
            Place(Rect(pause), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(96f, 96f));
            SetLabel(pause, "暂停");

            Save(root, EditorAssets.UIHudPath);
        }

        private static void BuildPausePanel()
        {
            if (Exists(EditorAssets.UIPausePanelPath)) return;

            var root = NewUI("UI_PausePanel", null);
            Stretch(Rect(root), 0f, 0f, 0f, 0f);

            var dimGo = NewUI("Dim", root.transform);
            Stretch(Rect(dimGo), 0f, 0f, 0f, 0f);
            AddImage(dimGo, null, DimColor);

            var panelGo = NewUI("Panel", root.transform);
            Place(Rect(panelGo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 520f));
            var panel = AddImage(panelGo, EditorAssets.LoadSquareSprite(), PanelColor);
            panel.type = Image.Type.Sliced;

            var titleGo = NewUI("Title", panelGo.transform);
            Place(Rect(titleGo), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(480f, 72f));
            AddText(titleGo, "暂停", 44f, Color.white, TextAlignmentOptions.Center);

            CreatePanelButton(panelGo.transform, "Btn_Resume", "继续游戏", -140f);
            CreatePanelButton(panelGo.transform, "Btn_Restart", "重新开始", -232f);
            CreatePanelButton(panelGo.transform, "Btn_Settings", "设置", -324f);
            CreatePanelButton(panelGo.transform, "Btn_MainMenu", "返回主菜单", -416f);

            Save(root, EditorAssets.UIPausePanelPath);
        }

        private static void BuildItemSelectPanel(Sprite square)
        {
            if (Exists(EditorAssets.UIItemSelectPanelPath)) return;

            var root = NewUI("UI_ItemSelectPanel", null);
            Stretch(Rect(root), 0f, 0f, 0f, 0f);

            var dimGo = NewUI("Dim", root.transform);
            Stretch(Rect(dimGo), 0f, 0f, 0f, 0f);
            AddImage(dimGo, null, DimColor);

            var panelGo = NewUI("Panel", root.transform);
            Place(Rect(panelGo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 800f));
            var panel = AddImage(panelGo, square, PanelColor);
            panel.type = Image.Type.Sliced;

            var titleGo = NewUI("Title", panelGo.transform);
            Place(Rect(titleGo), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(800f, 72f));
            AddText(titleGo, "选择道具与子弹", 44f, Color.white, TextAlignmentOptions.Center);

            CreateSectionLabel(panelGo.transform, "ItemTitle", "道具", -132f);
            for (int i = 0; i < 4; i++)
            {
                CreateSlot(panelGo.transform, $"ItemSlot_{i + 1}", square, new Vector2(-525f + 350f * i, -190f), new Vector2(220f, 260f), $"道具 {i + 1}");
            }

            CreateSectionLabel(panelGo.transform, "BulletTitle", "子弹", -480f);
            for (int i = 0; i < 4; i++)
            {
                CreateSlot(panelGo.transform, $"BulletSlot_{i + 1}", square, new Vector2(-420f + 280f * i, -540f), new Vector2(160f, 140f), $"子弹 {i + 1}");
            }

            GameObject confirm = Instantiate(EditorAssets.UIButtonPath, panelGo.transform, "Btn_Confirm");
            Place(Rect(confirm), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -700f), new Vector2(240f, 72f));
            SetLabel(confirm, "确认");

            Save(root, EditorAssets.UIItemSelectPanelPath);
        }

        private static void BuildPlacementPanel(Sprite square)
        {
            if (Exists(EditorAssets.UIPlacementPanelPath)) return;

            var root = NewUI("UI_PlacementPanel", null);
            RectTransform rootRect = Rect(root);
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(0f, 200f);

            var background = AddImage(root, square, PanelColor);
            background.type = Image.Type.Sliced;

            var titleGo = NewUI("Title", root.transform);
            Place(Rect(titleGo), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -16f), new Vector2(240f, 48f));
            AddText(titleGo, "摆放阶段", 32f, Color.white, TextAlignmentOptions.Left);

            for (int i = 0; i < 4; i++)
            {
                var slotPos = new Vector2(-260f + 110f * i, -32f);
                CreateSlot(root.transform, $"ItemSlot_{i + 1}", square, slotPos, new Vector2(96f, 96f), $"{i + 1}");
            }

            GameObject cancel = Instantiate(EditorAssets.UIButtonPath, root.transform, "Btn_Cancel");
            Place(Rect(cancel), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 24f), new Vector2(200f, 64f));
            SetLabel(cancel, "取消");

            GameObject confirm = Instantiate(EditorAssets.UIButtonPath, root.transform, "Btn_Confirm");
            Place(Rect(confirm), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(270f, 24f), new Vector2(200f, 64f));
            SetLabel(confirm, "确认");

            Save(root, EditorAssets.UIPlacementPanelPath);
        }

        private static void BuildScreen()
        {
            if (Exists(EditorAssets.UIScreenPath)) return;

            var root = NewUI("UI_Screen", null);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            GameObject hud = Instantiate(EditorAssets.UIHudPath, root.transform, "HUD");
            Stretch(Rect(hud), 0f, 0f, 0f, 0f);

            var panelsGo = NewUI("Panels", root.transform);
            Stretch(Rect(panelsGo), 0f, 0f, 0f, 0f);

            CreatePanelInstance(panelsGo.transform, EditorAssets.UIPausePanelPath, "PausePanel");
            CreatePanelInstance(panelsGo.transform, EditorAssets.UIItemSelectPanelPath, "ItemSelectPanel");
            CreatePanelInstance(panelsGo.transform, EditorAssets.UIPlacementPanelPath, "PlacementPanel");

            var overlayGo = NewUI("Overlay", root.transform);
            Stretch(Rect(overlayGo), 0f, 0f, 0f, 0f);

            Save(root, EditorAssets.UIScreenPath);
        }

        private static void CreateSectionLabel(Transform parent, string name, string content, float y)
        {
            var labelGo = new GameObject(name, typeof(RectTransform));
            labelGo.transform.SetParent(parent, false);
            Place(Rect(labelGo), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(400f, 48f));
            AddText(labelGo, content, 30f, AccentColor, TextAlignmentOptions.Center);
        }

        private static GameObject CreateSlot(Transform parent, string name, Sprite square, Vector2 position, Vector2 size, string content)
        {
            var slot = new GameObject(name, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            Place(Rect(slot), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);

            var background = AddImage(slot, square, SlotColor);
            background.type = Image.Type.Sliced;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(slot.transform, false);
            Stretch(Rect(labelGo), 4f, 4f, 4f, 4f);
            AddText(labelGo, content, 22f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.Center);
            return slot;
        }

        private static void CreatePanelButton(Transform parent, string name, string label, float y)
        {
            GameObject button = Instantiate(EditorAssets.UIButtonPath, parent, name);
            Place(Rect(button), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(280f, 72f));
            SetLabel(button, label);
        }

        private static void CreatePanelInstance(Transform parent, string path, string name)
        {
            GameObject panel = Instantiate(path, parent, name);
            Stretch(Rect(panel), 0f, 0f, 0f, 0f);
            panel.SetActive(false);
            Record(panel);
        }

        private static TMP_FontAsset LoadFont()
        {
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EditorAssets.TmpFontPath);
            if (asset == null) asset = TMP_Settings.defaultFontAsset;
            if (asset == null) throw new InvalidOperationException("找不到 TMP 字体，请先导入 TextMesh Pro Essentials");
            return asset;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rect(GameObject go) => go.GetComponent<RectTransform>();

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI AddText(GameObject go, string content, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject Instantiate(string path, Transform parent, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(path));
            instance.name = name;
            if (parent != null) instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void SetLabel(GameObject instance, string label)
        {
            TextMeshProUGUI[] texts = instance.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length == 0) return;

            texts[0].text = label;
            PrefabUtility.RecordPrefabInstancePropertyModifications(texts[0]);
        }

        private static void Record(GameObject instance)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        private static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException($"缺失预制体: {path}");
            return prefab;
        }

        private static bool Exists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return false;
            Debug.Log($"[UIBuilder] 已存在，跳过: {path}");
            return true;
        }

        private static void Save(GameObject root, string path)
        {
            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[UIBuilder] 生成: {path}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new UnityException("[UIBuilder] " + message);
        }
    }
}
