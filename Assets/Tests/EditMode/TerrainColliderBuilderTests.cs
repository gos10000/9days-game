using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>地形遮罩 → 轮廓的纯算法测试。</summary>
    public class TerrainColliderBuilderTests
    {
        private static bool[] SolidRect(int width, int height)
        {
            var solid = new bool[width * height];
            for (int i = 0; i < solid.Length; i++) solid[i] = true;
            return solid;
        }

        [Test]
        public void BuildContours_SolidRect_ReturnsOneClosedLoop()
        {
            bool[] solid = SolidRect(8, 8);

            List<Vector2[]> contours = TerrainColliderBuilder.BuildContours(
                solid, 8, 8, Vector2.zero, 1f, 0f);

            Assert.AreEqual(1, contours.Count);
            Assert.GreaterOrEqual(contours[0].Length, 4);
            Assert.AreEqual(contours[0][0], contours[0][contours[0].Length - 1]);
        }

        [Test]
        public void BuildContours_Hole_ReturnsOuterAndInnerLoops()
        {
            int width = 8;
            int height = 8;
            bool[] solid = SolidRect(width, height);
            for (int y = 3; y <= 4; y++)
                for (int x = 3; x <= 4; x++)
                    solid[x + y * width] = false;

            List<Vector2[]> contours = TerrainColliderBuilder.BuildContours(
                solid, width, height, Vector2.zero, 1f, 0f);

            Assert.AreEqual(2, contours.Count, "实心矩形挖洞后应得到外轮廓 + 内轮廓");
        }

        [Test]
        public void BuildContours_Empty_ReturnsNoContours()
        {
            List<Vector2[]> contours = TerrainColliderBuilder.BuildContours(
                new bool[16], 4, 4, Vector2.zero, 1f, 0f);

            Assert.AreEqual(0, contours.Count);
        }

        [Test]
        public void Simplify_RemovesCollinearPoints()
        {
            var points = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0),
                new Vector2(2, 1), new Vector2(2, 2),
                new Vector2(1, 2), new Vector2(0, 2),
                new Vector2(0, 1), new Vector2(0, 0)
            };

            Vector2[] simplified = TerrainColliderBuilder.Simplify(points.ToArray(), 0.001f);

            Assert.Less(simplified.Length, points.Count);
            Assert.GreaterOrEqual(simplified.Length, 4);
            Assert.AreEqual(simplified[0], simplified[simplified.Length - 1]);
        }
    }
}
