using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>子弹特性与飞行影响的真实物理链路。</summary>
    public class ProjectileTraitTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.Destroy(spawned[i]);

            spawned.Clear();
            yield return null;
        }

        private CharacterHealth CreateTarget(Vector3 position)
        {
            var go = new GameObject("Target");
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1f, 2f);

            go.AddComponent<Character>();
            go.AddComponent<CharacterCombat>();
            var health = go.AddComponent<CharacterHealth>();

            spawned.Add(go);
            return health;
        }

        private Projectile CreateBall(ProjectileStats stats, IReadOnlyList<ProjectileTrait> traits,
                                      Vector3 position, Vector2 velocity)
        {
            var go = new GameObject("TestBall");
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = stats.radius;

            var projectile = go.AddComponent<Projectile>();
            projectile.Configure(stats, traits);
            projectile.Launch(velocity, null);

            spawned.Add(go);
            return projectile;
        }

        private static ProjectileStats DefaultStats()
        {
            return new ProjectileStats
            {
                mass = 0.5f,
                radius = 0.25f,
                speed = 10f,
                lifeTime = 5f,
                damage = 10f,
                gravityScale = 0f
            };
        }

        [UnityTest]
        public IEnumerator PierceTrait_HitsTwoTargets()
        {
            CharacterHealth first = CreateTarget(Vector3.zero);
            CharacterHealth second = CreateTarget(new Vector3(2f, 0f, 0f));

            var config = ScriptableObject.CreateInstance<ProjectileConfig>();
            config.mass = 0.5f;
            config.radius = 0.25f;
            config.initialSpeed = 14f;
            config.lifeTime = 5f;
            config.damage = 10f;
            config.gravityScale = 0f;
            config.traits.Add(ScriptableObject.CreateInstance<PierceTrait>());

            ProjectileLoadout loadout = ProjectileBuild.Build(config, null, null, 14f);
            CreateBall(loadout.Stats, loadout.Traits, new Vector3(-3f, 0f, 0f), Vector2.right * 14f);

            float firstBefore = first.Current;
            float secondBefore = second.Current;
            float deadline = Time.time + 3f;

            while ((first.Current >= firstBefore || second.Current >= secondBefore) && Time.time < deadline)
                yield return null;

            Assert.Less(first.Current, firstBefore, "第一个目标应该被命中");
            Assert.Less(second.Current, secondBefore, "穿透后第二个目标也应该被命中");

            Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator GravityField_Accumulate_PullsTowardCenter()
        {
            var fieldGo = new GameObject("GravityWell");
            fieldGo.transform.position = new Vector3(0f, -2f, 0f);
            var field = fieldGo.AddComponent<RadialGravityField>();
            spawned.Add(fieldGo);

            Projectile projectile = CreateBall(DefaultStats(), null, Vector3.zero, Vector2.right * 5f);

            ProjectileInfluence influence = ProjectileInfluence.None;
            field.Accumulate(projectile, ref influence, Time.fixedDeltaTime);

            Assert.Less(influence.Acceleration.y, 0f, "引力中心在下方，加速度应向下");
            yield return null;
        }

        [UnityTest]
        public IEnumerator GravityField_TriggerAffectsFlyingProjectile()
        {
            var fieldGo = new GameObject("GravityWell");
            fieldGo.transform.position = new Vector3(0f, -2f, 0f);

            var fieldCollider = fieldGo.AddComponent<CircleCollider2D>();
            fieldCollider.radius = 6f;
            fieldCollider.isTrigger = true;

            fieldGo.AddComponent<RadialGravityField>();
            spawned.Add(fieldGo);

            Projectile projectile = CreateBall(DefaultStats(), null, new Vector3(-3f, 0f, 0f), Vector2.right * 8f);
            var body = projectile.GetComponent<Rigidbody2D>();

            float deadline = Time.time + 2f;
            while (body.linearVelocity.y >= 0f && Time.time < deadline)
                yield return new WaitForFixedUpdate();

            Assert.Less(body.linearVelocity.y, 0f, "进入引力场后子弹应被拉向下方");
        }

        [UnityTest]
        public IEnumerator Projectile_WithoutOwner_HitsWallAndDespawns()
        {
            var wall = new GameObject("Wall");
            wall.transform.position = new Vector3(2f, 0f, 0f);
            var wallCollider = wall.AddComponent<BoxCollider2D>();
            wallCollider.size = new Vector2(0.5f, 4f);
            spawned.Add(wall);

            Projectile projectile = CreateBall(DefaultStats(), null, Vector3.zero, Vector2.right * 8f);

            float deadline = Time.time + 2f;
            while (projectile != null && Time.time < deadline)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(projectile == null, "无发射者的子弹撞墙后应销毁");
        }

        [UnityTest]
        public IEnumerator Influence_ChangesVelocity_OnFixedUpdate()
        {
            Projectile projectile = CreateBall(DefaultStats(), null, Vector3.zero, Vector2.right * 10f);
            var body = projectile.GetComponent<Rigidbody2D>();

            projectile.AddAcceleration(Vector2.down * 20f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.Less(body.linearVelocity.y, 0f, "外部加速度应改变子弹速度");
        }
    }
}
