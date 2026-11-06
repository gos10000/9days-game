using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>关卡/覆盖/槽位/出生点的 PlayMode 验收。</summary>
    public class BattleSceneInitializerTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private BattleSceneInitializer initializer;
        private TurnController turnController;
        private Character player1;
        private Character player2;
        private BattleLevel levelA;
        private BattleLevel levelB;
        private GameObject backgroundA;
        private GameObject backgroundB;
        private GameObject arenaA;
        private GameObject arenaB;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            backgroundA = Track(new GameObject("BG_A"));
            backgroundB = Track(new GameObject("BG_B"));
            arenaA = Track(new GameObject("AR_A"));
            arenaB = Track(new GameObject("AR_B"));

            levelA = CreateLevel("Level_A", backgroundA, arenaA, new Vector2(-2f, -1f), new Vector2(2f, 1f));
            levelB = CreateLevel("Level_B", backgroundB, arenaB, new Vector2(-3f, -2f), new Vector2(3f, 2f));

            player1 = CreateCharacter(Team.Player1, new Vector3(-9f, 0f, 0f));
            player2 = CreateCharacter(Team.Player2, new Vector3(9f, 0f, 0f));

            turnController = Track(new GameObject("TurnController")).AddComponent<TurnController>();
            SetCharacterList(turnController, player1, player2);

            initializer = Track(new GameObject("BattleSceneInitializer"))
                .AddComponent<BattleSceneInitializer>();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            spawned.Clear();

            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplyLevel_UsesPrefabsAndSpawns()
        {
            Assert.IsTrue(initializer.ApplyLevel(levelA));

            Assert.AreSame(levelA, initializer.ActiveLevel);
            Assert.AreSame(backgroundA, initializer.ActiveBackground);
            Assert.AreSame(arenaA, initializer.ActiveArena);
            Assert.IsNotNull(initializer.BackgroundSlot.Instance);
            Assert.IsNotNull(initializer.ArenaSlot.Instance);
            Assert.AreEqual(new Vector3(-2f, -1f, 0f), player1.SpawnPoint);
            Assert.AreEqual(new Vector3(2f, 1f, 0f), player2.SpawnPoint);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelOverride_Wins()
        {
            initializer.ApplyLevel(levelA);
            initializer.SetLevelOverride(levelB);
            Assert.IsTrue(initializer.Apply());

            Assert.AreSame(levelB, initializer.ActiveLevel);
            Assert.AreSame(backgroundB, initializer.ActiveBackground);
            Assert.AreSame(arenaB, initializer.ActiveArena);
            Assert.AreEqual(new Vector3(-3f, -2f, 0f), player1.SpawnPoint);

            yield return null;
        }

        [UnityTest]
        public IEnumerator BackgroundOverride_KeepsArenaFromLevel()
        {
            initializer.ApplyLevel(levelA);
            initializer.SetOverrides(backgroundB, null);
            Assert.IsTrue(initializer.Apply());

            Assert.AreSame(backgroundB, initializer.ActiveBackground);
            Assert.AreSame(arenaA, initializer.ActiveArena);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ArenaOverride_KeepsBackgroundFromLevel()
        {
            initializer.ApplyLevel(levelA);
            initializer.SetOverrides(null, arenaB);
            Assert.IsTrue(initializer.Apply());

            Assert.AreSame(backgroundA, initializer.ActiveBackground);
            Assert.AreSame(arenaB, initializer.ActiveArena);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NoLevel_DoesNothing()
        {
            Assert.IsFalse(initializer.Apply());
            Assert.IsNull(initializer.ActiveLevel);
            Assert.IsNull(initializer.BackgroundSlot.Instance);
            Assert.IsNull(initializer.ArenaSlot.Instance);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SamePrefab_DoesNotReinstantiate()
        {
            initializer.ApplyLevel(levelA);
            GameObject firstBackground = initializer.BackgroundSlot.Instance;
            GameObject firstArena = initializer.ArenaSlot.Instance;

            initializer.ApplyLevel(levelA);

            Assert.AreSame(firstBackground, initializer.BackgroundSlot.Instance);
            Assert.AreSame(firstArena, initializer.ArenaSlot.Instance);

            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleController_BeginBattle_UsesLevelSpawn()
        {
            initializer.ApplyLevel(levelA);

            var config = ScriptableObject.CreateInstance<BattleConfig>();
            var battle = Track(new GameObject("BattleController")).AddComponent<BattleController>();
            SetField(battle, "config", config);
            SetField(battle, "turnController", turnController);
            SetField(battle, "characters", new List<Character> { player1, player2 });
            SetField(battle, "sceneInitializer", initializer);
            SetField(battle, "autoStart", false);
            SetField(battle, "showTurnBanner", false);

            battle.BeginBattle();
            yield return null;

            Assert.AreEqual(new Vector2(-2f, -1f), player1.Body.position);
            Assert.AreEqual(new Vector2(2f, 1f), player2.Body.position);

            Object.Destroy(config);
        }

        private BattleLevel CreateLevel(string name, GameObject background, GameObject arena,
                                        Vector2 p1, Vector2 p2)
        {
            var go = Track(new GameObject(name));
            var level = go.AddComponent<BattleLevel>();
            level.backgroundPrefab = background;
            level.arenaPrefab = arena;
            level.player1Spawn = p1;
            level.player2Spawn = p2;
            return level;
        }

        private Character CreateCharacter(Team team, Vector3 position)
        {
            var go = Track(new GameObject($"Character_{team}"));
            go.transform.position = position;
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            go.AddComponent<CapsuleCollider2D>();
            Character character = go.AddComponent<Character>();
            SetField(character, "team", team);
            return character;
        }

        private void SetCharacterList(TurnController controller, params Character[] characters)
        {
            FieldInfo field = typeof(TurnController).GetField("characters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var list = (List<Character>)field.GetValue(controller);
            list.Clear();
            for (int i = 0; i < characters.Length; i++) list.Add(characters[i]);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }
    }
}
