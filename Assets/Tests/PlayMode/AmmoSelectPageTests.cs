using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NineDays.Tests.PlayMode
{
    /// <summary>选弹页按选项数量生成按钮，并正确回调索引。</summary>
    public class AmmoSelectPageTests
    {
        private GameObject pageGo;
        private AmmoSelectPage page;
        private RectTransform optionRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            pageGo = new GameObject("AmmoSelectPage", typeof(RectTransform), typeof(CanvasGroup));
            page = pageGo.AddComponent<AmmoSelectPage>();

            var rootGo = new GameObject("OptionRoot", typeof(RectTransform));
            rootGo.transform.SetParent(pageGo.transform, false);
            optionRoot = rootGo.GetComponent<RectTransform>();

            AmmoOptionView template = CreateTemplate();
            template.gameObject.SetActive(false);

            SetField(page, "optionRoot", optionRoot);
            SetField(page, "optionTemplate", template);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pageGo != null) Object.Destroy(pageGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildsButtons_AndInvokesCallback()
        {
            int selected = -1;
            var args = new AmmoSelectArgs(new[]
            {
                new AmmoOption("A", "A desc", null),
                new AmmoOption("B", "B desc", null),
                new AmmoOption("C", "C desc", null),
            }, index => selected = index);

            page.OnShow(args);

            Assert.AreEqual(3, optionRoot.childCount);
            optionRoot.GetChild(1).GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, selected);

            page.OnHide();
            yield return null;
            Assert.AreEqual(0, optionRoot.childCount);
        }

        private AmmoOptionView CreateTemplate()
        {
            var templateGo = new GameObject("OptionTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            templateGo.transform.SetParent(pageGo.transform, false);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(templateGo.transform, false);
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(templateGo.transform, false);
            var descGo = new GameObject("Description", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(templateGo.transform, false);

            AmmoOptionView view = templateGo.AddComponent<AmmoOptionView>();
            SetField(view, "icon", iconGo.GetComponent<Image>());
            SetField(view, "nameText", nameGo.GetComponent<Text>());
            SetField(view, "descriptionText", descGo.GetComponent<Text>());
            SetField(view, "button", templateGo.GetComponent<Button>());
            return view;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }
    }
}
