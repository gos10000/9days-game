using System;
using System.Collections.Generic;

namespace NineDays
{
    /// <summary>
    /// 子弹抽取纯逻辑：不依赖 Unity 对象，EditMode 可测。
    /// 正常模式随机去重；全选模式保持目录顺序。
    /// </summary>
    public static class AmmoDraw
    {
        /// <summary>从池中抽取 count 个不重复子弹；all=true 时返回全部有效项。</summary>
        public static IReadOnlyList<AmmoDefinition> Pick(
            IReadOnlyList<AmmoDefinition> pool, int count, bool all, Random rng)
        {
            if (pool == null) return Array.Empty<AmmoDefinition>();
            rng ??= new Random();

            var candidates = new List<AmmoDefinition>();
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].prefab != null)
                    candidates.Add(pool[i]);

            if (candidates.Count == 0) return Array.Empty<AmmoDefinition>();
            if (all || count >= candidates.Count) return candidates;

            count = Math.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                int j = rng.Next(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates.GetRange(0, count);
        }
    }
}
