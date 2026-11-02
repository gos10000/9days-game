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
        private ProjectileInfluence pendingInfluence = ProjectileInfluence.None;

        private Character owner;
        private Collider2D[] ownerColliders;
        private Vector2 lastVelocity;
        private float lifeTimer;
        private float ignoreTimer;
        private bool despawning;

        public ProjectileConfig Config => config;
        public ProjectileStats Stats => stats;
        public IReadOnlyList<ProjectileTrait> Traits => traits;
        public Character Owner => owner;
        public Team Team => owner != null ? owner.Team : NineDays.Team.None;
        public bool IsLaunched { get; private set; }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (stats == null) stats = ProjectileStats.From(config);
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

            Configure(ProjectileStats.From(projectileConfig), newTraits);
        }

        /// <summary>注入最终数值与特性（发射构建管线使用）。</summary>
        public void Configure(ProjectileStats projectileStats, IReadOnlyList<ProjectileTrait> projectileTraits)
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

            ApplyStats();
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

            if (trail != null) trail.Clear();
            if (body != null) body.linearVelocity = velocity;
            lastVelocity = velocity;

            IgnoreOwnerTemporarily();

            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                    if (traits[i] != null) traits[i].OnLaunch(this);
            }
        }

        private void IgnoreOwnerTemporarily()
        {
            if (owner == null || bodyCollider == null) return;

            ignoreTimer = stats != null ? stats.ignoreOwnerTime : 0.15f;
            ownerColliders = owner.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null && ownerColliders[i] != bodyCollider)
                    Physics2D.IgnoreCollision(bodyCollider, ownerColliders[i], true);
            }
        }

        private void RestoreOwnerCollision()
        {
            if (ownerColliders == null) return;

            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null && ownerColliders[i] != bodyCollider)
                    Physics2D.IgnoreCollision(bodyCollider, ownerColliders[i], false);
            }
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
                if (bodyCollider != null && collision.collider != null)
                    Physics2D.IgnoreCollision(bodyCollider, collision.collider, true);
                if (body != null) body.linearVelocity = lastVelocity;
            }

            if (target != null)
            {
                HitInfo info = BuildHitInfo(hit);
                target.ReceiveHit(info);
                GameEvents.RaiseProjectileHitLanded(target, this, info);
            }

            if (disposition == HitDisposition.Consume) Despawn(ExpireReason.Hit);
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

        /// <summary>唯一销毁出口：特性回调 → 事件 → 销毁（对象池以后从这里接管）。</summary>
        public void Despawn(ExpireReason reason)
        {
            if (despawning) return;

            despawning = true;
            IsLaunched = false;

            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                    if (traits[i] != null) traits[i].OnExpire(this, reason);
            }

            GameEvents.RaiseProjectileExpired(this, reason);

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
