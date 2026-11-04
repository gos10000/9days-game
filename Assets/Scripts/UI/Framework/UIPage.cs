using UnityEngine;

namespace NineDays
{
    /// <summary>页面基类；Router 负责实例化、缓存、入栈与返回。</summary>
    public class UIPage : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        public string PageId { get; private set; }
        public UIPageLayer Layer { get; private set; }
        public UIPageConfig Config { get; private set; }
        public UIPageRouter Router { get; private set; }
        public CanvasGroup CanvasGroup => canvasGroup;
        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>Router 实例化后绑定配置；不负责显示。</summary>
        internal void Bind(UIPageConfig config, UIPageRouter router)
        {
            Config = config;
            Router = router;
            PageId = config.PageId;
            Layer = config.Layer;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                throw new System.InvalidOperationException($"[UI] 页面 {PageId} 缺少 CanvasGroup");

            var rect = transform as RectTransform;
            if (rect == null)
                throw new System.InvalidOperationException($"[UI] 页面 {PageId} 根节点不是 RectTransform");

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        internal void Create()
        {
            OnCreate();
        }

        internal void Show(UIPageArgs args)
        {
            gameObject.SetActive(true);
            IsVisible = true;
            OnShow(args);
        }

        internal void Hide()
        {
            if (!IsVisible)
            {
                gameObject.SetActive(false);
                return;
            }

            OnHide();
            IsVisible = false;
            SetInteractable(false);
            gameObject.SetActive(false);
        }

        internal void Dispose()
        {
            IsVisible = false;
            OnDispose();
        }

        /// <summary>由过渡动画调用；0=完全透明，1=完全显示。</summary>
        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void SetInteractable(bool value)
        {
            if (canvasGroup == null) return;
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        /// <summary>页面内按钮可直接调用；关闭逻辑交给 Router。</summary>
        public void Close()
        {
            if (Router != null) Router.Close(this);
        }

        protected virtual void OnCreate()
        {
        }

        public virtual void OnShow(UIPageArgs args)
        {
        }

        public virtual void OnHide()
        {
        }

        public virtual void OnDispose()
        {
        }
    }
}
