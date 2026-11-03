using UnityEngine;

namespace NineDays
{
    /// <summary>抹除地形的形状。</summary>
    public enum TerrainBreakShape
    {
        Circle = 0,
        Box = 1
    }

    /// <summary>破坏来源；只做标记，场景模块不依赖任何爆发类型。</summary>
    public enum TerrainBreakKind
    {
        Impact = 0,
        Burst = 1,
        Item = 2,
        Other = 3
    }

    /// <summary>一次地形破坏请求：破坏力决定能抹除哪些层，范围决定抹多大。</summary>
    public readonly struct TerrainBreakRequest
    {
        public readonly float BreakPower;
        public readonly float Radius;
        public readonly TerrainBreakShape Shape;
        public readonly Vector2 Size;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly GameObject Source;
        public readonly TerrainBreakKind Kind;

        public TerrainBreakRequest(float breakPower, float radius, TerrainBreakShape shape,
                                   Vector2 size, Vector2 point, Vector2 normal,
                                   GameObject source, TerrainBreakKind kind)
        {
            BreakPower = breakPower;
            Radius = radius;
            Shape = shape;
            Size = size;
            Point = point;
            Normal = normal;
            Source = source;
            Kind = kind;
        }

        /// <summary>圆形请求的便捷构造。</summary>
        public static TerrainBreakRequest Circle(float breakPower, float radius, Vector2 point,
                                                 GameObject source, TerrainBreakKind kind)
        {
            return new TerrainBreakRequest(breakPower, radius, TerrainBreakShape.Circle,
                                           Vector2.one, point, Vector2.zero, source, kind);
        }

        /// <summary>矩形请求的便捷构造。</summary>
        public static TerrainBreakRequest Box(float breakPower, Vector2 size, Vector2 point,
                                              GameObject source, TerrainBreakKind kind)
        {
            return new TerrainBreakRequest(breakPower, 0f, TerrainBreakShape.Box,
                                           size, point, Vector2.zero, source, kind);
        }
    }

    /// <summary>一次地形抹除的结果。</summary>
    public readonly struct TerrainBreakResult
    {
        public readonly int AffectedLayers;
        public readonly int AffectedPixels;
        public readonly bool AnythingChanged;

        public TerrainBreakResult(int affectedLayers, int affectedPixels)
        {
            AffectedLayers = affectedLayers;
            AffectedPixels = affectedPixels;
            AnythingChanged = affectedLayers > 0 && affectedPixels > 0;
        }
    }
}
