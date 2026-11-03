using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>爆发效果与 Projectile 触发链路。</summary>
    public class BurstPlayModeTests
    {
        private sealed class RecordingEffect : BurstEffect
        {
            public int Count;
            public BurstContext Last;

            public override void Execute(in BurstContext context)
            {
                Count++;
                Last = context;
            }
        }

        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private GameObject target;
        private Character targetCharacter;
        private CharacterHealth targetHealth;
        private GameObject ball;
        private Projectile projectile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();

            target = new GameObject("BurstTarget");
            var body = target.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var collider = target.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1f, 2f);
            target.AddComponent<Character>();
            target.AddComponent<CharacterCombat>();
            targetHealth = target.AddComponent<CharacterHealth>();
            targetCharacter = target.GetComponent<Character>();
            target.transform.position = new Vector3(2f, 0f, 0f);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.ClearAll();
            if (ball != null) Object.Destroy(ball);
            if (target != null) Object.Destroy(target);
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null) Object.Destroy(assets[i]);
            assets.Clear();
            yield return null;
        }

        private T Track<T>(T asset) where T : ScriptableObject
        {
            assets.Add(asset);
            return asset;
        }

        private RecordingEffect CreateRecordingEffect()
        {
            var effect = Track(ScriptableObject.CreateInstance<RecordingEffect>());
            effect.Count = 0;
            return effect;
        }

        private BurstDefinition CreateDefinition(BurstTriggerType trigger, RecordingEffect effect)
        {
            var definition = Track(ScriptableObject.CreateInstance<BurstDefinition>());
            definition.trigger = trigger;
            definition.effects = new List<BurstEffect> { effect };
            return definition;
        }

        private BurstContext Context(Vector2 point, BurstDefinition definition = null)
        {
            return new BurstContext(null, definition, BurstTriggerType.Manual, 1,
                                    point, Vector2.up, default, false);
        }

        [UnityTest]
        public IEnumerator BurstDamageEffect_DamagesTargetInRadius()
        {
            var effect = Track(ScriptableObject.CreateInstance<BurstDamageEffect>());
            effect.radius = 3f;
            effect.damage = 15f;
            effect.impulse = 0f;

            float before = targetHealth.Current;
            effect.Execute(Context(Vector2.zero));

            Assert.AreEqual(before - 15f, targetHealth.Current, 0.001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BurstForceEffect_PullsTaget()
        {
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            target.transform.position = new Vector3(2f, 0f, 0f);

            var effect = Track(ScriptableObject.CreateInstance<BurstForceEffect>());
            effect.radius = 3f;
            effect.force = 5f;
            effect.mode = BurstForceMode.Pull;

            effect.Execute(Context(Vector2.zero));
            yield return new WaitForFixedUpdate();

            Assert.Less(target.GetComponent<Rigidbody2D>().linearVelocity.x, 0f, "Pull 应把目标拉向中心");
        }

        [UnityTest]
        public IEnumerator ExpireBurst_TriggersBeforePoolReturn()
        {
            var effect = CreateRecordingEffect();
            var definition = CreateDefinition(BurstTriggerType.OnExpire, effect);
            definition.maxTriggers = 1;

            var config = Track(ScriptableObject.CreateInstance<ProjectileConfig>());
            config.mass = 0.5f;
            config.radius = 0.25f;
            config.lifeTime = 5f;
            config.gravityScale = 0f;
            config.bursts = new List<BurstDefinition> { definition };

            ball = new GameObject("ExpireBall");
            ball.transform.position = new Vector3(5f, 5f, 0f);
            var body = ball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var collider = ball.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            projectile = ball.AddComponent<Projectile>();
            projectile.Configure(config);
            projectile.Launch(Vector2.right, null);

            projectile.Despawn(ExpireReason.Aborted);
            projectile.Despawn(ExpireReason.Aborted);

            Assert.AreEqual(1, effect.Count, "OnExpire 爆发只应触发一次");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TimerBurst_TriggersRepeatedly()
        {
            var effect = CreateRecordingEffect();
            var definition = CreateDefinition(BurstTriggerType.OnTimer, effect);
            definition.delay = 0.05f;
            definition.cooldown = 0.05f;
            definition.maxTriggers = 2;

            var config = Track(ScriptableObject.CreateInstance<ProjectileConfig>());
            config.mass = 0.5f;
            config.radius = 0.25f;
            config.lifeTime = 5f;
            config.damage = 0f;
            config.gravityScale = 0f;
            config.bursts = new List<BurstDefinition> { definition };

            ball = new GameObject("BurstBall");
            ball.transform.position = new Vector3(5f, 5f, 0f);
            var body = ball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var collider = ball.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            projectile = ball.AddComponent<Projectile>();
            projectile.Configure(config);
            projectile.Launch(Vector2.zero, null);

            yield return new WaitForSeconds(0.4f);
            Assert.GreaterOrEqual(effect.Count, 2);
            Assert.AreEqual(2, projectile.BurstRuntimes[0].TriggerCount);
        }

        [UnityTest]
        public IEnumerator ImpactBurst_AfterDirectHit()
        {
            target.transform.position = new Vector3(2f, 0f, 0f);
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;

            var effect = CreateRecordingEffect();
            var definition = CreateDefinition(BurstTriggerType.OnImpact, effect);
            definition.maxTriggers = 1;
            definition.impactFilter = BurstImpactFilter.Any;

            var config = Track(ScriptableObject.CreateInstance<ProjectileConfig>());
            config.mass = 0.5f;
            config.radius = 0.25f;
            config.lifeTime = 5f;
            config.damage = 10f;
            config.destroyOnHit = true;
            config.gravityScale = 0f;
            config.bursts = new List<BurstDefinition> { definition };

            float before = targetHealth.Current;
            ball = new GameObject("ImpactBall");
            ball.transform.position = Vector3.zero;
            var body = ball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var collider = ball.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            projectile = ball.AddComponent<Projectile>();
            projectile.Configure(config);
            projectile.Launch(Vector2.right * 12f, null);

            float deadline = Time.time + 3f;
            while (effect.Count == 0 && Time.time < deadline) yield return null;

            Assert.AreEqual(1, effect.Count, "Impact 爆发应触发一次");
            Assert.AreEqual(before - 10f, targetHealth.Current, 0.001f, "直接命中应先结算");
            Assert.AreEqual(1, effect.Last.TriggerCount);
        }

        [UnityTest]
        public IEnumerator TerrainBurstEffect_ErasesTerrain()
        {
            var mask = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            var pixels = new Color32[16 * 8];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            mask.SetPixels32(pixels);
            mask.Apply(false, false);

            var arenaConfig = Track(ScriptableObject.CreateInstance<ArenaConfig>());
            arenaConfig.worldRect = new Rect(-4f, -2f, 8f, 4f);
            arenaConfig.pixelsPerUnit = 2;
            var layer = new TerrainLayerData
            {
                layerId = "Test",
                destructible = true,
                requiredBreakPower = 1f,
                collisionEnabled = true,
                initialMask = mask
            };
            arenaConfig.layers.Add(layer);

            var arenaGo = new GameObject("BurstArena");
            var arena = arenaGo.AddComponent<ArenaController>();
            arena.Configure(arenaConfig, null, null);
            arena.BuildForBattle();
            yield return null;

            var effect = Track(ScriptableObject.CreateInstance<BurstTerrainEffect>());
            effect.breakPower = 2f;
            effect.radius = 1f;
            effect.shape = TerrainBreakShape.Circle;

            effect.Execute(Context(Vector2.zero));
            int center = arena.Layers[0].MaskWidth / 2 +
                         (arena.Layers[0].MaskHeight / 2) * arena.Layers[0].MaskWidth;
            Assert.AreEqual(0, arena.Layers[0].MaskPixels[center].r);

            Object.Destroy(arenaGo);
            Object.Destroy(mask);
            yield return null;
        }
    }
}

