using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>运行时地形：搭建、破坏力门槛、范围、追加与重开恢复。</summary>
    public class TerrainTests
    {
        private ArenaController arena;
        private ArenaConfig config;
        private TerrainLayerData layerData;
        private Texture2D mask;
        private GameObject arenaGo;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ArenaController[] oldArenas = Object.FindObjectsByType<ArenaController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < oldArenas.Length; i++) Object.Destroy(oldArenas[i].gameObject);

            yield return null;

            config = ScriptableObject.CreateInstance<ArenaConfig>();
            config.worldRect = new Rect(-4f, -2f, 8f, 4f);
            config.pixelsPerUnit = 2; // 16 x 8

            mask = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            var white = new Color32(255, 255, 255, 255);
            var pixels = new Color32[16 * 8];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
            mask.SetPixels32(pixels);
            mask.Apply(false, false);

            layerData = new TerrainLayerData
            {
                layerId = "Ground",
                destructible = true,
                requiredBreakPower = 1f,
                collisionEnabled = true,
                initialMask = mask
            };
            config.layers.Add(layerData);

            arenaGo = new GameObject("Arena_Test");
            arena = arenaGo.AddComponent<ArenaController>();
            arena.Configure(config, null, null);
            arena.BuildForBattle();

            yield return null;
            Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arenaGo != null) Object.Destroy(arenaGo);
            if (config != null) Object.Destroy(config);
            if (mask != null) Object.Destroy(mask);
            yield return null;
        }

        private static int CenterIndex(TerrainLayer layer)
        {
            int x = layer.MaskWidth / 2;
            int y = layer.MaskHeight / 2;
            return x + y * layer.MaskWidth;
        }

        private static void RefreshPhysics()
        {
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator BuildForBattle_CreatesLayerAndCollider()
        {
            Assert.AreEqual(1, arena.Layers.Count);
            Assert.IsNotNull(arena.Physics);
            Assert.Greater(arena.Physics.ContourCount, 0, "整块遮罩应生成至少一个闭合轮廓");
            Assert.IsTrue(arena.IsTerrainCollider(arena.Physics.GetComponentInChildren<Collider2D>()));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplyTerrainBreak_RemovesMaskAndRebuilds()
        {
            TerrainLayer layer = arena.Layers[0];
            int before = arena.Physics.ContourCount;
            Assert.AreEqual(255, layer.MaskPixels[CenterIndex(layer)].r);

            TerrainBreakResult result = arena.ApplyTerrainBreak(
                TerrainBreakRequest.Circle(2f, 1f, Vector2.zero, null, TerrainBreakKind.Impact));

            RefreshPhysics();

            Assert.IsTrue(result.AnythingChanged);
            Assert.Greater(result.AffectedPixels, 0);
            Assert.AreEqual(0, layer.MaskPixels[CenterIndex(layer)].r, "中心像素应被抹除");
            Assert.GreaterOrEqual(arena.Physics.ContourCount, before, "挖洞后应重建轮廓");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplyTerrainBreak_InsufficientPower_DoesNothing()
        {
            TerrainLayer layer = arena.Layers[0];
            layerData.requiredBreakPower = 3f;
            int before = layer.MaskPixels[CenterIndex(layer)].r;

            TerrainBreakResult result = arena.ApplyTerrainBreak(
                TerrainBreakRequest.Circle(1f, 2f, Vector2.zero, null, TerrainBreakKind.Impact));

            Assert.IsFalse(result.AnythingChanged);
            Assert.AreEqual(before, layer.MaskPixels[CenterIndex(layer)].r);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplyTerrainBreak_IndestructibleLayer_DoesNothing()
        {
            TerrainLayer layer = arena.Layers[0];
            layerData.destructible = false;
            int before = layer.MaskPixels[CenterIndex(layer)].r;

            TerrainBreakResult result = arena.ApplyTerrainBreak(
                TerrainBreakRequest.Circle(99f, 2f, Vector2.zero, null, TerrainBreakKind.Burst));

            Assert.IsFalse(result.AnythingChanged);
            Assert.AreEqual(before, layer.MaskPixels[CenterIndex(layer)].r);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplyTerrainBreak_LargerRadius_RemovesMore()
        {
            TerrainLayer layer = arena.Layers[0];

            TerrainBreakResult small = arena.ApplyTerrainBreak(
                TerrainBreakRequest.Circle(2f, 0.5f, Vector2.zero, null, TerrainBreakKind.Impact));
            int afterSmall = CountEmpty(layer);

            arena.BuildForBattle();
            yield return null;
            layer = arena.Layers[0];

            TerrainBreakResult large = arena.ApplyTerrainBreak(
                TerrainBreakRequest.Circle(2f, 1.5f, Vector2.zero, null, TerrainBreakKind.Impact));
            int afterLarge = CountEmpty(layer);

            Assert.Greater(small.AffectedPixels, 0);
            Assert.Greater(large.AffectedPixels, small.AffectedPixels);
            Assert.Greater(afterLarge, afterSmall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AppendTerrainCircle_RestoresMask()
        {
            TerrainLayer layer = arena.Layers[0];
            arena.ApplyTerrainBreak(TerrainBreakRequest.Circle(2f, 1f, Vector2.zero, null, TerrainBreakKind.Impact));
            Assert.AreEqual(0, layer.MaskPixels[CenterIndex(layer)].r);

            int appended = arena.AppendTerrainCircle("Ground", Vector2.zero, 1f);
            RefreshPhysics();

            Assert.Greater(appended, 0);
            Assert.AreEqual(255, layer.MaskPixels[CenterIndex(layer)].r);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildForBattle_Restart_RestoresInitialMask()
        {
            TerrainLayer layer = arena.Layers[0];
            arena.ApplyTerrainBreak(TerrainBreakRequest.Circle(2f, 1f, Vector2.zero, null, TerrainBreakKind.Impact));
            Assert.AreEqual(0, layer.MaskPixels[CenterIndex(layer)].r);

            arena.BuildForBattle();
            yield return null;

            layer = arena.Layers[0];
            Assert.AreEqual(255, layer.MaskPixels[CenterIndex(layer)].r, "重开应复制初始遮罩");
        }

        [UnityTest]
        public IEnumerator TerrainCollider_Raycast_And_BreakMovesBoundary()
        {
            RaycastHit2D before = Physics2D.Raycast(new Vector2(0f, 3f), Vector2.down, 10f);
            Assert.IsNotNull(before.collider, "地形 EdgeCollider 应能被射线命中");
            float topY = before.point.y;

            arena.ApplyTerrainBreak(
                TerrainBreakRequest.Circle(2f, 1f, new Vector2(0f, 1.5f), null, TerrainBreakKind.Impact));
            RefreshPhysics();

            RaycastHit2D after = Physics2D.Raycast(new Vector2(0f, 3f), Vector2.down, 10f);
            Assert.IsNotNull(after.collider);
            Assert.Less(after.point.y, topY - 0.1f, "顶部挖空后射线应落到更低的边界");
            yield return null;
        }

        private static int CountEmpty(TerrainLayer layer)
        {
            int count = 0;
            Color32[] pixels = layer.MaskPixels;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r < 128) count++;
            return count;
        }
    }
}

