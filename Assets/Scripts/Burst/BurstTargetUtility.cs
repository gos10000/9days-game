using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>爆发效果的公共目标筛选：避免每个效果重复写阵营/发射者过滤。</summary>
    public static class BurstTargetUtility
    {
        public static void CollectCharacters(Vector2 point, float radius, LayerMask mask,
                                             bool hitAllies, bool hitOwner, in BurstContext context,
                                             List<Character> results)
        {
            results.Clear();
            if (radius <= 0f) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius, mask);
            var seen = new HashSet<int>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                Character target = hit.GetComponentInParent<Character>();
                if (target == null || target.IsDefeated) continue;
                if (!seen.Add(target.GetInstanceID())) continue;
                if (!IsAllowed(target, context, hitAllies, hitOwner)) continue;

                results.Add(target);
            }
        }

        public static bool IsAllowed(Character target, in BurstContext context, bool hitAllies, bool hitOwner)
        {
            if (target == null) return false;
            if (!hitOwner && context.Owner == target) return false;

            if (!hitAllies && context.Team != Team.None && target.Team == context.Team)
                return false;

            return true;
        }
    }
}
