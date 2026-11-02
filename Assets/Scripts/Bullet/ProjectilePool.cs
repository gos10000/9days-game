using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace NineDays
{
    /// <summary>
    /// 子弹对象池：按 prefab 维护，场景级单例。
    /// 生成只走 Rent，回收只走 Release；池空时扩容，超过上限自动销毁。
    /// </summary>
    public sealed class ProjectilePool : MonoBehaviour
    {
        [Header("预热")]
        [Tooltip("场景启动时预热的子弹 prefab")]
        [SerializeField] private Projectile prewarmPrefab;

        [Tooltip("预热数量")]
        [SerializeField, Min(0)] private int prewarmCount = 8;

        [Header("容量")]
        [Tooltip("每个 prefab 的初始容量")]
        [SerializeField, Min(1)] private int defaultCapacity = 8;

        [Tooltip("每个 prefab 的池上限，超出后销毁溢出对象")]
        [SerializeField, Min(1)] private int maxSizePerPrefab = 64;

        [Tooltip("重复归还时直接报错，便于尽早发现 bug")]
        [SerializeField] private bool collectionCheck = true;

        [Tooltip("跨场景保留（单场景先关）")]
        [SerializeField] private bool dontDestroyOnLoad;

        private static ProjectilePool instance;

        private readonly Dictionary<Projectile, ObjectPool<Projectile>> pools =
            new Dictionary<Projectile, ObjectPool<Projectile>>();

        private Transform poolRoot;

        public static ProjectilePool Instance
        {
            get
            {
                if (instance != null) return instance;

                instance = FindAnyObjectByType<ProjectilePool>();
                if (instance == null)
                {
                    var go = new GameObject("[ProjectilePool]");
                    instance = go.AddComponent<ProjectilePool>();
                }

                return instance;
            }
        }

        public Projectile PrewarmPrefab => prewarmPrefab;
        public int DefaultCapacity => defaultCapacity;
        public int MaxSizePerPrefab => maxSizePerPrefab;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            EnsureRoot();
            if (prewarmPrefab != null && prewarmCount > 0) Prewarm(prewarmPrefab, prewarmCount);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>从池中取出一颗子弹；池空时实例化。</summary>
        public Projectile Rent(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            EnsureRoot();
            Projectile projectile = GetPool(prefab).Get();
            projectile.Pool = this;
            projectile.SourcePrefab = prefab;
            projectile.MarkRented();
            projectile.transform.SetParent(poolRoot, false);
            projectile.transform.SetPositionAndRotation(position, rotation);
            return projectile;
        }

        /// <summary>归还子弹；只接受本池租出的对象。</summary>
        public bool Return(Projectile projectile)
        {
            if (projectile == null || projectile.Pool != this) return false;

            Projectile prefab = projectile.SourcePrefab;
            if (prefab == null) return false;

            projectile.Pool = null;
            GetPool(prefab).Release(projectile);
            return true;
        }

        /// <summary>预热指定数量，避免第一发卡顿。</summary>
        public void Prewarm(Projectile prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            EnsureRoot();
            ObjectPool<Projectile> pool = GetPool(prefab);

            // 先全部取出再统一归还，否则每次 Get 都会拿到刚归还的同一个实例
            var created = new List<Projectile>(count);
            for (int i = 0; i < count; i++) created.Add(pool.Get());
            for (int i = 0; i < created.Count; i++) pool.Release(created[i]);
        }

        public int CountInactive(Projectile prefab)
        {
            return prefab != null && pools.TryGetValue(prefab, out ObjectPool<Projectile> pool)
                ? pool.CountInactive
                : 0;
        }

        public int CountActive(Projectile prefab)
        {
            return prefab != null && pools.TryGetValue(prefab, out ObjectPool<Projectile> pool)
                ? pool.CountActive
                : 0;
        }

        /// <summary>销毁所有池化对象（场景卸载 / 测试清理）。</summary>
        public void Clear()
        {
            foreach (ObjectPool<Projectile> pool in pools.Values) pool.Clear();
            pools.Clear();
        }

        private void EnsureRoot()
        {
            if (poolRoot != null) return;

            var root = new GameObject("Pooled Projectiles");
            root.transform.SetParent(transform, false);
            poolRoot = root.transform;
        }

        private ObjectPool<Projectile> GetPool(Projectile prefab)
        {
            if (pools.TryGetValue(prefab, out ObjectPool<Projectile> pool)) return pool;

            pool = new ObjectPool<Projectile>(
                createFunc: () => CreateInstance(prefab),
                actionOnGet: projectile => projectile.gameObject.SetActive(true),
                actionOnRelease: projectile =>
                {
                    projectile.ResetForPool();
                    projectile.transform.SetParent(poolRoot, false);
                    projectile.gameObject.SetActive(false);
                },
                actionOnDestroy: projectile =>
                {
                    if (projectile != null) Destroy(projectile.gameObject);
                },
                collectionCheck: collectionCheck,
                defaultCapacity: defaultCapacity,
                maxSize: maxSizePerPrefab);

            pools.Add(prefab, pool);
            return pool;
        }

        private Projectile CreateInstance(Projectile prefab)
        {
            Projectile projectile = Instantiate(prefab, Vector3.zero, Quaternion.identity, poolRoot);
            projectile.SourcePrefab = prefab;
            projectile.gameObject.SetActive(false);
            return projectile;
        }
    }
}
