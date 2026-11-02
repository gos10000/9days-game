using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>发射时修改运行时数值或追加特性；道具 / 装备 / Buff 共用这个扩展点。</summary>
    public abstract class ProjectileModifier : ScriptableObject
    {
        public virtual void Modify(ProjectileStats stats, ProjectileBuildContext context) { }
    }

    /// <summary>角色侧提供修改器的来源（装备、道具、临时 Buff）。</summary>
    public interface IProjectileModifierSource
    {
        void CollectModifiers(List<ProjectileModifier> buffer);
    }
}
