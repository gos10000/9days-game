using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成战斗 UI：场景内实体 Canvas（名 UICanvas）+ 回合 HUD + 暂停面板。
    /// 与 UIRoot 的运行时 Canvas 相互独立，sortingOrder 更高。
    /// </summary>
    internal static class BattleUIBuilder
    {
        internal const string CanvasName = "UICanvas";
        internal const string FontPath = "Assets/Art/Fonts/sarasa-gothic-sc-regular.ttf";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // 面板布局
        private static readonly Vector2 PanelSize = new Vector2(580f, 420f);
        private static readonly Vector2 ButtonSize = new Vector2(460f, 72f);

        // 「谁的回合」用亮绿色强调，需与 BattleHud.actorColor 的默认值保持一致
        private static readonly Color ActorHighlight = new Color(0.42f, 1f, 0.55f);

        internal static GameObject Build(BattleController battleController = null)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
                throw new InvalidOperationException($"找不到中文字体：{FontPath}");

            EnsureEventSystem();
            RemoveExisting();
            DisableLegacyTurnBanner();

            GameObject canvasGo = CreateCanvas();
            Transform root = canvasGo.transform;

            Text roundText = CreateText(root, "RoundText", "第 1 / 9 轮", 46, font,
                                        new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(roundText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(1000f, 70f), new Vector2(0f, -28f));
            AddShadow(roundText.gameObject);

            Text actorText = CreateText(root, "ActorText", "玩家 1 的回合", 30, font,
                                        ActorHighlight, TextAnchor.MiddleCenter);
            SetRect(actorText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(1000f, 44f), new Vector2(0f, -100f));
            AddShadow(actorText.gameObject);

            GameObject pausePanel = BuildPausePanel(root, font,
                out Button resumeButton, out Button restartButton, out Button quitButton);

            var hud = canvasGo.AddComponent<BattleHud>();
            EditorAssets.SetRef(hud, "roundText", roundText);
            EditorAssets.SetRef(hud, "actorText", actorText);

            var pause = canvasGo.AddComponent<PauseMenu>();
            EditorAssets.SetRef(pause, "panelRoot", pausePanel);
            EditorAssets.SetRef(pause, "resumeButton", resumeButton);
            EditorAssets.SetRef(pause, "restartButton", restartButton);
            EditorAssets.SetRef(pause, "quitButton", quitButton);
            if (battleController != null) EditorAssets.SetRef(pause, "battleController", battleController);
            EditorAssets.SetString(pause, "startSceneName", "StartScene");
            EditorAssets.SetBool(pause, "toggleWithEscape", true);

            return canvasGo;
        }

        // ---- 构建步骤 ----

        private static GameObject CreateCanvas()
        {
            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas),
                                    typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return go;
        }

        private static GameObject BuildPausePanel(Transform parent, Font font,
                                                  out Button resume, out Button restart, out Button quit)
        {
            var panelGo = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            panelGo.layer = LayerMask.NameToLayer("UI");

            var dimmer = panelGo.GetComponent<Image>();
            dimmer.sprite = EditorAssets.LoadSquareSprite();
            dimmer.type = Image.Type.Simple;
            dimmer.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);
            dimmer.raycastTarget = true;   // 挡住面板外的点击
            Stretch(panelGo.GetComponent<RectTransform>());

            var boxGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(panelGo.transform, false);
            boxGo.layer = LayerMask.NameToLayer("UI");

            var boxImage = boxGo.GetComponent<Image>();
            boxImage.sprite = EditorAssets.LoadSquareSprite();
            boxImage.type = Image.Type.Sliced;
            boxImage.color = new Color(0.10f, 0.12f, 0.18f, 0.98f);
            boxImage.raycastTarget = true;
            SetRect(boxGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    PanelSize, Vector2.zero);

            Text title = CreateText(boxGo.transform, "Title", "暂停", 40, font,
                                    new Color(0.95f, 0.96f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(520f, 64f), new Vector2(0f, -34f));

            Color primary = new Color(0.18f, 0.26f, 0.40f, 1f);
            Color danger = new Color(0.38f, 0.16f, 0.18f, 1f);

            resume = CreateButton(boxGo.transform, "ResumeButton", "回到游戏", font, primary,
                                  new Vector2(0f, -118f));
            restart = CreateButton(boxGo.transform, "RestartButton", "重新开始", font, primary,
                                   new Vector2(0f, -206f));
            quit = CreateButton(boxGo.transform, "QuitButton", "回到开始界面", font, danger,
                                new Vector2(0f, -294f));

            panelGo.SetActive(false);
            return panelGo;
        }

        private static Button CreateButton(Transform parent, string name, string label, Font font,
                                           Color background, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var image = go.GetComponent<Image>();
            image.sprite = EditorAssets.LoadSquareSprite();
            image.type = Image.Type.Sliced;
            image.color = background;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    ButtonSize, position);

            Text text = CreateText(go.transform, "Label", label, 30, font, Color.white,
                                   TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize,
                                       Font font, Color color, TextAnchor anchor,
                                       FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        // ---- 工具 ----

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.layer = LayerMask.NameToLayer("UI");

            // 不调用的话 UI 点击/导航都不会响应
            var module = go.GetComponent<InputSystemUIInputModule>();
            if (module != null) module.AssignDefaultActions();
        }

        private static void RemoveExisting()
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == CanvasName)
                    UnityEngine.Object.DestroyImmediate(roots[i]);
        }

        /// <summary>关掉旧的 OnGUI 回合横幅，避免和 BattleHud 重复显示同一份回合信息。</summary>
        private static void DisableLegacyTurnBanner()
        {
            TurnBanner[] banners = UnityEngine.Object.FindObjectsByType<TurnBanner>(FindObjectsSortMode.None);
            for (int i = 0; i < banners.Length; i++)
            {
                if (banners[i] == null) continue;
                EditorAssets.SetBool(banners[i], "showBanner", false);
            }
        }

        private static void AddShadow(GameObject go)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot,
                                    Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>在当前打开的场景里增量生成战斗 UI，不重建整个场景。</summary>
    internal static class BattleUIMenu
    {
        [MenuItem("9Days/UI/Build Battle Canvas (当前场景)")]
        private static void BuildInCurrentScene()
        {
            if (!CheckNotPlaying()) return;

            BattleController battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
            if (battle == null && !EditorUtility.DisplayDialog(
                    "没有 BattleController",
                    "当前场景里找不到 BattleController，「重新开始」按钮会失效。\n仍然生成吗？",
                    "仍然生成", "取消"))
                return;

            BuildAndMark(battle);
        }

        [MenuItem("9Days/UI/Build Battle Canvas (TestArena)")]
        private static void BuildInTestArena()
        {
            if (!CheckNotPlaying()) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(EditorAssets.TestArenaScenePath);

            BattleController battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
            BuildAndMark(battle);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BattleUI] TestArena 已保存");
        }

        private static bool CheckNotPlaying()
        {
            if (!EditorApplication.isPlaying) return true;

            EditorUtility.DisplayDialog("正在播放", "请先退出播放模式再生成 UI。", "好");
            return false;
        }

        private static void BuildAndMark(BattleController battle)
        {
            GameObject canvas = BattleUIBuilder.Build(battle);

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Selection.activeGameObject = canvas;

            Debug.Log($"[BattleUI] 已生成 {BattleUIBuilder.CanvasName}（HUD + 暂停菜单）", canvas);
        }
    }
}
