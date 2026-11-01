using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹原型脚本（文档「不同子弹复用原型脚本」）。
    /// 差异全部由 ProjectileConfig 提供：质量、半径、初速、伤害、是否打自己人。
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

        private Character owner;
        private Collider2D[] ownerColliders;
        private float lifeTimer;
        private float ignoreTimer;

        public ProjectileConfig Config => config;
        public Character Owner => owner;
        public Team Team => owner != null ? owner.Team : NineDays.Team.None;
        public bool IsLaunched { get; private set; }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            ApplyConfig();
        }

        private void ApplyConfig()
        {
            if (config == null) return;

            if (body != null)
            {
                body.mass = Mathf.Max(0.01f, config.mass);
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (bodyCollider is CircleCollider2D circle) circle.radius = config.radius;
        }

        /// <summary>由发射者调用：给定初速与所属角色。</summary>
        public void Launch(Vector2 velocity, Character shooter)
        {
            owner = shooter;
            IsLaunched = true;
            lifeTimer = 0f;

            if (trail != null) trail.Clear();
            if (body != null) body.linearVelocity = velocity;

            IgnoreOwnerTemporarily();
        }

        private void IgnoreOwnerTemporarily()
        {
            if (owner == null || bodyCollider == null) return;

            ignoreTimer = config != null ? config.ignoreOwnerTime : 0.15f;
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

            if (config != null && lifeTimer >= config.lifeTime) Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsLaunched || collision.collider == null) return;

            Character target = collision.collider.GetComponentInParent<Character>();
            if (target == null)
            {
                if (config == null || config.destroyOnHit) Destroy(gameObject);
                return;
            }

            if (target == owner) return;
            if (owner != null && target.Team == owner.Team && (config == null || !config.friendlyFire)) return;

            target.ReceiveHit(BuildHitInfo(collision));
            if (config == null || config.destroyOnHit) Destroy(gameObject);
        }

        private HitInfo BuildHitInfo(Collision2D collision)
        {
            Vector2 point = body != null ? body.position : (Vector2)transform.position;
            Vector2 normal = Vector2.zero;

            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            return new HitInfo(
                gameObject,
                owner != null ? owner.gameObject : null,
                Team,
                HitKind.Projectile,
                point,
                normal,
                CharacterCombat.SumImpulse(collision),
                collision.relativeVelocity.magnitude,
                config != null ? config.damage : 0f);
        }
    }
}
