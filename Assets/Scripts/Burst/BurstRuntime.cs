using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 单发子弹的爆发运行时：计时、冷却、次数和效果执行。
    /// 每发子弹一份，绝不写回 BurstDefinition。
    /// </summary>
    public sealed class BurstRuntime
    {
        private const float MinRepeatCooldown = 0.01f;

        public BurstDefinition Definition { get; }
        public int TriggerCount { get; private set; }
        public float Elapsed { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float NextTimerTime { get; private set; }

        public BurstRuntime(BurstDefinition definition)
        {
            Definition = definition;
            ResetForLaunch();
        }

        /// <summary>新一发子弹出膛时重置全部触发状态。</summary>
        public void ResetForLaunch()
        {
            TriggerCount = 0;
            Elapsed = 0f;
            CooldownRemaining = 0f;
            NextTimerTime = Definition != null ? Mathf.Max(0f, Definition.delay) : 0f;
        }

        /// <summary>推进 OnTimer；只由 Projectile.Update 调用。</summary>
        public void Tick(float deltaTime, Projectile projectile)
        {
            if (Definition == null) return;

            Elapsed += Mathf.Max(0f, deltaTime);
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Mathf.Max(0f, deltaTime));

            if (Definition.trigger != BurstTriggerType.OnTimer) return;
            if (!CanTrigger() || Elapsed < NextTimerTime) return;

            Vector2 point = projectile != null ? (Vector2)projectile.transform.position : Vector2.zero;
            Trigger(projectile, BurstTriggerType.OnTimer, point, Vector2.zero,
                    default, false);
        }

        /// <summary>碰撞时尝试触发 OnImpact，过滤目标和速度。</summary>
        public bool TryImpact(Projectile projectile, in ProjectileHit hit, bool hitTerrain)
        {
            if (Definition == null || Definition.trigger != BurstTriggerType.OnImpact) return false;
            if (!CanTrigger() || !Definition.MatchesImpact(hit, hitTerrain)) return false;

            return Trigger(projectile, BurstTriggerType.OnImpact, hit.Point, hit.Normal, hit, hitTerrain);
        }

        /// <summary>出膛/消失/手动触发；OnImpact 请用 TryImpact。</summary>
        public bool TryTrigger(Projectile projectile, BurstTriggerType trigger, Vector2 point, Vector2 normal,
                               ProjectileHit hit, bool hitTerrain)
        {
            if (Definition == null || Definition.trigger != trigger) return false;
            if (trigger == BurstTriggerType.OnImpact) return TryImpact(projectile, hit, hitTerrain);
            if (!CanTrigger()) return false;

            return Trigger(projectile, trigger, point, normal, hit, hitTerrain);
        }

        /// <summary>手动触发当前子弹上的 Manual 爆发。</summary>
        public bool TryManual(Projectile projectile)
        {
            Vector2 point = projectile != null ? (Vector2)projectile.transform.position : Vector2.zero;
            return TryTrigger(projectile, BurstTriggerType.Manual, point, Vector2.zero, default, false);
        }

        private bool CanTrigger()
        {
            return Definition != null
                   && Definition.HasTriggersLeft(TriggerCount)
                   && CooldownRemaining <= 0f;
        }

        private bool Trigger(Projectile projectile, BurstTriggerType trigger, Vector2 point, Vector2 normal,
                             ProjectileHit hit, bool hitTerrain)
        {
            TriggerCount++;
            float cooldown = Definition.cooldown;
            if (Definition.maxTriggers == 0 && cooldown <= 0f) cooldown = MinRepeatCooldown;
            CooldownRemaining = Mathf.Max(0f, cooldown);

            if (Definition.trigger == BurstTriggerType.OnTimer)
                NextTimerTime = Elapsed + Mathf.Max(MinRepeatCooldown, cooldown);

            var context = new BurstContext(projectile, Definition, trigger, TriggerCount,
                                           point, normal, hit, hitTerrain);

            GameEvents.RaiseBurstTriggered(projectile, Definition, context);

            if (Definition.effects != null)
            {
                for (int i = 0; i < Definition.effects.Count; i++)
                {
                    BurstEffect effect = Definition.effects[i];
                    if (effect == null) continue;
                    effect.Execute(context);
                    GameEvents.RaiseBurstEffectExecuted(projectile, effect, context);
                }
            }

            if (Definition.destroyOnTrigger && projectile != null)
                projectile.Despawn(ExpireReason.Burst);

            return true;
        }
    }
}
