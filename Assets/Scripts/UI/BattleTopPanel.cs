using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 顶部信息位：默认显示「回合 / 谁的回合」木牌（up panel），
    /// 选卡页打开时切成 CHOOSE 木牌。两块木牌放在同一个位置，任何时候只显示其中一块。
    ///
    /// 数据来源是 <see cref="AmmoSelectPage.VisibilityChanged"/>，不轮询、不需要在场景里找页面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleTopPanel : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("默认显示的木牌（回合 / 谁的回合）")]
        [SerializeField] private GameObject upPanel;

        [Tooltip("选卡时显示的木牌（CHOOSE）；初始应为未激活")]
        [SerializeField] private GameObject chooseHeader;

        private bool chooseVisible;

        /// <summary>当前是否处于「选卡」状态，供外部查询。</summary>
        public bool ChooseVisible => chooseVisible;

        private void Awake()
        {
            Apply(false);
        }

        private void OnEnable()
        {
            AmmoSelectPage.VisibilityChanged -= HandleChooseVisibility;
            AmmoSelectPage.VisibilityChanged += HandleChooseVisibility;
        }

        private void OnDisable()
        {
            AmmoSelectPage.VisibilityChanged -= HandleChooseVisibility;
        }

        private void HandleChooseVisibility(bool visible)
        {
            Apply(visible);
        }

        private void Apply(bool choose)
        {
            chooseVisible = choose;

            if (upPanel != null) upPanel.SetActive(!choose);
            if (chooseHeader != null) chooseHeader.SetActive(choose);
        }
    }
}
