using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>UI 页面配置与注册表的结构校验。</summary>
    public class UIPageCatalogTests
    {
        private sealed class TestPage : UIPage
        {
        }

        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) UnityEngine.Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }

        private UIPageConfig CreateConfig(string id, bool addPage = true, bool addCanvasGroup = true)
        {
            var go = new GameObject("Page_" + id, typeof(RectTransform));
            if (addPage) go.AddComponent<TestPage>();
            if (addCanvasGroup) go.AddComponent<CanvasGroup>();
            cleanup.Add(go);

            var config = ScriptableObject.CreateInstance<UIPageConfig>();
            config.Configure(id, go, UIPageLayer.Screen);
            cleanup.Add(config);
            return config;
        }

        private UIPageCatalog CreateCatalog(params UIPageConfig[] configs)
        {
            var catalog = ScriptableObject.CreateInstance<UIPageCatalog>();
            cleanup.Add(catalog);
            catalog.SetPages(new List<UIPageConfig>(configs));
            return catalog;
        }

        [Test]
        public void Catalog_FindsConfiguredPage()
        {
            UIPageConfig config = CreateConfig("Main");
            UIPageCatalog catalog = CreateCatalog(config);

            catalog.Validate();

            Assert.AreSame(config, catalog.Find("Main"));
            Assert.AreEqual(1, catalog.Count);
        }

        [Test]
        public void Catalog_DuplicateIdThrows()
        {
            UIPageConfig first = CreateConfig("Same");
            UIPageConfig second = CreateConfig("Same");

            Assert.Throws<InvalidOperationException>(() => CreateCatalog(first, second));
        }

        [Test]
        public void Config_MissingPageComponentThrows()
        {
            UIPageConfig config = CreateConfig("NoPage", addPage: false);

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Test]
        public void Config_MissingCanvasGroupThrows()
        {
            UIPageConfig config = CreateConfig("NoGroup", addCanvasGroup: false);

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Test]
        public void Config_EmptyIdThrows()
        {
            UIPageConfig config = CreateConfig("Temporary");
            config.Configure(" ", config.Prefab, UIPageLayer.Screen);

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }
    }
}
