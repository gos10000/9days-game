using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>选弹页的纯显示数据；UI 不接触 Projectile / AmmoDefinition。</summary>
    public sealed class AmmoOption
    {
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon { get; }

        public AmmoOption(string name, string description, Sprite icon)
        {
            Name = name;
            Description = description;
            Icon = icon;
        }
    }

    /// <summary>选弹页参数：标题、选项列表 + 选中索引回调。</summary>
    public sealed class AmmoSelectArgs : UIPageArgs
    {
        public string Title { get; }
        public IReadOnlyList<AmmoOption> Options { get; }
        public Action<int> OnSelected { get; }

        public AmmoSelectArgs(IReadOnlyList<AmmoOption> options, Action<int> onSelected)
            : this("选择子弹", options, onSelected)
        {
        }

        public AmmoSelectArgs(string title, IReadOnlyList<AmmoOption> options, Action<int> onSelected)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "选择" : title;
            Options = options;
            OnSelected = onSelected;
        }
    }
}
