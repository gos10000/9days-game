using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>子弹对象池：复用、事件、预热、回合判定兼容。</summary>
    public class ProjectilePoolTests
    {
        private ProjectilePool pool;
        private GameObject prefab;
        private Projectile prefabProjectile;
        private int expiredCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            expiredCount = 0;
            GameEvents.ProjectileExpired += OnExpired;

            // 清理上一个用例可能留下的子弹与池，保证计数干净
            Projectile[] leftovers = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < leftovers.Length; i++) Object.Destroy(leftovers[i].gameObject);

            ProjectilePool[] oldPools = Object.FindObjectsByType<ProjectilePool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < oldPools.Length; i++) Object.Destroy(oldPools[i].gameObject);

            yield return null;

            prefab = new GameObject("PoolTestBall");
            var body = prefab.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var collider = prefab.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            prefabProjectile = prefab.AddComponent<Projectile>();
            prefab.SetActive(false);

            pool = new GameObject("ProjectilePool_Test").AddComponent<ProjectilePool>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.ProjectileExpired -= OnExpired;

            if (pool != null) Object.Destroy(pool.gameObject);
            if (prefab != null) Object.Destroy(prefab);

            yield return null;
        }

        private void OnExpired(Projectile projectile, ExpireReason reason)
        {
            expiredCount++;
        }

        [UnityTest]
        public IEnumerator Rent_ReusesReturnedInstance()
        {
            Projectile first = pool.Rent(prefabProjectile, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(first);
            int firstId = first.GetInstanceID();

            first.Launch(Vector2.right, null);
            first.Despawn(ExpireReason.Aborted);

            Projectile second = pool.Rent(prefabProjectile, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(firstId, second.GetInstanceID(), "回收后再次取出应复用同一实例");
            Assert.IsTrue(second.gameObject.activeSelf, "复用后应处于激活状态");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Despawn_DeactivatesAndFiresExpiredOnce()
        {
            Projectile projectile = pool.Rent(prefabProjectile, Vector3.zero, Quaternion.identity);
            projectile.Launch(Vector2.right, null);

            projectile.Despawn(ExpireReason.Hit);
            projectile.Despawn(ExpireReason.Hit);   // 重复调用不应再次触发

            yield return null;

            Assert.IsFalse(projectile.gameObject.activeSelf, "回收后应失活");
            Assert.AreEqual(1, expiredCount, "ProjectileExpired 只应触发一次");
            Assert.AreEqual(1, pool.CountInactive(prefabProjectile), "池内应有 1 个空闲实例");
        }

        [UnityTest]
        public IEnumerator Prewarm_CreatesInactiveInstances()
        {
            pool.Prewarm(prefabProjectile, 3);

            yield return null;

            Assert.AreEqual(3, pool.CountInactive(prefabProjectile));
            Assert.AreEqual(0, pool.CountActive(prefabProjectile));
        }

        [UnityTest]
        public IEnumerator PooledProjectile_NotReturnedByFindObjects()
        {
            Projectile projectile = pool.Rent(prefabProjectile, Vector3.zero, Quaternion.identity);
            projectile.Launch(Vector2.right, null);
            projectile.Despawn(ExpireReason.Aborted);

            yield return null;

            Projectile[] active = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            Assert.AreEqual(0, active.Length, "池内失活子弹不应被 FindObjectsByType 统计");
        }

        [UnityTest]
        public IEnumerator Return_RejectsForeignProjectile()
        {
            var foreignGo = new GameObject("ForeignBall");
            foreignGo.AddComponent<Rigidbody2D>();
            foreignGo.AddComponent<CircleCollider2D>();
            var foreign = foreignGo.AddComponent<Projectile>();

            Assert.IsFalse(pool.Return(foreign), "非本池对象不应被回收");

            Object.Destroy(foreignGo);
            yield return null;
        }
    }
}
