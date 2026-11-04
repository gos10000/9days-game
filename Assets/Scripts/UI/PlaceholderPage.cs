using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>框架占位页：验证 UIRoot / Router 链路，正式页面可复制此结构。</summary>
    public sealed class PlaceholderPage : UIPage
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Button closeButton;

        public override void OnShow(UIPageArgs args)
        {
            if (titleText != null) titleText.text = "UI FRAMEWORK\nPlaceholder Page";

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }
    }
}
