using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹出膛位置解析：统一从角色身体中心发射，并避免生成点落在障碍物内部。
    /// 拖拽发射和调试面板的空格发球都走这里，保证行为一致。
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

            CharacterConfig config = shooter.Config;
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float radius = prefab.Config != null ? prefab.Config.radius : 0.25f;
            int obstacleMask = config != null ? config.obstacleMask.value : 0;
            int attempts = config != null ? config.spawnNudgeAttempts : 1;
            float step = config != null ? config.spawnNudgeStep : 0.3f;

            Vector2 origin = ResolveFreePoint(GetOrigin(shooter), dir, radius, obstacleMask, attempts, step);
            Projectile projectile = Object.Instantiate(prefab, origin, Quaternion.identity);
            projectile.Launch(dir * speed, shooter);
            return projectile;
        }
    }
}
