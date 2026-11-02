using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>飞行影响积分与引力场数学。</summary>
    public class ProjectileInfluenceTests
    {
        [Test]
        public void Solver_AppliesAcceleration()
        {
            ProjectileInfluence influence = ProjectileInfluence.None;
            influence.Acceleration = new Vector2(0f, -10f);

            Vector2 velocity = ProjectileInfluenceSolver.Integrate(Vector2.right * 5f, influence, 0.5f);

            Assert.AreEqual(5f, velocity.x, 0.001f);
            Assert.AreEqual(-5f, velocity.y, 0.001f);
        }

        [Test]
        public void Solver_AppliesVelocityScale()
        {
            ProjectileInfluence influence = ProjectileInfluence.None;
            influence.VelocityScale = 0.5f;

            Vector2 velocity = ProjectileInfluenceSolver.Integrate(Vector2.right * 10f, influence, 0.02f);

            Assert.AreEqual(5f, velocity.x, 0.001f);
        }

        [Test]
        public void Solver_OverrideWins()
        {
            ProjectileInfluence influence = ProjectileInfluence.None;
            influence.Acceleration = new Vector2(100f, 100f);
            influence.VelocityOverride = Vector2.left * 3f;

            Vector2 velocity = ProjectileInfluenceSolver.Integrate(Vector2.right * 10f, influence, 1f);

            Assert.AreEqual(-3f, velocity.x, 0.001f);
            Assert.AreEqual(0f, velocity.y, 0.001f);
        }

        [Test]
        public void Influence_AddAccumulates()
        {
            ProjectileInfluence a = ProjectileInfluence.None;
            a.Acceleration = new Vector2(1f, 2f);
            a.VelocityScale = 0.5f;

            ProjectileInfluence b = ProjectileInfluence.None;
            b.Acceleration = new Vector2(3f, -1f);
            b.VelocityScale = 0.5f;

            a.Add(b);

            Assert.AreEqual(4f, a.Acceleration.x, 0.001f);
            Assert.AreEqual(1f, a.Acceleration.y, 0.001f);
            Assert.AreEqual(0.25f, a.VelocityScale, 0.001f);
        }

        [Test]
        public void GravityField_PullsTowardCenter()
        {
            Vector2 acceleration = RadialGravityField.ComputeAcceleration(
                new Vector2(2f, 0f), Vector2.zero, 30f, 4f, 60f, 0.5f);

            Assert.Less(acceleration.x, 0f);
            Assert.AreEqual(0f, acceleration.y, 0.001f);
        }

        [Test]
        public void GravityField_OutsideRadius_IsZero()
        {
            Vector2 acceleration = RadialGravityField.ComputeAcceleration(
                new Vector2(10f, 0f), Vector2.zero, 30f, 4f, 60f, 0.5f);

            Assert.AreEqual(Vector2.zero, acceleration);
        }

        [Test]
        public void GravityField_ClampsAcceleration()
        {
            Vector2 acceleration = RadialGravityField.ComputeAcceleration(
                new Vector2(0.01f, 0f), Vector2.zero, 1000f, 4f, 60f, 0.01f);

            Assert.LessOrEqual(acceleration.magnitude, 60.001f);
        }
    }
}
