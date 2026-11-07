using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>UIRoot/Router 桥接：把纯选项数据变成 Popup 选弹页。</summary>
    [DisallowMultipleComponent]
    public sealed class UIAmmoSelectPresenter : MonoBehaviour, IAmmoSelectPresenter
    {
        [SerializeField] private string pageId = "AmmoSelect";

        private UIPageHandle handle;

        public bool Show(IReadOnlyList<AmmoOption> options, Action<int> onSelected)
        {
            Hide();

            UIRoot root = UIRoot.Instance;
            if (root == null || root.Router == null || root.Catalog == null) return false;
            if (!root.Catalog.TryGet(pageId, out UIPageConfig config) || config == null || config.Prefab == null)
                return false;

            handle = root.Router.PushPopup(pageId, new AmmoSelectArgs(options, onSelected));
            return handle != null;
        }

        public void Hide()
        {
            if (handle == null) return;
            handle.Close();
            handle = null;
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
