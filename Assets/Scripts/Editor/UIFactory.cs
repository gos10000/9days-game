using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays.EditorTools
{
    /// <summary>生成 UI 根 prefab、占位页面、页面配置与注册表。</summary>
    internal static class UIFactory
    {
        internal static void BuildAll()
        {
            EditorAssets.EnsureFolder(EditorAssets.PrefabRoot + "/UI");
            EditorAssets.EnsureFolder(EditorAssets.PrefabRoot + "/UI/Pages");
            EditorAssets.EnsureFolder(EditorAssets.DataRoot + "/UI");
            EditorAssets.EnsureFolder(EditorAssets.DataRoot + "/UI/Pages");

            GameObject placeholder = BuildPlaceholderPage(EditorAssets.UIPlaceholderPagePath);
            UIPageConfig placeholderConfig = CreatePageConfig(
                EditorAssets.UIPlaceholderConfigPath, placeholder, "Placeholder",
                UIPageLayer.Screen, true, true);

            GameObject ammoPage = BuildAmmoSelectPage(EditorAssets.AmmoSelectPagePath);
            UIPageConfig ammoConfig = CreatePageConfig(
                EditorAssets.AmmoSelectConfigPath, ammoPage, "AmmoSelect",
                UIPageLayer.Popup, false, true);

            UIPageCatalog catalog = CreateCatalog(
                EditorAssets.UIPageCatalogPath, placeholderConfig, ammoConfig);
            BuildUIRoot(EditorAssets.UIRootPrefabPath, catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UIFactory] UI 根、占位页与页面注册表生成完成");
        }

        private static GameObject BuildPlaceholderPage(string path)
        {
            var root = new GameObject("PlaceholderPage", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var group = root.AddComponent<CanvasGroup>();
            var page = root.AddComponent<PlaceholderPage>();
            EditorAssets.SetRef(page, "canvasGroup", group);

            var background = CreateImage(root.transform, "Background", new Color(0.06f, 0.08f, 0.13f, 0.94f));
            Stretch(background.rectTransform);

            Text title = CreateText(root.transform, "Title", "UI FRAMEWORK\nPlaceholder Page",
                                    42, new Vector2(900f, 180f), new Vector2(0f, 80f));
            Text hint = CreateText(root.transform, "Hint", "Click CLOSE", 20,
                                   new Vector2(900f, 50f), new Vector2(0f, -40f));
            hint.color = new Color(0.7f, 0.75f, 0.85f, 1f);

            Button close = CreateButton(root.transform, "CloseButton", "CLOSE", new Vector2(0f, -140f));
            EditorAssets.SetRef(page, "titleText", title);
            EditorAssets.SetRef(page, "closeButton", close);

            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[UIFactory] 占位页 prefab: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>生成动态选弹页：一个 inactive 模板 + Grid 容器。</summary>
        private static GameObject BuildAmmoSelectPage(string path)
        {
            var root = new GameObject("AmmoSelectPage", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var group = root.AddComponent<CanvasGroup>();
            var page = root.AddComponent<AmmoSelectPage>();
            EditorAssets.SetRef(page, "canvasGroup", group);

            var panel = CreateImage(root.transform, "Panel", new Color(0.06f, 0.08f, 0.13f, 0.96f));
            Stretch(panel.rectTransform);

            Text title = CreateText(root.transform, "Title", "选择子弹", 46,
                                    new Vector2(1000f, 100f), new Vector2(0f, 420f));
            Text hint = CreateText(root.transform, "Hint", "点击一枚子弹开始本回合", 22,
                                   new Vector2(1000f, 50f), new Vector2(0f, 350f));
            hint.color = new Color(0.7f, 0.75f, 0.85f, 1f);

            var optionRootGo = new GameObject("OptionRoot", typeof(RectTransform), typeof(GridLayoutGroup));
            optionRootGo.transform.SetParent(root.transform, false);
            optionRootGo.layer = LayerMask.NameToLayer("UI");
            var optionRoot = optionRootGo.GetComponent<RectTransform>();
            optionRoot.sizeDelta = new Vector2(1320f, 520f);
            optionRoot.anchoredPosition = new Vector2(0f, -20f);

            var grid = optionRootGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(400f, 240f);
            grid.spacing = new Vector2(30f, 30f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            GameObject template = CreateAmmoOptionTemplate(root.transform);
            template.SetActive(false);

            EditorAssets.SetRef(page, "titleText", title);
            EditorAssets.SetRef(page, "optionRoot", optionRoot);
            EditorAssets.SetRef(page, "optionTemplate", template.GetComponent<AmmoOptionView>());

            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[UIFactory] 选弹页 prefab: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>选项模板：图标 + 名称 + 介绍 + 按钮。</summary>
        private static GameObject CreateAmmoOptionTemplate(Transform parent)
        {
            var root = new GameObject("OptionTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.layer = LayerMask.NameToLayer("UI");

            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 240f);

            var image = root.GetComponent<Image>();
            image.sprite = EditorAssets.LoadSquareSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.16f, 0.25f, 0.38f, 1f);

            var button = root.GetComponent<Button>();
            button.targetGraphic = image;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            iconGo.layer = LayerMask.NameToLayer("UI");
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(96f, 96f);
            iconRect.anchoredPosition = new Vector2(-130f, 40f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = EditorAssets.LoadCircleSprite();
            icon.color = Color.white;
            icon.raycastTarget = false;

            Text nameText = CreateText(root.transform, "Name", "Bullet", 28,
                                       new Vector2(220f, 50f), new Vector2(30f, 55f));
            nameText.alignment = TextAnchor.MiddleLeft;
            Text descText = CreateText(root.transform, "Description", "Description", 18,
                                       new Vector2(250f, 90f), new Vector2(35f, -35f));
            descText.alignment = TextAnchor.UpperLeft;

            var view = root.AddComponent<AmmoOptionView>();
            EditorAssets.SetRef(view, "icon", icon);
            EditorAssets.SetRef(view, "nameText", nameText);
            EditorAssets.SetRef(view, "descriptionText", descText);
            EditorAssets.SetRef(view, "button", button);
            return root;
        }

        private static UIPageConfig CreatePageConfig(string path, GameObject pagePrefab,
                                                     string pageId, UIPageLayer layer,
                                                     bool closeOnBack, bool modal)
        {
            UIPageConfig config = AssetDatabase.LoadAssetAtPath<UIPageConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UIPageConfig>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(config, path);
            }

            config.Configure(pageId, pagePrefab, layer,
                             UIPageCachePolicy.KeepAlive, closeOnBack, null, modal);
            EditorUtility.SetDirty(config);
            return config;
        }

        private static UIPageCatalog CreateCatalog(string path, params UIPageConfig[] configs)
        {
            UIPageCatalog catalog = AssetDatabase.LoadAssetAtPath<UIPageCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UIPageCatalog>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(catalog, path);
            }

            var pages = new List<UIPageConfig>();
            if (catalog.Pages != null) pages.AddRange(catalog.Pages);
            for (int i = 0; i < configs.Length; i++)
                if (configs[i] != null && !pages.Contains(configs[i]))
                    pages.Insert(0, configs[i]);
            catalog.SetPages(pages);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void BuildUIRoot(string path, UIPageCatalog catalog)
        {
            var root = new GameObject("UIRoot", typeof(RectTransform));
            var uiRoot = root.AddComponent<UIRoot>();

            EditorAssets.SetRef(uiRoot, "catalog", catalog);
            EditorAssets.SetString(uiRoot, "startupPageId", string.Empty);
            EditorAssets.SetBool(uiRoot, "handleBackInput", true);
            EditorAssets.SetBool(uiRoot, "dontDestroyOnLoad", false);
            EditorAssets.SetVector2(uiRoot, "referenceResolution", new Vector2(1920f, 1080f));
            EditorAssets.SetInt(uiRoot, "canvasSortingOrder", 100);

            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[UIFactory] UI 根 prefab: {path}");
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var image = go.GetComponent<Image>();
            image.sprite = EditorAssets.LoadSquareSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, int size,
                                       Vector2 rectSize, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = LoadBuiltinFont();
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.sizeDelta = rectSize;
            rect.anchoredPosition = position;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var image = go.GetComponent<Image>();
            image.sprite = EditorAssets.LoadSquareSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.22f, 0.45f, 0.75f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 64f);
            rect.anchoredPosition = position;

            Text text = CreateText(go.transform, "Label", label, 22, new Vector2(220f, 64f), Vector2.zero);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return button;
        }

        private static Font LoadBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) throw new InvalidOperationException("找不到内置字体 LegacyRuntime.ttf / Arial.ttf");
            return font;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

