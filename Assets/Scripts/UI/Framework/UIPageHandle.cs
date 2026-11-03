namespace NineDays
{
    /// <summary>一次页面显示的句柄；调用 Close 等价于 Router.Close(page)。</summary>
    public sealed class UIPageHandle
    {
        private readonly UIPageRouter router;

        public UIPage Page { get; }

        public bool IsOpen => Page != null && Page.IsVisible;

        internal UIPageHandle(UIPageRouter router, UIPage page)
        {
            this.router = router;
            Page = page;
        }

        public void Close()
        {
            if (router != null && Page != null) router.Close(Page);
        }
    }
}
