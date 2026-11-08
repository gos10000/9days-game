using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 把「顶部木牌（up panel / CHOOSE）」搭到场景的 UICanvas 上，并把选卡页的卡片换成 item1 / item2。
    ///
    /// 菜单：9Days/UI/Build Choose Panel
    /// 幂等：重复执行只会更新现有节点，不会重复创建。
    /// </summary>
    internal static class ChoosePanelBuilder
    {
        private const string ChooseArtRoot = "Assets/Art/Choose";
        private const string ChoosePath = ChooseArtRoot + "/CHOOSE.png";
        private const string UpPanelPath = ChooseArtRoot + "/up panel.png";
        private const string Item1Path = ChooseArtRoot + "/item1.png";
        private const string Item2Path = ChooseArtRoot + "/item2.png";

        private const string UpPanelName = "UpPanel";
        private const string ChooseHeaderName = "ChooseHeader";

        // 两块木牌的子图都是 226×125，用同一个尺寸和位置才能完全对齐
        private static readonly Vector2 HeaderSize = new Vector2(460f, 254f);
        private static readonly Vector2 HeaderPosition = new Vector2(0f, -16f);

        // 卡片：item1 的 156×231 等比放大
        private const float CardWidth = 200f;
        private const float CardHeight = 296f;

        [MenuItem("9Days/UI/Build Choose Panel")]
        internal static void BuildMenu()
        {
            Build();
        }

        /// <summary>返回 false 表示当前场景没有 UICanvas，什么都没做。</summary>
        internal static bool Build()
        {
            Canvas canvas = FindUiCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[ChoosePanel] 当前场景里找不到名为 UICanvas 的画布（需要先打开 Test_BattleScene）");
                return false;
            }

            Sprite upSprite = LoadSprite(UpPanelPath, "up");
            Sprite chooseSprite = LoadSprite(ChoosePath, "CHOOSE_0");
            if (upSprite == null || chooseSprite == null)
            {
                Debug.LogError($"[ChoosePanel] 读不到木牌精灵，请确认 {UpPanelPath} / {ChoosePath} 已导入为 Sprite");
                return false;
            }

            GameObject upPanel = BuildHeader(canvas.transform, UpPanelName, upSprite, true);
            GameObject chooseHeader = BuildHeader(canvas.transform, ChooseHeaderName, chooseSprite, false);

            MoveHudText(canvas.transform, upPanel.transform, "RoundText", new Vector2(420f, 60f), new Vector2(0f, -28f));
            MoveHudText(canvas.transform, upPanel.transform, "ActorText", new Vector2(420f, 46f), new Vector2(0f, -82f));

            BattleTopPanel switcher = canvas.GetComponent<BattleTopPanel>();
            if (switcher == null) switcher = Undo.AddComponent<BattleTopPanel>(canvas.gameObject);

            var so = new SerializedObject(switcher);
            so.FindProperty("upPanel").objectReferenceValue = upPanel;
            so.FindProperty("chooseHeader").objectReferenceValue = chooseHeader;
            so.ApplyModifiedPropertiesWithoutUndo();

            PatchSelectPage();
            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = upPanel;
            Debug.Log("[ChoosePanel] 完成：UpPanel / ChooseHeader 已就位，选卡页卡片已换成 item1/item2。记得 Ctrl+S 保存场景。");
            return true;
        }

        // ---------------- 场景部分 ----------------

        private static GameObject BuildHeader(Transform parent, string name, Sprite sprite, bool active)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            bool created = existing == null;

            if (!created)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
                // 排在最后 → 画在最上层，不会被其它 HUD 盖住
                go.transform.SetAsLastSibling();
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            }

            // 已存在就不动位置：那是你手调过的值
            if (created)
            {
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = HeaderSize;
                rect.anchoredPosition = HeaderPosition;
            }

            var image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;

            if (go.activeSelf != active) go.SetActive(active);
            return go;
        }

        /// <summary>把 HUD 文字挂到木牌里；已经在里面的也能重复执行。</summary>
        private static void MoveHudText(Transform canvasRoot, Transform panel, string name,
                                        Vector2 size, Vector2 position)
        {
            Transform target = panel.Find(name);
            if (target == null) target = canvasRoot.Find(name);
            if (target == null)
            {
                Debug.LogWarning($"[ChoosePanel] 没找到 {name}，跳过");
                return;
            }

            bool moved = target.parent != panel;
            if (moved)
            {
                Undo.SetTransformParent(target, panel, "Move HUD text");

                // 只在第一次搬进来时排版；之后保留你手调的位置
                var rect = target as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = size;
                    rect.anchoredPosition = position;
                }
            }

            if (target.TryGetComponent(out Text text)) text.raycastTarget = false;
            if (target.TryGetComponent(out Shadow shadow)) shadow.effectDistance = new Vector2(2f, -2f);
        }

        private static Canvas FindUiCanvas()
        {
            Scene scene = SceneManager.GetActiveScene();
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.gameObject.scene != scene) continue;
                if (canvas.name == "UICanvas") return canvas;
            }

            return null;
        }

        // ---------------- 选卡页 prefab ----------------

        private static void PatchSelectPage()
        {
            Sprite item1 = LoadSprite(Item1Path, "item1_0");
            Sprite item2 = LoadSprite(Item2Path, "item2_0");
            if (item1 == null || item2 == null)
            {
                Debug.LogError("[ChoosePanel] 读不到 item1 / item2 精灵，选卡页保持原样");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(EditorAssets.AmmoSelectPagePath);
            if (contents == null)
            {
                Debug.LogError($"[ChoosePanel] 打不开 {EditorAssets.AmmoSelectPagePath}");
                return;
            }

            try
            {
                // 卡片：item1 作底，选中态换 item2
                Transform template = contents.transform.Find("OptionTemplate");
                if (template != null)
                {
                    var rect = template.GetComponent<RectTransform>();
                    if (rect != null) rect.sizeDelta = new Vector2(CardWidth, CardHeight);

                    // 根节点只当点击区：透明、不显示图
                    var image = template.GetComponent<Image>();
                    if (image != null)
                    {
                        image.sprite = null;
                        image.type = Image.Type.Simple;
                        image.color = new Color(1f, 1f, 1f, 0f);
                        image.raycastTarget = true;
                    }

                    // 真正的卡片图挂在 Art 子物体上：换图时才能「左边不动、右边延展」
                    Transform art = template.Find("Art");
                    bool freshArt = art == null;
                    if (art == null)
                    {
                        var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                        artGo.transform.SetParent(template, false);
                        artGo.layer = template.gameObject.layer;
                        artGo.transform.SetAsFirstSibling();
                        art = artGo.transform;
                    }

                    var artRect = art as RectTransform;
                    if (artRect != null && freshArt)
                    {
                        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
                        artRect.pivot = new Vector2(0.5f, 0.5f);
                        artRect.sizeDelta = new Vector2(CardWidth, CardHeight);
                        artRect.anchoredPosition = Vector2.zero;
                    }

                    var artImage = art.GetComponent<Image>();
                    if (artImage == null) artImage = art.gameObject.AddComponent<Image>();
                    artImage.sprite = item1;
                    artImage.type = Image.Type.Simple;
                    artImage.color = Color.white;
                    artImage.raycastTarget = false;

                    var button = template.GetComponent<Button>();
                    if (button != null)
                    {
                        // 换图交给 AmmoOptionView（需要同时改宽度，SpriteSwap 做不到）
                        button.transition = Selectable.Transition.None;
                        if (image != null) button.targetGraphic = image;
                    }

                    var view = template.GetComponent<AmmoOptionView>();
                    if (view != null)
                    {
                        // item2 比 item1 宽出来的比例 × 卡宽 = 悬停时要向右多出的宽度
                        float extra = item1.rect.width > 0f
                            ? CardWidth * (item2.rect.width / item1.rect.width - 1f)
                            : 22f;

                        var viewSo = new SerializedObject(view);
                        viewSo.FindProperty("artImage").objectReferenceValue = artImage;
                        viewSo.FindProperty("normalArt").objectReferenceValue = item1;
                        viewSo.FindProperty("selectedArt").objectReferenceValue = item2;
                        viewSo.FindProperty("selectedExtraWidth").floatValue = Mathf.Max(0f, extra);
                        viewSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // 只在首次建 Art 时排版；之后保留你在预制体里手调过的位置
                    if (freshArt)
                    {
                        LayoutCard(template, "Icon", new Vector2(88f, 88f), new Vector2(0f, 92f));
                        LayoutCardText(template, "Name", new Vector2(170f, 34f), new Vector2(0f, 28f), 24,
                                        TextAnchor.MiddleCenter);
                        LayoutCardText(template, "Description", new Vector2(168f, 120f), new Vector2(0f, -72f), 16,
                                        TextAnchor.UpperCenter);
                    }
                }

                // 标题被 CHOOSE 木牌取代
                Transform title = contents.transform.Find("Title");
                if (title != null && title.gameObject.activeSelf) title.gameObject.SetActive(false);

                // 提示语往下挪，避免压到 CHOOSE 木牌的底边
                Transform hint = contents.transform.Find("Hint");
                if (hint is RectTransform hintRect) hintRect.anchoredPosition = new Vector2(0f, 225f);

                // 网格：一张卡 200×296（item1 的 156×231 等比）
                Transform optionRoot = contents.transform.Find("OptionRoot");
                if (optionRoot != null)
                {
                    var grid = optionRoot.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                    {
                        grid.cellSize = new Vector2(CardWidth, CardHeight);
                        grid.spacing = new Vector2(36f, 24f);
                    }

                    var rootRect = optionRoot.GetComponent<RectTransform>();
                    if (rootRect != null)
                    {
                        rootRect.sizeDelta = new Vector2(1320f, 640f);
                        rootRect.anchoredPosition = new Vector2(0f, -60f);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, EditorAssets.AmmoSelectPagePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Debug.Log("[ChoosePanel] 选卡页已更新：卡片 = item1，悬停 = item2");
        }

        private static void LayoutCard(Transform card, string name, Vector2 size, Vector2 position)
        {
            Transform child = card.Find(name);
            if (child == null) return;

            var rect = child as RectTransform;
            if (rect == null) return;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void LayoutCardText(Transform card, string name, Vector2 size, Vector2 position,
                                           int fontSize, TextAnchor alignment)
        {
            Transform child = card.Find(name);
            if (child == null) return;

            LayoutCard(card, name, size, position);

            if (child.TryGetComponent(out Text text))
            {
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.raycastTarget = false;
            }
        }

        // ---------------- 工具 ----------------

        private static Sprite LoadSprite(string path, string spriteName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == spriteName) return sprite;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    /// <summary>
    /// 一次性自动执行：存在 Temp/choose_builder.flag 时，等编辑器空闲就自动跑一遍 Build()。
    /// 只有真的在含 UICanvas 的场景里跑成功才删标记，否则等切场景 / 重新聚焦再试。
    /// </summary>
    [InitializeOnLoad]
    internal static class ChoosePanelAutoRun
    {
        private const string FlagPath = "Temp/choose_builder.flag";
        private static bool hooked;

        static ChoosePanelAutoRun()
        {
            if (!System.IO.File.Exists(FlagPath)) return;
            Hook();
            EditorApplication.delayCall += Run;
        }

        private static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused) EditorApplication.delayCall += Run;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += Run;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (!System.IO.File.Exists(FlagPath))
            {
                Unhook();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            try
            {
                if (!ChoosePanelBuilder.Build()) return; // 场景里没有 UICanvas：等下次
                System.IO.File.Delete(FlagPath);
                Unhook();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[ChoosePanel] 自动执行失败：" + exception);
                Unhook();
            }
        }
    }
}
