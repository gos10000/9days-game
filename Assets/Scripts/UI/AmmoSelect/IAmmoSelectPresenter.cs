using System;
using System.Collections.Generic;

namespace NineDays
{
    /// <summary>选弹页的 UI 桥接接口；Flow 通过它显示/关闭页面。</summary>
    public interface IAmmoSelectPresenter
    {
        bool Show(IReadOnlyList<AmmoOption> options, Action<int> onSelected);
        void Hide();
    }
}
