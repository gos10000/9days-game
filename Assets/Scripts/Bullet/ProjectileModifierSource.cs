using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>挂在角色上：收集装备 / 道具 / 临时 Buff 的发射修改器。</summary>
    public sealed class ProjectileModifierSource : MonoBehaviour, IProjectileModifierSource
    {
        [Header("修改器")]
        [SerializeField] private List<ProjectileModifier> modifiers = new List<ProjectileModifier>();

        public IReadOnlyList<ProjectileModifier> Modifiers => modifiers;

        public void CollectModifiers(List<ProjectileModifier> buffer)
        {
            if (buffer == null) return;

            for (int i = 0; i < modifiers.Count; i++)
                if (modifiers[i] != null) buffer.Add(modifiers[i]);
        }

        /// <summary>装备 / 道具系统在运行时挂载修改器。</summary>
        public void Add(ProjectileModifier modifier)
        {
            if (modifier == null || modifiers.Contains(modifier)) return;
            modifiers.Add(modifier);
        }

        public void Remove(ProjectileModifier modifier)
        {
            if (modifier != null) modifiers.Remove(modifier);
        }
    }
}
