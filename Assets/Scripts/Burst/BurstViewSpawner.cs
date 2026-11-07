using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 爆发特效的统一出口：订阅 BurstTriggered，按 BurstDefinition.viewPrefab 在爆发点生成特效。
    /// 同时提供静态 Spawn，供子弹撞击等非爆发场景复用（撞击特效）。
    /// </summary>
    public sealed class BurstViewSpawner : MonoBehaviour
    {
        [Tooltip("找不到粒子系统时，特效实例的统一存活时间")]
        [SerializeField] private float fallbackLifetime = 3f;

        private static BurstViewSpawner instance;

        public static BurstViewSpawner EnsureExists()
        {
            if (instance != null) return instance;

            var existing = FindAnyObjectByType<BurstViewSpawner>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            var go = new GameObject("BurstViewSpawner");
            instance = go.AddComponent<BurstViewSpawner>();
            return instance;
        }

        private void Awake()
        {
            instance = this;
        }

        private void OnEnable()
        {
            GameEvents.BurstTriggered += OnBurstTriggered;
        }

        private void OnDisable()
        {
            GameEvents.BurstTriggered -= OnBurstTriggered;
        }

        private void OnBurstTriggered(Projectile projectile, BurstDefinition definition, BurstContext context)
        {
            if (definition == null) return;
            Spawn(definition.viewPrefab, context.Point, fallbackLifetime);
        }

        /// <summary>在世界坐标生成一个特效实例，并按粒子时长自动销毁。</summary>
        public static void Spawn(GameObject prefab, Vector2 point, float fallback = 3f)
        {
            if (prefab == null) return;

            GameObject fx = Instantiate(prefab);
            fx.transform.position = point;

            Object.Destroy(fx, ResolveLifetime(fx, fallback));
        }

        private static float ResolveLifetime(GameObject fx, float fallback)
        {
            ParticleSystem particles = fx.GetComponentInChildren<ParticleSystem>();
            if (particles == null) return Mathf.Max(0.5f, fallback);

            ParticleSystem.MainModule main = particles.main;
            float lifetime = main.duration + main.startLifetime.constantMax;
            return Mathf.Max(0.5f, lifetime);
        }
    }
}
