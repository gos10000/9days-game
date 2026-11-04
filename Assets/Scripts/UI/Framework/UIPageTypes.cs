namespace NineDays
{
    /// <summary>UI 页面所在层；数字越大越靠上。</summary>
    public enum UIPageLayer
    {
        Background = 0,
        Screen = 1,
        Popup = 2,
        Overlay = 3,
        Toast = 4,
    }

    /// <summary>页面隐藏后的缓存策略。</summary>
    public enum UIPageCachePolicy
    {
        /// <summary>保留实例，下次打开直接复用。</summary>
        KeepAlive = 0,

        /// <summary>隐藏后销毁，适合一次性弹窗。</summary>
        DestroyOnHide = 1,
    }
}
