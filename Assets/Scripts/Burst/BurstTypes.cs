using UnityEngine;

namespace NineDays
{
    /// <summary>爆发触发条件；一个定义只使用一种。</summary>
    public enum BurstTriggerType
    {
        OnLaunch = 0,
        OnTimer = 1,
        OnImpact = 2,
        OnExpire = 3,
        Manual = 4
    }

    /// <summary>OnImpact 可响应的碰撞目标。</summary>
    [System.Flags]
    public enum BurstImpactFilter
    {
        None = 0,
        Character = 1,
        Terrain = 2,
        Other = 4,
        Any = Character | Terrain | Other
    }

    /// <summary>一次爆发触发时传给效果的完整上下文；只读。</summary>
    public readonly struct BurstContext
    {
        public readonly Projectile Projectile;
        public readonly Character Owner;
        public readonly Team Team;
        public readonly BurstDefinition Definition;
        public readonly BurstTriggerType Trigger;
        public readonly int TriggerCount;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly ProjectileHit Hit;
        public readonly bool HitTerrain;

        public BurstContext(Projectile projectile, BurstDefinition definition, BurstTriggerType trigger,
                            int triggerCount, Vector2 point, Vector2 normal,
                            ProjectileHit hit, bool hitTerrain)
        {
            Projectile = projectile;
            Owner = projectile != null ? projectile.Owner : null;
            Team = projectile != null ? projectile.Team : NineDays.Team.None;
            Definition = definition;
            Trigger = trigger;
            TriggerCount = triggerCount;
            Point = point;
            Normal = normal;
            Hit = hit;
            HitTerrain = hitTerrain;
        }

        /// <summary>没有子弹来源的爆发（持续区域到期结算等）。阵营与发射者直接传入，否则友军过滤会失效。</summary>
        public BurstContext(Character owner, Team team, BurstDefinition definition, BurstTriggerType trigger,
                            int triggerCount, Vector2 point, Vector2 normal,
                            ProjectileHit hit, bool hitTerrain)
        {
            Projectile = null;
            Owner = owner;
            Team = team;
            Definition = definition;
            Trigger = trigger;
            TriggerCount = triggerCount;
            Point = point;
            Normal = normal;
            Hit = hit;
            HitTerrain = hitTerrain;
        }

        public GameObject Source => Projectile != null ? Projectile.gameObject : null;
        public Character HitCharacter => Hit.Character;
        public Collider2D HitCollider => Hit.Collider;
    }
}
