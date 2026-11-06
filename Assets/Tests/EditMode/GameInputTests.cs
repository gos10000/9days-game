using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>输入门面的快照、取消消费和测试注入行为。</summary>
    public class GameInputTests
    {
        private FakeInputSource source;

        [SetUp]
        public void SetUp()
        {
            source = new FakeInputSource();
            GameInput.SetSource(source);
        }

        [TearDown]
        public void TearDown()
        {
            GameInput.Reset();
        }

        [Test]
        public void Current_ReturnsSourceSnapshot()
        {
            source.Snapshot = new InputSnapshot(
                true, new Vector2(12f, 34f), true, true, false, true, true);

            InputSnapshot current = GameInput.Current;

            Assert.IsTrue(current.HasPointer);
            Assert.AreEqual(new Vector2(12f, 34f), current.PointerScreen);
            Assert.IsTrue(current.PrimaryPressed);
            Assert.IsTrue(current.PrimaryHeld);
            Assert.IsFalse(current.PrimaryReleased);
            Assert.IsTrue(current.SecondaryPressed);
            Assert.IsTrue(current.CancelPressed);
        }

        [Test]
        public void ConsumeCancel_ReturnsTrueOncePerPress()
        {
            source.Snapshot = new InputSnapshot(
                true, Vector2.zero, false, false, false, false, true);

            Assert.IsTrue(GameInput.ConsumeCancel());
            Assert.IsFalse(GameInput.ConsumeCancel());
        }

        [Test]
        public void SetSource_ResetsCancelConsumption()
        {
            source.Snapshot = new InputSnapshot(
                true, Vector2.zero, false, false, false, false, true);
            Assert.IsTrue(GameInput.ConsumeCancel());

            var next = new FakeInputSource
            {
                Snapshot = new InputSnapshot(
                    true, Vector2.zero, false, false, false, false, true),
            };
            GameInput.SetSource(next);

            Assert.IsTrue(GameInput.ConsumeCancel());
        }

        [Test]
        public void WasPressed_DelegatesToSource()
        {
            source.PressedActions.Add(GameInputAction.TestShot);

            Assert.IsTrue(GameInput.WasPressed(GameInputAction.TestShot));
            Assert.IsFalse(GameInput.WasPressed(GameInputAction.Restart));
        }

        [Test]
        public void PointerWorld_ProjectsToCameraPlane()
        {
            var cameraGo = new GameObject("InputTestCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -5f);

            source.Snapshot = new InputSnapshot(
                true, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                false, false, false, false, false);

            Vector2 world = GameInput.PointerWorld(camera);

            Assert.AreEqual(0f, world.x, 0.01f);
            Assert.AreEqual(0f, world.y, 0.01f);
            Object.DestroyImmediate(cameraGo);
        }

        private sealed class FakeInputSource : IInputSource
        {
            public InputSnapshot Snapshot;
            public readonly HashSet<GameInputAction> PressedActions = new HashSet<GameInputAction>();

            public InputSnapshot Read() => Snapshot;

            public bool WasPressed(GameInputAction action) => PressedActions.Contains(action);
        }
    }
}
