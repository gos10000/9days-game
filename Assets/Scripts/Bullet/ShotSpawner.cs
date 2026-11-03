using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹出膛位置解析与发射入口：拖拽发射、HUD 与特性生成的子弹都走这里。
    /// 发射时收集角色身上的 ProjectileModifier，构建本发子弹的数值与特性。
    /// </summary>
    public static class ShotSpawner
    {
        /// <summary>出膛原点 = 角色刚体中心（不是 AimPivot，也不是鼠标位置）。</summary>
        public static Vector2 GetOrigin(Character shooter)
        {
            if (shooter == null) return Vector2.zero;
            if (shooter.Body != null) return shooter.Body.position;
            return shooter.transform.position;
        }

        /// <summary>如果原点被障碍物占住，就沿发射方向（并稍微向上）找一个空位。</summary>
        public static Vector2 ResolveFreePoint(Vector2 origin, Vector2 direction, float radius,
                                               int obstacleMask, int attempts, float step)
        {
            if (obstacleMask == 0) return origin;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            if (attempts < 1) attempts = 1;
            if (step <= 0f) step = radius;

            for (int i = 0; i < attempts; i++)
            {
                Vector2 candidate = origin + dir * (step * i) + Vector2.up * (step * 0.3f * i);
                if (Physics2D.OverlapCircle(candidate, radius * 0.95f, obstacleMask) == null) return candidate;
            }

            // 全被占住也要给一个位置，避免完全发不出球
            return origin;
        }

        /// <summary>实例化并发射一颗子弹，返回生成的对象（失败返回 null）。</summary>
        public static Projectile Spawn(Projectile prefab, Character shooter, Vector2 direction, float speed)
        {
            if (prefab == null || shooter == null) return null;

            List<ProjectileModifier> modifiers = CollectModifiers(shooter);
            ProjectileLoadout loadout = ProjectileBuild.Build(prefab.Config, modifiers, shooter, speed);
            ProjectileStats stats = loadout.Stats;

            CharacterConfig config = shooter.Config;
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            int obstacleMask = config != null ? config.obstacleMask.value : 0;
            int attempts = config != null ? config.spawnNudgeAttempts : 1;
            float step = config != null ? config.spawnNudgeStep : 0.3f;

            Vector2 origin = ResolveFreePoint(GetOrigin(shooter), dir, stats.radius, obstacleMask, attempts, step);
            Projectile projectile = ProjectilePool.Instance.Rent(prefab, origin, Quaternion.identity);
            if (projectile == null) return null;

            projectile.Configure(stats, loadout.Traits, loadout.Bursts);
            projectile.Launch(dir * stats.speed, shooter);
            return projectile;
        }

        /// <summary>收集角色及其子物体上的所有修改器来源。</summary>
        private static List<ProjectileModifier> CollectModifiers(Character shooter)
        {
            var modifiers = new List<ProjectileModifier>();
            IProjectileModifierSource[] sources = shooter.GetComponentsInChildren<IProjectileModifierSource>(true);

            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].CollectModifiers(modifiers);

            return modifiers;
        }
    }
}
