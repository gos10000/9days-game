using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 牵引区域的运行时对象：每帧把范围内角色吸向中心，按间隔结算伤害，
    /// 到时执行 finalBurst 的结束爆炸后自毁。参数全部来自 TractionEffect。
    /// </summary>
    public sealed class TractionZone : MonoBehaviour
    {
        private const int MaxTicksPerFrame = 8;

        private TractionEffect effect;
        private Character owner;
        private Team team;
        private BurstContext context;
        private readonly List<Character> targets = new List<Character>();

        private float elapsed;
        private float tickTimer;

        public static TractionZone Create(Vector2 point, TractionEffect effect, Character owner, Team team)
        {
            if (effect == null) return null;

            var go = new GameObject($"TractionZone({effect.name})");
            go.transform.position = point;

            var zone = go.AddComponent<TractionZone>();
            zone.effect = effect;
            zone.owner = owner;
            zone.team = team;
            zone.context = new BurstContext(owner, team, null, BurstTriggerType.OnExpire, 1,
                                           point, Vector2.zero, default, false);

            var visual = go.AddComponent<TractionRingVisual>();
            visual.Initialize(effect.radius, effect.ringColor, effect.ringCount,
                              effect.ringLifetime, effect.ringWidth);

            return zone;
        }

        private void Update()
        {
            if (effect == null)
            {
                Destroy(gameObject);
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;   // 暂停时不推进

            elapsed += deltaTime;
            tickTimer += deltaTime;

            Pull(deltaTime);

            int guard = 0;
            while (tickTimer >= effect.tickInterval && guard++ < MaxTicksPerFrame)
            {
                tickTimer -= effect.tickInterval;
                DamageTick();
            }

            if (elapsed >= effect.duration) Finish();
        }

        /// <summary>把范围内角色持续拉向中心；按质量折算成恒定加速度。</summary>
        private void Pull(float deltaTime)
        {
            if (effect.pullAcceleration <= 0f) return;

            Collect();
            Vector2 center = transform.position;

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target == null || target.Body == null) continue;

                Vector2 direction = center - (Vector2)target.transform.position;
                if (direction.sqrMagnitude < 0.0001f) continue;

                target.Body.AddForce(direction.normalized * effect.pullAcceleration * target.Body.mass,
                                     ForceMode2D.Force);
            }
        }

        /// <summary>按间隔结算一次伤害。</summary>
        private void DamageTick()
        {
            if (effect.damagePerTick <= 0f) return;

            Collect();
            Vector2 center = transform.position;

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target == null) continue;

                Vector2 direction = (Vector2)target.transform.position - center;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;

                var info = new HitInfo(
                    gameObject,
                    owner != null ? owner.gameObject : null,
                    team,
                    HitKind.Burst,
                    center,
                    direction.normalized,
                    0f,
                    0f,
                    effect.damagePerTick);

                target.ReceiveHit(info);
            }
        }

        private void Collect()
        {
            BurstTargetUtility.CollectCharacters(transform.position, effect.radius, effect.targetMask,
                                                 effect.hitAllies, effect.hitOwner, context, targets);
        }

        /// <summary>区域结束：执行收尾爆炸，然后自毁。</summary>
        private void Finish()
        {
            BurstDefinition final = effect != null ? effect.finalBurst : null;

            if (final != null && final.effects != null)
            {
                var finalContext = new BurstContext(owner, team, final, BurstTriggerType.OnExpire, 1,
                                                    transform.position, Vector2.zero, default, false);

                // 广播一次，让爆炸特效 / 镜头震动等表现层能接上收尾爆炸
                GameEvents.RaiseBurstTriggered(null, final, finalContext);

                for (int i = 0; i < final.effects.Count; i++)
                {
                    BurstEffect burstEffect = final.effects[i];
                    if (burstEffect == null) continue;

                    burstEffect.Execute(finalContext);
                    GameEvents.RaiseBurstEffectExecuted(null, burstEffect, finalContext);
                }
            }

            Destroy(gameObject);
        }
    }
}
