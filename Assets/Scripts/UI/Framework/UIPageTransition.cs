using System.Collections;
using UnityEngine;

namespace NineDays
{
    /// <summary>页面过渡动画基类；实现 Enter / Exit 两个协程即可扩展转场。</summary>
    public abstract class UIPageTransition : ScriptableObject
    {
        public abstract IEnumerator Enter(UIPage page);
        public abstract IEnumerator Exit(UIPage page);
    }
}
