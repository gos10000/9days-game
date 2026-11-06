using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>弹药目录与数据体的最小校验。</summary>
    public class AmmoCatalogTests
    {
        private readonly List<GameObject> prefabs = new List<GameObject>();
        private readonly List<AmmoDefinition> definitions = new List<AmmoDefinition>();
        private AmmoCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<AmmoCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            if (catalog != null) Object.DestroyImmediate(catalog);
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null) Object.DestroyImmediate(definitions[i]);
            for (int i = 0; i < prefabs.Count; i++)
                if (prefabs[i] != null) Object.DestroyImmediate(prefabs[i]);

            definitions.Clear();
            prefabs.Clear();
        }

        [Test]
        public void Validate_RemovesNullAndMissingPrefab()
        {
            AmmoDefinition validA = CreateDefinition("A");
            AmmoDefinition validB = CreateDefinition("B");
            AmmoDefinition missingPrefab = CreateDefinition("Missing");
            missingPrefab.prefab = null;

            catalog.SetAmmo(new[] { validA, null, missingPrefab, validB });
            catalog.Validate();

            Assert.AreEqual(2, catalog.Count);
            Assert.AreSame(validA, catalog.Get(0));
            Assert.AreSame(validB, catalog.Get(1));
        }

        [Test]
        public void Get_OutOfRange_ReturnsNull()
        {
            Assert.IsNull(catalog.Get(-1));
            Assert.IsNull(catalog.Get(0));
        }

        [Test]
        public void ResolvedName_FallsBackToPrefabName()
        {
            AmmoDefinition definition = CreateDefinition("PrefabName");
            definition.displayName = string.Empty;

            Assert.AreEqual("PrefabName", definition.ResolvedName);
        }

        private AmmoDefinition CreateDefinition(string prefabName)
        {
            var go = new GameObject(prefabName);
            go.AddComponent<Rigidbody2D>();
            Projectile projectile = go.AddComponent<Projectile>();
            prefabs.Add(go);

            var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
            definition.prefab = projectile;
            definition.displayName = prefabName;
            definitions.Add(definition);
            return definition;
        }
    }
}
