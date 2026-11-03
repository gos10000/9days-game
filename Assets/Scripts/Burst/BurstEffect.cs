using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 爆发效果基类：只做逻辑，不碰 Animator / 粒子 / 音效。
    /// 表现层通过 BurstTriggered / BurstEffectExecuted 事件接入。
    /// </summary>
    public abstract class BurstEffect : ScriptableObject
    {
        /// <summary>执行一次效果；实现必须无状态，可被多个子弹共享。</summary>
        public abstract void Execute(in BurstContext context);

        /// <summary>Inspector 参数校验；默认不处理。</summary>
        public virtual void Validate() { }
    }
}
