using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>一次子弹接触：角色可能为空（墙 / 障碍）。</summary>
    public readonly struct ProjectileHit
    {
        public readonly Collider2D Collider;
        public readonly Character Character;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly float Impulse;
        public readonly float Speed;

        public ProjectileHit(Collider2D collider, Character character,
                             Vector2 point, Vector2 normal, float impulse, float speed)
        {
            Collider = collider;
            Character = character;
            Point = point;
            Normal = normal;
            Impulse = impulse;
            Speed = speed;
        }

        public bool HasCharacter => Character != null;
    }

    /// <summary>命中处置：Ignore = 不结算也不消耗，Pass = 结算并继续飞，Consume = 结算并消失。</summary>
    public enum HitDisposition
    {
        Ignore = 0,
        Pass = 1,
        Consume = 2
    }

    /// <summary>子弹消失原因（特效 / 音效 / 统计用）。</summary>
    public enum ExpireReason
    {
        Hit = 0,
        Lifetime = 1,
        Aborted = 2,
        Burst = 3
    }

    /// <summary>命中决策：优先问特性，没有特性投票时用配置的 destroyOnHit。</summary>
    public static class ProjectileHitResolver
    {
        public static HitDisposition Resolve(bool destroyOnHit, IReadOnlyList<ProjectileTrait> traits,
                                             Projectile projectile, in ProjectileHit hit)
        {
            bool voted = false;
            HitDisposition result = destroyOnHit ? HitDisposition.Consume : HitDisposition.Pass;

            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                {
                    ProjectileTrait trait = traits[i];
                    if (trait == null) continue;

                    HitDisposition? vote = trait.OnHit(projectile, hit);
                    if (!vote.HasValue) continue;

                    result = voted ? Combine(result, vote.Value) : vote.Value;
                    voted = true;
                }
            }

            return result;
        }

        /// <summary>多特性投票：Consume 优先于 Pass，Pass 优先于 Ignore。</summary>
        public static HitDisposition Combine(HitDisposition a, HitDisposition b)
        {
            if (a == HitDisposition.Consume || b == HitDisposition.Consume) return HitDisposition.Consume;
            if (a == HitDisposition.Pass || b == HitDisposition.Pass) return HitDisposition.Pass;
            return HitDisposition.Ignore;
        }
    }
}
