using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>镜头取景数学：全景、目标框选、宽高比与场地夹取。</summary>
    public class CameraFramerTests
    {
        private static readonly Rect Bounds = new Rect(-8f, -5f, 16f, 10f);

        [Test]
        public void FrameOverview_UsesArenaAndAspect()
        {
            CameraPose pose = CameraFramer.FrameOverview(Bounds, 0.5f, 4.5f, 7f, 16f / 9f);

            Assert.AreEqual(0f, pose.Center.x, 0.001f);
            Assert.AreEqual(0f, pose.Center.y, 0.001f);
            Assert.AreEqual(5.5f, pose.OrthographicSize, 0.001f);
        }

        [Test]
        public void Frame_TwoTargets_FramesBothAndClampsToArena()
        {
            var points = new List<Vector2>
            {
                new Vector2(-5f, -3.7f),
                new Vector2(5f, -3.7f),
            };

            CameraPose pose = CameraFramer.Frame(points, 1f, 4.5f, 7f, 16f / 9f, Bounds);

            Assert.AreEqual(0f, pose.Center.x, 0.001f);
            Assert.AreEqual(-0.5f, pose.Center.y, 0.001f);
            Assert.AreEqual(4.5f, pose.OrthographicSize, 0.001f);
        }

        [Test]
        public void Frame_RespectsMaxSize()
        {
            var points = new List<Vector2>
            {
                new Vector2(-100f, -100f),
                new Vector2(100f, 100f),
            };

            CameraPose pose = CameraFramer.Frame(points, 0f, 4.5f, 7f, 1f, Bounds);

            Assert.AreEqual(7f, pose.OrthographicSize, 0.001f);
            Assert.LessOrEqual(pose.Center.x, 1f);
            Assert.GreaterOrEqual(pose.Center.x, -1f);
            Assert.LessOrEqual(pose.Center.y, 1f);
            Assert.GreaterOrEqual(pose.Center.y, -1f);
        }

        [Test]
        public void Frame_EmptyTargets_MatchesOverview()
        {
            var empty = new List<Vector2>();

            CameraPose framed = CameraFramer.Frame(empty, 1f, 4.5f, 7f, 16f / 9f, Bounds, 0.5f);
            CameraPose overview = CameraFramer.FrameOverview(Bounds, 0.5f, 4.5f, 7f, 16f / 9f);

            Assert.AreEqual(overview.Center, framed.Center);
            Assert.AreEqual(overview.OrthographicSize, framed.OrthographicSize, 0.001f);
        }

        [Test]
        public void Frame_EdgeMargin_KeepsTargetAwayFromScreenEdge()
        {
            var arena = new Rect(-12f, -5f, 24f, 10f);
            var actor = new List<Vector2> { new Vector2(11f, -3.7f) };
            const float aspect = 16f / 9f;

            CameraPose pose = CameraFramer.Frame(actor, 2.5f, 5.6f, 8.5f, aspect, arena, 0f, 3f, actor);

            float halfWidth = pose.OrthographicSize * aspect;
            Assert.GreaterOrEqual(pose.Center.x + halfWidth - 11f, 2.99f);
            Assert.GreaterOrEqual(-3.7f - (pose.Center.y - pose.OrthographicSize), 2.99f);
            Assert.AreEqual(0f, pose.Center.y, 0.001f);
        }

        [Test]
        public void Frame_EdgeMargin_OnlyUsesMarginPoints()
        {
            var arena = new Rect(-12f, -5f, 24f, 10f);
            var actor = new List<Vector2> { new Vector2(0f, -3.7f) };
            var all = new List<Vector2> { new Vector2(0f, -3.7f), new Vector2(11f, -3.7f) };
            const float aspect = 16f / 9f;

            CameraPose characterOnly = CameraFramer.Frame(all, 2.5f, 5.6f, 8.5f, aspect, arena, 0f, 3f, actor);
            CameraPose includingBullet = CameraFramer.Frame(all, 2.5f, 5.6f, 8.5f, aspect, arena, 0f, 3f, null);

            Assert.Less(characterOnly.OrthographicSize, includingBullet.OrthographicSize);
        }

        [Test]
        public void Frame_EdgeMargin_StopsAtMaxSize()
        {
            var arena = new Rect(-12f, -5f, 24f, 10f);
            var actor = new List<Vector2> { new Vector2(11f, -4f) };
            const float aspect = 16f / 9f;

            CameraPose pose = CameraFramer.Frame(actor, 2.5f, 5.6f, 8.5f, aspect, arena, 0f, 20f, actor);

            Assert.AreEqual(8.5f, pose.OrthographicSize, 0.001f);
        }

        [Test]
        public void ClampCenter_CentersWhenViewIsLargerThanArena()
        {
            Vector2 center = CameraFramer.ClampCenter(new Vector2(20f, 20f), 10f, 16f / 9f, Bounds);

            Assert.AreEqual(0f, center.x, 0.001f);
            Assert.AreEqual(0f, center.y, 0.001f);
        }

        [Test]
        public void Expand_AddsPaddingOnAllSides()
        {
            Rect expanded = CameraFramer.Expand(Bounds, 0.5f);

            Assert.AreEqual(-8.5f, expanded.xMin, 0.001f);
            Assert.AreEqual(-5.5f, expanded.yMin, 0.001f);
            Assert.AreEqual(17f, expanded.width, 0.001f);
            Assert.AreEqual(11f, expanded.height, 0.001f);
        }
    }
}
