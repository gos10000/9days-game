using System.Collections;
using UnityEngine;

namespace NineDays
{
    /// <summary>默认淡入淡出过渡；时长与曲线在资产 Inspector 中调整。</summary>
    [CreateAssetMenu(menuName = "9Days/UI/Transitions/Fade", fileName = "UIFadeTransition")]
    public sealed class UIFadeTransition : UIPageTransition
    {
        [SerializeField, Min(0f)] private float duration = 0.18f;

        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public override IEnumerator Enter(UIPage page)
        {
            return Fade(page, 0f, 1f);
        }

        public override IEnumerator Exit(UIPage page)
        {
            return Fade(page, 1f, 0f);
        }

        /// <summary>运行时/测试用，避免直接改序列化字段。</summary>
        public void Configure(float seconds)
        {
            duration = Mathf.Max(0f, seconds);
        }

        private IEnumerator Fade(UIPage page, float from, float to)
        {
            if (page == null) yield break;

            if (duration <= 0f)
            {
                page.SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = curve != null ? curve.Evaluate(t) : t;
                page.SetAlpha(Mathf.Lerp(from, to, eased));
                yield return null;
            }

            page.SetAlpha(to);
        }
    }
}
