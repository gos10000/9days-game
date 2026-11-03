using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>破坏力 → 范围的可调曲线测试。</summary>
    public class TerrainBreakProfileTests
    {
        private TerrainBreakProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<TerrainBreakProfile>();
            profile.baseRadius = 0.5f;
            profile.radiusPerPower = 0.25f;
            profile.minRadius = 0.25f;
            profile.maxRadius = 3f;
            profile.useCurve = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void EvaluateRadius_Linear()
        {
            Assert.AreEqual(0.5f, profile.EvaluateRadius(0f), 0.001f);
            Assert.AreEqual(1.5f, profile.EvaluateRadius(4f), 0.001f);
        }

        [Test]
        public void EvaluateRadius_ClampsToMax()
        {
            Assert.AreEqual(3f, profile.EvaluateRadius(100f), 0.001f);
        }

        [Test]
        public void CreateRequest_UsesEvaluatedRadiusAndPower()
        {
            TerrainBreakRequest request = profile.CreateRequest(4f, Vector2.one, Vector2.up, null,
                                                                TerrainBreakKind.Burst);

            Assert.AreEqual(4f, request.BreakPower, 0.001f);
            Assert.AreEqual(1.5f, request.Radius, 0.001f);
            Assert.AreEqual(TerrainBreakShape.Circle, request.Shape);
        }
    }
}
