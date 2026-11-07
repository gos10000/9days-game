using System;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>选弹页的单个选项：图标、名称、介绍、按钮。</summary>
    public sealed class AmmoOptionView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button button;

        private Action onClick;

        private void Awake()
        {
            ResolveRefs();
        }

        /// <summary>绑定一条纯 UI 数据与点击回调。</summary>
        public void Bind(AmmoOption option, Action callback)
        {
            ResolveRefs();
            onClick = callback;

            if (icon != null)
            {
                icon.sprite = option != null ? option.Icon : null;
                icon.enabled = icon.sprite != null;
            }

            if (nameText != null) nameText.text = option != null ? option.Name : string.Empty;
            if (descriptionText != null) descriptionText.text = option != null ? option.Description : string.Empty;

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick() => onClick?.Invoke();

        /// <summary>模板可能是 inactive，不能只依赖 Awake。</summary>
        private void ResolveRefs()
        {
            if (button == null) button = GetComponent<Button>();
            if (button == null) button = GetComponentInChildren<Button>(true);
            if (icon == null) icon = GetComponentInChildren<Image>(true);
            if (nameText == null) nameText = FindText("Name");
            if (descriptionText == null) descriptionText = FindText("Description");
        }

        private Text FindText(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }
    }
}
