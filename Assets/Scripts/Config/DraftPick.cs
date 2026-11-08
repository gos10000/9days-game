using System.Collections.Generic;
using Random = System.Random;

namespace NineDays
{
    /// <summary>抽取用的通用洗牌；子弹表和道具表、流程层共用。</summary>
    public static class DraftPick
    {
        /// <summary>随机取 count 个不重复项；不足 count 时返回全部（顺序已打乱）。</summary>
        public static List<T> Pick<T>(IReadOnlyList<T> source, int count, Random random)
        {
            var result = new List<T>();
            if (source == null || count <= 0) return result;

            var pool = new List<T>(source);
            random ??= new Random();

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            for (int i = 0; i < count && i < pool.Count; i++)
                result.Add(pool[i]);

            return result;
        }
    }
}
