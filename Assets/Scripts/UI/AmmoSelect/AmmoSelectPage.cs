using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>选弹页：按 AmmoSelectArgs.Options 数量动态生成选项按钮。</summary>
    public sealed class AmmoSelectPage : UIPage
    {
        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform optionRoot;
        [SerializeField] private AmmoOptionView optionTemplate;

        /// <summary>选卡页显示 / 隐藏时触发：true = 正在显示。顶部木牌据此在 up panel 与 CHOOSE 之间切换。</summary>
        public static event System.Action<bool> VisibilityChanged;

        private readonly List<AmmoOptionView> optionViews = new List<AmmoOptionView>();

        public override void OnShow(UIPageArgs args)
        {
            VisibilityChanged?.Invoke(true);

            ClearOptions();
            if (!(args is AmmoSelectArgs selectArgs)) return;

            // 标题由参数决定：选子弹 / 选道具共用一个页面
            if (titleText != null) titleText.text = selectArgs.Title;
            if (optionRoot == null || optionTemplate == null) return;

            IReadOnlyList<AmmoOption> options = selectArgs.Options;
            if (options == null) return;

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                AmmoOptionView view = Instantiate(optionTemplate, optionRoot);
                view.gameObject.SetActive(true);
                view.Bind(options[i], () => selectArgs.OnSelected?.Invoke(index));
                optionViews.Add(view);
            }
        }

        public override void OnHide()
        {
            VisibilityChanged?.Invoke(false);
            ClearOptions();
        }

        public override void OnDispose()
        {
            VisibilityChanged?.Invoke(false);
            ClearOptions();
        }

        /// <summary>清空动态按钮；optionTemplate 不放在 optionRoot 下。</summary>
        private void ClearOptions()
        {
            for (int i = 0; i < optionViews.Count; i++)
                if (optionViews[i] != null) Destroy(optionViews[i].gameObject);

            optionViews.Clear();
        }
    }
}
