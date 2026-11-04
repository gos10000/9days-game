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
            UIPageConfig config = CreatePageConfig(EditorAssets.UIPlaceholderConfigPath, placeholder);
            UIPageCatalog catalog = CreateCatalog(EditorAssets.UIPageCatalogPath, config);
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

        private static UIPageConfig CreatePageConfig(string path, GameObject pagePrefab)
        {
            UIPageConfig config = AssetDatabase.LoadAssetAtPath<UIPageConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UIPageConfig>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(config, path);
            }

            config.Configure("Placeholder", pagePrefab, UIPageLayer.Screen,
                             UIPageCachePolicy.KeepAlive, true, null, true);
            EditorUtility.SetDirty(config);
            return config;
        }

        private static UIPageCatalog CreateCatalog(string path, UIPageConfig config)
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
            if (!pages.Contains(config)) pages.Insert(0, config);
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

