using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹原型脚本（文档「不同子弹复用原型脚本」）。
    /// 数值来自 ProjectileStats，行为来自 ProjectileTrait 列表，飞行影响来自 ProjectileInfluence。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private ProjectileConfig config;

        [Header("引用")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private TrailRenderer trail;

        private ProjectileStats stats;
        private List<ProjectileTrait> traits;
        private List<BurstDefinition> burstDefinitions;
        private readonly List<BurstRuntime> burstRuntimes = new List<BurstRuntime>();
        private ProjectileInfluence pendingInfluence = ProjectileInfluence.None;

        private Character owner;
        private Collider2D[] ownerColliders;
        private readonly List<Collider2D> ignoredColliders = new List<Collider2D>();
        private Vector2 lastVelocity;
        private float lifeTimer;
        private float ignoreTimer;
        private bool despawning;

        internal ProjectilePool Pool { get; set; }
        internal Projectile SourcePrefab { get; set; }

        public ProjectileConfig Config => config;
        public ProjectileStats Stats => stats;
        public IReadOnlyList<ProjectileTrait> Traits => traits;
        public IReadOnlyList<BurstDefinition> Bursts => burstDefinitions;
        public IReadOnlyList<BurstRuntime> BurstRuntimes => burstRuntimes;
        public Character Owner => owner;
        public Team Team => owner != null ? owner.Team : NineDays.Team.None;
        public bool IsLaunched { get; private set; }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (stats == null) stats = ProjectileStats.From(config);
            burstDefinitions = CopyBursts(config != null ? config.bursts : null);
            BuildBurstRuntimes();
            ApplyStats();
        }

        /// <summary>运行时注入配置（对象池 / 测试用）。</summary>
        public void Configure(ProjectileConfig projectileConfig)
        {
            config = projectileConfig;

            var newTraits = new List<ProjectileTrait>();
            if (projectileConfig != null && projectileConfig.traits != null)
            {
                for (int i = 0; i < projectileConfig.traits.Count; i++)
                    if (projectileConfig.traits[i] != null) newTraits.Add(projectileConfig.traits[i]);
            }

            Configure(ProjectileStats.From(projectileConfig), newTraits,
                      CopyBursts(projectileConfig != null ? projectileConfig.bursts : null));
        }

        /// <summary>注入最终数值与特性（旧接口）；爆发从当前 Config 读取。</summary>
        public void Configure(ProjectileStats projectileStats, IReadOnlyList<ProjectileTrait> projectileTraits)
        {
            Configure(projectileStats, projectileTraits, config != null ? config.bursts : null);
        }

        /// <summary>注入最终数值、特性与爆发（发射构建管线使用）。</summary>
        public void Configure(ProjectileStats projectileStats, IReadOnlyList<ProjectileTrait> projectileTraits,
                              IReadOnlyList<BurstDefinition> projectileBursts)
        {
            stats = projectileStats ?? ProjectileStats.From(config);
            stats.Validate();
            stats.pierceCount = Mathf.Max(0, stats.pierceCount);
            if (stats.pierceRemaining <= 0 && stats.pierceCount > 0) stats.pierceRemaining = stats.pierceCount;
            stats.pierceRemaining = Mathf.Min(stats.pierceRemaining, stats.pierceCount);

            traits = new List<ProjectileTrait>();
            if (projectileTraits != null)
            {
                for (int i = 0; i < projectileTraits.Count; i++)
                    if (projectileTraits[i] != null) traits.Add(projectileTraits[i]);
            }

            burstDefinitions = CopyBursts(projectileBursts);
            BuildBurstRuntimes();
            ApplyStats();
        }

        private static List<BurstDefinition> CopyBursts(IReadOnlyList<BurstDefinition> source)
        {
            var result = new List<BurstDefinition>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
                if (source[i] != null) result.Add(source[i]);

            return result;
        }

        private void BuildBurstRuntimes()
        {
            burstRuntimes.Clear();
            if (burstDefinitions == null) return;

            for (int i = 0; i < burstDefinitions.Count; i++)
                burstRuntimes.Add(new BurstRuntime(burstDefinitions[i]));
        }

        /// <summary>把运行时数值同步到刚体与碰撞体。</summary>
        private void ApplyStats()
        {
            if (stats == null) return;

            if (body != null)
            {
                body.mass = Mathf.Max(0.01f, stats.mass);
                body.gravityScale = stats.gravityScale;
                body.linearDamping = stats.linearDamping;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (bodyCollider is CircleCollider2D circle) circle.radius = stats.radius;
        }

        /// <summary>由发射者调用：给定初速与所属角色。</summary>
        public void Launch(Vector2 velocity, Character shooter)
        {
            owner = shooter;
            IsLaunched = true;
            despawning = false;
            lifeTimer = 0f;
            pendingInfluence.Reset();

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            if (body != null) body.linearVelocity = velocity;
            lastVelocity = velocity;

            IgnoreOwnerTemporarily();

            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                    if (traits[i] != null) traits[i].OnLaunch(this);
            }

            for (int i = 0; i < burstRuntimes.Count; i++)
                burstRuntimes[i].ResetForLaunch();

            TriggerBursts(BurstTriggerType.OnLaunch,
                          body != null ? body.position : (Vector2)transform.position,
                          Vector2.zero, default, false);
        }

        private void IgnoreOwnerTemporarily()
        {
            if (owner == null || bodyCollider == null) return;

            ignoreTimer = stats != null ? stats.ignoreOwnerTime : 0.15f;
            ownerColliders = owner.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null && ownerColliders[i] != bodyCollider)
                    IgnorePair(ownerColliders[i]);
            }
        }

        private void RestoreOwnerCollision()
        {
            if (ownerColliders == null) return;

            for (int i = 0; i < ownerColliders.Length; i++)
                if (ownerColliders[i] != null) RestorePair(ownerColliders[i]);

            ownerColliders = null;
        }

        /// <summary>记录并忽略一对碰撞，池化回收时统一恢复。</summary>
        private void IgnorePair(Collider2D other)
        {
            if (bodyCollider == null || other == null) return;

            Physics2D.IgnoreCollision(bodyCollider, other, true);
            if (!ignoredColliders.Contains(other)) ignoredColliders.Add(other);
        }

        private void RestorePair(Collider2D other)
        {
            if (bodyCollider != null && other != null)
                Physics2D.IgnoreCollision(bodyCollider, other, false);

            ignoredColliders.Remove(other);
        }

        /// <summary>恢复所有忽略关系，避免下一发复用池对象时仍然穿透。</summary>
        private void RestoreAllIgnoredCollisions()
        {
            for (int i = 0; i < ignoredColliders.Count; i++)
            {
                Collider2D other = ignoredColliders[i];
                if (bodyCollider != null && other != null)
                    Physics2D.IgnoreCollision(bodyCollider, other, false);
            }

            ignoredColliders.Clear();
            ownerColliders = null;
        }

        private void Update()
        {
            if (!IsLaunched) return;

            float dt = Time.deltaTime;
            lifeTimer += dt;

            if (ignoreTimer > 0f)
            {
                ignoreTimer -= dt;
                if (ignoreTimer <= 0f) RestoreOwnerCollision();
            }

            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                    if (traits[i] != null) traits[i].OnTick(this, dt);
            }

            TickBursts(dt);
            if (!IsLaunched) return;

            if (stats != null && lifeTimer >= stats.lifeTime) Despawn(ExpireReason.Lifetime);
        }

        /// <summary>飞行影响在物理帧统一结算；技能 / 道具只需往这里累加。</summary>
        private void FixedUpdate()
        {
            if (!IsLaunched || body == null) return;

            body.linearVelocity = ProjectileInfluenceSolver.Integrate(
                body.linearVelocity, pendingInfluence, Time.fixedDeltaTime);
            pendingInfluence.Reset();
            lastVelocity = body.linearVelocity;
        }

        public void AddInfluence(in ProjectileInfluence influence)
        {
            pendingInfluence.Add(influence);
        }

        public void AddAcceleration(Vector2 acceleration)
        {
            pendingInfluence.Acceleration += acceleration;
        }

        public void ScaleVelocity(float scale)
        {
            pendingInfluence.VelocityScale *= Mathf.Max(0f, scale);
        }

        public void OverrideVelocity(Vector2 velocity)
        {
            pendingInfluence.VelocityOverride = velocity;
        }

        /// <summary>进入 Trigger：转发给特性（道具场等非实体碰撞不会走 OnCollisionEnter2D）。</summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsLaunched || despawning || other == null) return;
            if (traits == null) return;

            for (int i = 0; i < traits.Count; i++)
                if (traits[i] != null) traits[i].OnTrigger(this, other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsLaunched || despawning || collision.collider == null) return;

            Character target = collision.collider.GetComponentInParent<Character>();
            if (target != null && target == owner) return;
            if (target != null && owner != null && target.Team == owner.Team && (stats == null || !stats.friendlyFire)) return;

            ProjectileHit hit = BuildProjectileHit(collision, target);
            HitDisposition disposition = ProjectileHitResolver.Resolve(
                stats == null || stats.destroyOnHit, traits, this, hit);

            if (disposition == HitDisposition.Ignore) return;

            // 穿透：忽略这个目标并恢复碰撞前的速度，避免被碰撞反弹带偏
            if (disposition == HitDisposition.Pass && target != null)
            {
                IgnorePair(collision.collider);
                if (body != null) body.linearVelocity = lastVelocity;
            }

            if (target != null)
            {
                HitInfo info = BuildHitInfo(hit);
                target.ReceiveHit(info);
                GameEvents.RaiseProjectileHitLanded(target, this, info);
            }

            bool hitTerrain = ArenaController.Instance != null &&
                              ArenaController.Instance.IsTerrainCollider(collision.collider);

            // 撞击特效：任何子弹撞到任何东西都生成（爆炸特效由 BurstDefinition.viewPrefab 负责）
            if (config != null && config.impactViewPrefab != null)
                BurstViewSpawner.Spawn(config.impactViewPrefab, hit.Point);

            TriggerBursts(BurstTriggerType.OnImpact, hit.Point, hit.Normal, hit, hitTerrain);

            if (disposition == HitDisposition.Consume) Despawn(ExpireReason.Hit);
        }

        /// <summary>推进 OnTimer 爆发；效果可能回收子弹。</summary>
        private void TickBursts(float deltaTime)
        {
            for (int i = 0; i < burstRuntimes.Count; i++)
            {
                burstRuntimes[i].Tick(deltaTime, this);
                if (!IsLaunched) return;
            }
        }

        /// <summary>按触发类型执行爆发；返回成功触发的数量。</summary>
        private int TriggerBursts(BurstTriggerType trigger, Vector2 point, Vector2 normal,
                                  ProjectileHit hit, bool hitTerrain)
        {
            int count = 0;
            for (int i = 0; i < burstRuntimes.Count; i++)
            {
                if (burstRuntimes[i].TryTrigger(this, trigger, point, normal, hit, hitTerrain))
                    count++;
                if (trigger != BurstTriggerType.OnExpire && !IsLaunched) return count;
            }
            return count;
        }

        /// <summary>外部手动触发 Manual 爆发；返回成功触发的数量。</summary>
        public int TriggerManualBursts()
        {
            if (!IsLaunched) return 0;

            Vector2 point = body != null ? body.position : (Vector2)transform.position;
            int count = 0;
            for (int i = 0; i < burstRuntimes.Count; i++)
            {
                if (burstRuntimes[i].TryManual(this)) count++;
            }
            return count;
        }

        private ProjectileHit BuildProjectileHit(Collision2D collision, Character target)
        {
            Vector2 point = body != null ? body.position : (Vector2)transform.position;
            Vector2 normal = Vector2.zero;

            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            return new ProjectileHit(
                collision.collider,
                target,
                point,
                normal,
                CharacterCombat.SumImpulse(collision),
                collision.relativeVelocity.magnitude);
        }

        private HitInfo BuildHitInfo(in ProjectileHit hit)
        {
            return new HitInfo(
                gameObject,
                owner != null ? owner.gameObject : null,
                Team,
                HitKind.Projectile,
                hit.Point,
                hit.Normal,
                hit.Impulse,
                hit.Speed,
                stats != null ? stats.damage : 0f);
        }

        /// <summary>从池中取出后调用：解除“已回收”保护。</summary>
        internal void MarkRented()
        {
            despawning = false;
            IsLaunched = false;
        }

        /// <summary>归还对象池前清空所有每发状态；由 ProjectilePool 调用。</summary>
        internal void ResetForPool()
        {
            RestoreAllIgnoredCollisions();

            owner = null;
            ownerColliders = null;
            stats = null;
            traits = null;
            burstDefinitions = null;
            burstRuntimes.Clear();

            pendingInfluence.Reset();
            lifeTimer = 0f;
            ignoreTimer = 0f;
            lastVelocity = Vector2.zero;

            IsLaunched = false;
            despawning = true;
            SourcePrefab = null;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.Sleep();
            }

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }

        /// <summary>唯一销毁出口：特性回调 → 事件 → 入池（池不存在时销毁）。</summary>
        public void Despawn(ExpireReason reason)
        {
            if (despawning) return;

            despawning = true;
            IsLaunched = false;

            Vector2 point = body != null ? body.position : (Vector2)transform.position;
            TriggerBursts(BurstTriggerType.OnExpire, point, Vector2.zero, default, false);

            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                    if (traits[i] != null) traits[i].OnExpire(this, reason);
            }

            GameEvents.RaiseProjectileExpired(this, reason);

            if (Pool != null && Pool.Return(this)) return;

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
