using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>选弹门流程：选项数量、输入门、应用到 launcher、onReady 一次。</summary>
    public class AmmoSelectionFlowTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<AmmoDefinition> definitions = new List<AmmoDefinition>();
        private AmmoCatalog catalog;
        private TurnController turn;
        private BattleInputController input;
        private AmmoSelectionController selection;
        private Character player1;
        private FakePresenter presenter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            catalog = ScriptableObject.CreateInstance<AmmoCatalog>();

            definitions.Add(CreateAmmo("Ammo_A"));
            definitions.Add(CreateAmmo("Ammo_B"));
            catalog.SetAmmo(definitions);

            player1 = CreateCharacter(Team.Player1);
            CreateCharacter(Team.Player2);

            turn = Track(new GameObject("TurnController")).AddComponent<TurnController>();
            input = Track(new GameObject("BattleInput")).AddComponent<BattleInputController>();

            selection = Track(new GameObject("AmmoSelection")).AddComponent<AmmoSelectionController>();
            presenter = new FakePresenter();
            selection.Configure(catalog, 2, false, 42, presenter, turn, input);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) UnityEngine.Object.Destroy(spawned[i]);
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null) UnityEngine.Object.Destroy(definitions[i]);
            if (catalog != null) UnityEngine.Object.Destroy(catalog);

            spawned.Clear();
            definitions.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Selection_AppliesPrefab_AndStartsTurn()
        {
            bool ready = false;
            selection.BeginTurn(Team.Player1, () => ready = true);

            Assert.AreEqual(2, presenter.Options.Count);
            Assert.IsFalse(input.InputEnabled);
            Assert.IsFalse(ready);

            presenter.Select(1);

            Assert.IsTrue(ready);
            Assert.IsTrue(input.InputEnabled);
            CharacterLauncher launcher = player1.GetComponent<CharacterLauncher>();
            Assert.AreSame(definitions[1].prefab, launcher.ActiveProjectilePrefab);

            yield return null;
        }

        private AmmoDefinition CreateAmmo(string name)
        {
            var go = Track(new GameObject(name));
            go.AddComponent<Rigidbody2D>();
            Projectile projectile = go.AddComponent<Projectile>();

            var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
            definition.prefab = projectile;
            definition.displayName = name;
            return definition;
        }

        private Character CreateCharacter(Team team)
        {
            var go = Track(new GameObject($"Character_{team}"));
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            go.AddComponent<CapsuleCollider2D>();
            Character character = go.AddComponent<Character>();
            SetField(character, "team", team);
            go.AddComponent<CharacterLauncher>();
            return character;
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }

        private sealed class FakePresenter : IAmmoSelectPresenter
        {
            private Action<int> onSelected;

            public List<AmmoOption> Options { get; } = new List<AmmoOption>();

            public bool Show(IReadOnlyList<AmmoOption> options, Action<int> selected)
            {
                Options.Clear();
                if (options != null)
                    for (int i = 0; i < options.Count; i++) Options.Add(options[i]);

                onSelected = selected;
                return true;
            }

            public void Hide()
            {
                onSelected = null;
            }

            public void Select(int index)
            {
                onSelected?.Invoke(index);
            }
        }
    }
}
