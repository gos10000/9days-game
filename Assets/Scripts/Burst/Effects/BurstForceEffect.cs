using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    public enum BurstForceMode
    {
        Push = 0,
        Pull = 1
    }

    /// <summary>范围牵引/击退：直接作用于角色刚体，不产生伤害。</summary>
    [CreateAssetMenu(menuName = "9Days/子弹/爆发效果/力场", fileName = "BurstForceEffect")]
    public class BurstForceEffect : BurstEffect
    {
        [Header("范围")]
        [Min(0f)] public float radius = 1.5f;
        public LayerMask targetMask = ~0;

        [Header("力")]
        [Min(0f)] public float force = 5f;
        public BurstForceMode mode = BurstForceMode.Push;

        [Tooltip("0 = 不锁操作")]
        [Min(0f)] public float lockControlTime;
        public bool enterAirborne;

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
                if (target == null || target.Body == null) continue;

                Vector2 direction = (Vector2)target.transform.position - context.Point;
                if (mode == BurstForceMode.Pull) direction = -direction;
                if (direction.sqrMagnitude < 0.0001f) direction = context.Normal;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;

                target.Body.AddForce(direction.normalized * force, ForceMode2D.Impulse);

                if (enterAirborne) target.EnterAirborne();
                if (lockControlTime > 0f) target.LockControl(lockControlTime);
            }
        }

        public override void Validate()
        {
            radius = Mathf.Max(0f, radius);
            force = Mathf.Max(0f, force);
            lockControlTime = Mathf.Max(0f, lockControlTime);
        }

        private void OnValidate() => Validate();
    }
}
