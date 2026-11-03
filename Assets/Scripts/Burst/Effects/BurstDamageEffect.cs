using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>范围伤害：走现有 Character.ReceiveHit / CharacterHealth / CharacterHit 链路。</summary>
    [CreateAssetMenu(menuName = "9Days/子弹/爆发效果/伤害", fileName = "BurstDamageEffect")]
    public class BurstDamageEffect : BurstEffect
    {
        [Header("范围")]
        [Min(0f)] public float radius = 1.5f;
        public LayerMask targetMask = ~0;

        [Header("伤害")]
        [Min(0f)] public float damage = 20f;

        [Tooltip("0 = 不击退；大于角色阈值会走现有 Airborne/Hit 表现")]
        [Min(0f)] public float impulse;

        [Header("衰减")]
        public bool useFalloff;
        public AnimationCurve falloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("过滤")]
        public bool hitAllies;
        public bool hitOwner;

        private readonly List<Character> targets = new List<Character>();

        public override void Execute(in BurstContext context)
        {
            BurstTargetUtility.CollectCharacters(context.Point, radius, targetMask,
                                               hitAllies, hitOwner, context, targets);

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target == null) continue;

                float distance = Vector2.Distance(context.Point, target.transform.position);
                float factor = 1f;
                if (useFalloff && falloff != null)
                {
                    float t = Mathf.Clamp01(distance / Mathf.Max(0.0001f, radius));
                    factor = falloff.Evaluate(t);
                }

                float amount = damage * factor;
                if (amount <= 0f) continue;

                Vector2 normal = (Vector2)target.transform.position - context.Point;
                if (normal.sqrMagnitude < 0.0001f) normal = context.Normal;

                var info = new HitInfo(
                    context.Source,
                    context.Owner != null ? context.Owner.gameObject : null,
                    context.Team,
                    HitKind.Burst,
                    context.Point,
                    normal,
                    impulse,
                    0f,
                    amount);

                target.ReceiveHit(info);
            }
        }

        public override void Validate()
        {
            radius = Mathf.Max(0f, radius);
            damage = Mathf.Max(0f, damage);
            impulse = Mathf.Max(0f, impulse);
        }

        private void OnValidate() => Validate();
    }
}
