using UnityEngine;

namespace NineDays
{
    /// <summary>把爆发接到场景地形模块；只调用 ArenaController 的统一破坏接口。</summary>
    [CreateAssetMenu(menuName = "9Days/子弹/爆发效果/地形破坏", fileName = "BurstTerrainEffect")]
    public class BurstTerrainEffect : BurstEffect
    {
        [Header("破坏")]
        [Min(0f)] public float breakPower = 1f;
        [Min(0f)] public float radius = 1.5f;

        [Tooltip("可选；勾选后用 Profile 计算范围")]
        public bool useProfile;
        public TerrainBreakProfile profile;

        public TerrainBreakShape shape = TerrainBreakShape.Circle;
        public Vector2 size = Vector2.one;

        public override void Execute(in BurstContext context)
        {
            ArenaController arena = ArenaController.Instance;
            if (arena == null)
            {
                Debug.LogWarning("[Burst] 场景没有 ArenaController，地形破坏被跳过");
                return;
            }

            TerrainBreakRequest request;
            if (useProfile && profile != null)
            {
                request = profile.CreateRequest(breakPower, context.Point, context.Normal,
                                                context.Source, TerrainBreakKind.Burst);
            }
            else
            {
                request = new TerrainBreakRequest(
                    breakPower,
                    radius,
                    shape,
                    size,
                    context.Point,
                    context.Normal,
                    context.Source,
                    TerrainBreakKind.Burst);
            }

            arena.ApplyTerrainBreak(request);
        }

        public override void Validate()
        {
            breakPower = Mathf.Max(0f, breakPower);
            radius = Mathf.Max(0f, radius);
            if (size.x < 0f) size.x = 0f;
            if (size.y < 0f) size.y = 0f;
        }

        private void OnValidate() => Validate();
    }
}
