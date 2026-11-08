using System;
using System.Collections.Generic;

namespace NineDays
{
    /// <summary>
    /// 通用「几选一」页面桥接：子弹选择、道具选择共用同一张页面，只是标题和数据不同。
    /// </summary>
    public interface IChoicePresenter
    {
        bool Show(string title, IReadOnlyList<AmmoOption> options, Action<int> onSelected);
        void Hide();
    }
}
