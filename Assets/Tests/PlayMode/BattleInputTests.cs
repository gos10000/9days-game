using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>战斗输入路由：只有当前行动角色能收到瞄准输入。</summary>
    public class BattleInputTests
    {
        private GameObject player1;
        private GameObject player2;
        private GameObject turnGo;
        private GameObject inputGo;
        private CharacterLauncher player1Launcher;
        private CharacterLauncher player2Launcher;
        private FakeInputSource source;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            player1 = CreateCharacter("InputTest_P1");
            player1Launcher = player1.GetComponent<CharacterLauncher>();

            player2 = CreateCharacter("InputTest_P2");
            player2Launcher = player2.GetComponent<CharacterLauncher>();

            turnGo = new GameObject("InputTest_Turn");
            var turn = turnGo.AddComponent<TurnController>();

            yield return null;
            turn.BeginTurn(Team.Player1);

            inputGo = new GameObject("InputTest_Router");
            inputGo.AddComponent<BattleInputController>();

            source = new FakeInputSource();
            GameInput.SetSource(source);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.Reset();

            if (inputGo != null) UnityEngine.Object.Destroy(inputGo);
            if (turnGo != null) UnityEngine.Object.Destroy(turnGo);
            if (player1 != null) UnityEngine.Object.Destroy(player1);
            if (player2 != null) UnityEngine.Object.Destroy(player2);

            yield return null;
        }

        [UnityTest]
        public IEnumerator OnlyCurrentActor_ReceivesAimInput()
        {
            source.Snapshot = new InputSnapshot(
                true, Vector2.zero, true, true, false, false, false);

            yield return null;

            Assert.IsTrue(player1Launcher.IsAiming);
            Assert.IsFalse(player2Launcher.IsAiming);

            source.Snapshot = new InputSnapshot(
                true, Vector2.zero, false, false, true, false, false);

            yield return null;

            Assert.IsFalse(player1Launcher.IsAiming);
            Assert.IsFalse(player2Launcher.IsAiming);
        }

        private static GameObject CreateCharacter(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<Character>();
            go.AddComponent<CharacterLauncher>();
            return go;
        }

        private sealed class FakeInputSource : IInputSource
        {
            public InputSnapshot Snapshot;

            public InputSnapshot Read() => Snapshot;

            public bool WasPressed(GameInputAction action) => false;
        }
    }
}
