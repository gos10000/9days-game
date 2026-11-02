using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>真实物理链路：子弹命中角色 → 扣血 → CharacterHit 事件。</summary>
    public class ProjectileHitTests
    {
        private GameObject target;
        private GameObject ball;
        private int hitCount;
        private HitInfo lastHit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hitCount = 0;
            lastHit = default;
            GameEvents.CharacterHit += OnHit;

            target = new GameObject("Target");
            var body = target.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var collider = target.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1f, 2f);
            target.AddComponent<Character>();
            target.AddComponent<CharacterCombat>();
            target.AddComponent<CharacterHealth>();
            target.transform.position = Vector3.zero;

            yield return null;   // 等 Awake / OnEnable 完成
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.CharacterHit -= OnHit;

            if (ball != null) Object.Destroy(ball);
            if (target != null) Object.Destroy(target);

            yield return null;
        }

        private void OnHit(Character character, HitInfo info)
        {
            hitCount++;
            lastHit = info;
        }

        [UnityTest]
        public IEnumerator ProjectileHit_DamagesTarget_AndRaisesEvent()
        {
            var health = target.GetComponent<CharacterHealth>();
            float before = health.Current;

            var config = ScriptableObject.CreateInstance<ProjectileConfig>();
            config.damage = 25f;
            config.mass = 0.5f;
            config.radius = 0.25f;
            config.lifeTime = 5f;
            config.ignoreOwnerTime = 0.15f;
            config.destroyOnHit = true;

            ball = new GameObject("TestBall");
            ball.transform.position = new Vector3(-2f, 0f, 0f);

            var body = ball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            var collider = ball.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;

            var projectile = ball.AddComponent<Projectile>();
            projectile.Configure(config);
            projectile.Launch(Vector2.right * 12f, null);

            float deadline = Time.time + 3f;
            while (hitCount == 0 && Time.time < deadline) yield return null;

            Assert.AreEqual(1, hitCount, "应该恰好命中一次");
            Assert.AreEqual(25f, lastHit.Damage, 0.001f, "事件应携带子弹伤害");
            Assert.AreEqual(before - 25f, health.Current, 0.001f, "目标应扣除 25 点血");
            Assert.Less(health.Current, before);
        }
    }
}
