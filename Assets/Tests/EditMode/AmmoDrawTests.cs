using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace NineDays.Tests
{
    /// <summary>纯抽选逻辑：数量、去重、全选、种子与空池。</summary>
    public class AmmoDrawTests
    {
        private readonly List<GameObject> prefabs = new List<GameObject>();
        private readonly List<AmmoDefinition> definitions = new List<AmmoDefinition>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null) Object.DestroyImmediate(definitions[i]);

            for (int i = 0; i < prefabs.Count; i++)
                if (prefabs[i] != null) Object.DestroyImmediate(prefabs[i]);

            definitions.Clear();
            prefabs.Clear();
        }

        [Test]
        public void Pick_ReturnsRequestedCount_WithoutDuplicates()
        {
            var pool = CreatePool(5);
            IReadOnlyList<AmmoDefinition> result = AmmoDraw.Pick(pool, 3, false, new Random(7));

            Assert.AreEqual(3, result.Count);
            var unique = new HashSet<AmmoDefinition>(result);
            Assert.AreEqual(3, unique.Count);
        }

        [Test]
        public void Pick_CountGreaterThanPool_ReturnsAll()
        {
            var pool = CreatePool(2);
            IReadOnlyList<AmmoDefinition> result = AmmoDraw.Pick(pool, 3, false, new Random(7));

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Pick_All_KeepsCatalogOrder()
        {
            var pool = CreatePool(4);
            IReadOnlyList<AmmoDefinition> result = AmmoDraw.Pick(pool, 1, true, new Random(7));

            Assert.AreEqual(pool.Count, result.Count);
            for (int i = 0; i < pool.Count; i++) Assert.AreSame(pool[i], result[i]);
        }

        [Test]
        public void Pick_Seeded_IsDeterministic()
        {
            var pool = CreatePool(5);
            IReadOnlyList<AmmoDefinition> a = AmmoDraw.Pick(pool, 3, false, new Random(123));
            IReadOnlyList<AmmoDefinition> b = AmmoDraw.Pick(pool, 3, false, new Random(123));

            for (int i = 0; i < a.Count; i++) Assert.AreSame(a[i], b[i]);
        }

        [Test]
        public void Pick_EmptyPool_ReturnsEmpty()
        {
            Assert.AreEqual(0, AmmoDraw.Pick(null, 3, false, new Random(1)).Count);
            Assert.AreEqual(0, AmmoDraw.Pick(new List<AmmoDefinition>(), 3, false, new Random(1)).Count);
        }

        [Test]
        public void Pick_ZeroCount_ClampsToOne()
        {
            var pool = CreatePool(3);
            IReadOnlyList<AmmoDefinition> result = AmmoDraw.Pick(pool, 0, false, new Random(1));
            Assert.AreEqual(1, result.Count);
        }

        private List<AmmoDefinition> CreatePool(int count)
        {
            var pool = new List<AmmoDefinition>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"AmmoPrefab_{i}");
                go.AddComponent<Rigidbody2D>();
                Projectile projectile = go.AddComponent<Projectile>();
                prefabs.Add(go);

                var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
                definition.prefab = projectile;
                definition.displayName = $"Ammo_{i}";
                definitions.Add(definition);
                pool.Add(definition);
            }

            return pool;
        }
    }
}
