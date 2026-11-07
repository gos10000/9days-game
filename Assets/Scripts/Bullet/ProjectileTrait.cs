using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹特性基类：默认值写进 stats，行为通过钩子实现。
    /// 资产必须无状态（可被所有子弹共享），每发状态放 Projectile / ProjectileStats。
    /// </summary>
    public abstract class ProjectileTrait : ScriptableObject
    {
        /// <summary>把本特性的默认参数累加进运行时数值。</summary>
        public virtual void ApplyStats(ProjectileStats stats) { }

        /// <summary>出膛时调用一次。</summary>
        public virtual void OnLaunch(Projectile projectile) { }

        /// <summary>飞行中每帧调用。</summary>
        public virtual void OnTick(Projectile projectile, float deltaTime) { }

        /// <summary>进入 Trigger 时调用（道具场、破坏区等非实体碰撞）。</summary>
        public virtual void OnTrigger(Projectile projectile, Collider2D other) { }

        /// <summary>命中时投票；返回 null 表示不参与决策。</summary>
        public virtual HitDisposition? OnHit(Projectile projectile, in ProjectileHit hit) => null;

        /// <summary>消失时调用一次（命中 / 寿命 / 中止）。</summary>
        public virtual void OnExpire(Projectile projectile, ExpireReason reason) { }
    }
}
