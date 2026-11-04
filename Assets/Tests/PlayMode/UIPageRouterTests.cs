using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>页面栈、缓存、弹窗、返回与 UIRoot 初始化。</summary>
    public class UIPageRouterTests
    {
        private sealed class TestPage : UIPage
        {
            public int ShowCount;
            public int HideCount;

            public override void OnShow(UIPageArgs args)
            {
                ShowCount++;
            }

            public override void OnHide()
            {
                HideCount++;
            }
        }

        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly List<GameObject> prefabs = new List<GameObject>();
        private UIRoot uiRoot;
        private UIPageCatalog catalog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            catalog = Track(ScriptableObject.CreateInstance<UIPageCatalog>());
            UIPageConfig a = CreateConfig("A", UIPageLayer.Screen);
            UIPageConfig b = CreateConfig("B", UIPageLayer.Screen);
            UIPageConfig popup = CreateConfig("Popup", UIPageLayer.Popup, modal: true);
            catalog.SetPages(new List<UIPageConfig> { a, b, popup });

            var rootGo = new GameObject("UIRoot_Test");
            uiRoot = rootGo.AddComponent<UIRoot>();
            uiRoot.Configure(catalog);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (uiRoot != null) Object.Destroy(uiRoot.gameObject);
            for (int i = 0; i < prefabs.Count; i++)
                if (prefabs[i] != null) Object.Destroy(prefabs[i]);
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null) Object.Destroy(assets[i]);
            prefabs.Clear();
            assets.Clear();
            yield return null;
        }

        private T Track<T>(T asset) where T : ScriptableObject
        {
            assets.Add(asset);
            return asset;
        }

        private UIPageConfig CreateConfig(string id, UIPageLayer layer, bool modal = false)
        {
            var prefab = new GameObject("Prefab_" + id, typeof(RectTransform));
            prefab.AddComponent<CanvasGroup>();
            prefab.AddComponent<TestPage>();
            prefab.SetActive(false);
            prefabs.Add(prefab);

            var config = Track(ScriptableObject.CreateInstance<UIPageConfig>());
            config.Configure(id, prefab, layer, UIPageCachePolicy.KeepAlive, true, null, modal);
            return config;
        }

        [UnityTest]
        public IEnumerator ShowScreen_HidesPrevious_BackRestores()
        {
            UIPageHandle first = uiRoot.Router.ShowScreen("A");
            yield return null;

            Assert.IsNotNull(first.Page);
            Assert.AreEqual("A", uiRoot.Router.CurrentScreen.PageId);
            Assert.IsTrue(first.Page.gameObject.activeSelf);

            UIPageHandle second = uiRoot.Router.ShowScreen("B");
            yield return null;

            Assert.IsFalse(first.Page.gameObject.activeSelf);
            Assert.IsTrue(second.Page.gameObject.activeSelf);
            Assert.AreEqual("B", uiRoot.Router.CurrentScreen.PageId);
            Assert.IsTrue(uiRoot.Router.CanGoBack);

            Assert.IsTrue(uiRoot.Router.Back());
            yield return null;

            Assert.IsTrue(first.Page.gameObject.activeSelf);
            Assert.AreSame(first.Page, uiRoot.Router.CurrentScreen);
            Assert.IsFalse(second.Page.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Popup_BackClosesPopupBeforeScreen()
        {
            UIPageHandle screen = uiRoot.Router.ShowScreen("A");
            yield return null;

            UIPageHandle popup = uiRoot.Router.PushPopup("Popup");
            yield return null;

            Assert.IsTrue(screen.Page.gameObject.activeSelf);
            Assert.IsTrue(popup.Page.gameObject.activeSelf);
            Assert.AreSame(popup.Page, uiRoot.Router.CurrentPopup);

            Assert.IsTrue(uiRoot.Router.Back());
            yield return null;

            Assert.IsFalse(popup.Page.gameObject.activeSelf);
            Assert.IsTrue(screen.Page.gameObject.activeSelf);
            Assert.IsNull(uiRoot.Router.CurrentPopup);
            Assert.IsFalse(uiRoot.Router.CanGoBack);
        }

        [UnityTest]
        public IEnumerator KeepAlive_ReusesPageInstance()
        {
            UIPageHandle a = uiRoot.Router.ShowScreen("A");
            yield return null;
            UIPageHandle b = uiRoot.Router.ShowScreen("B");
            yield return null;
            uiRoot.Router.Back();
            yield return null;

            UIPageHandle bAgain = uiRoot.Router.ShowScreen("B");
            yield return null;

            Assert.AreSame(b.Page, bAgain.Page);
            Assert.IsTrue(a.Page.gameObject.activeSelf == false);
        }

        [UnityTest]
        public IEnumerator UiRoot_CreatesLayersAndEventSystem()
        {
            Assert.AreEqual(5, uiRoot.LayerCount);
            foreach (UIPageLayer layer in System.Enum.GetValues(typeof(UIPageLayer)))
                Assert.IsNotNull(uiRoot.GetLayer(layer), layer.ToString());

            yield return null;
            Assert.IsNotNull(EventSystem.current);
        }

        [UnityTest]
        public IEnumerator FadeTransition_FinishesAtFullAlpha()
        {
            var fade = Track(ScriptableObject.CreateInstance<UIFadeTransition>());
            fade.Configure(0.05f);

            var prefab = new GameObject("Prefab_Fade", typeof(RectTransform));
            prefab.AddComponent<CanvasGroup>();
            prefab.AddComponent<TestPage>();
            prefab.SetActive(false);
            prefabs.Add(prefab);

            var fadeConfig = Track(ScriptableObject.CreateInstance<UIPageConfig>());
            fadeConfig.Configure("Fade", prefab, UIPageLayer.Screen, UIPageCachePolicy.KeepAlive,
                                 true, fade, false);
            catalog.SetPages(new List<UIPageConfig> { fadeConfig });

            UIPageHandle handle = uiRoot.Router.ShowScreen("Fade");
            Assert.Less(handle.Page.CanvasGroup.alpha, 1f);

            yield return new WaitForSeconds(0.15f);

            Assert.AreEqual(1f, handle.Page.CanvasGroup.alpha, 0.01f);
            Assert.IsFalse(uiRoot.Router.IsTransitioning);
        }
    }
}

