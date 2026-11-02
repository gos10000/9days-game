using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 角色战斗表现：受击（事件 + 闪白 + 击退）与撞障碍冲量上报。
    /// 与 Character 挂在同一个根节点。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterCombat : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Character character;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CharacterHealth health;

        private Collider2D lastImpactCollider;
        private float lastImpactTime = -999f;

        private CharacterConfig Config => character != null ? character.Config : null;

        /// <summary>血量组件（惰性解析）。</summary>
        private CharacterHealth Health
        {
            get
            {
                if (health == null) health = GetComponent<CharacterHealth>();
                return health;
            }
        }

        private void Awake()
        {
            if (character == null) character = GetComponent<Character>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (health == null) health = GetComponent<CharacterHealth>();
        }

        /// <summary>受击入口：先报「被什么击中」，再按冲量决定是否击飞。</summary>
        public void ReceiveHit(in HitInfo info)
        {
            if (character == null) return;

            GameEvents.RaiseCharacterHit(character, info);
            character.Flash();

            CharacterHealth targetHealth = Health;
            if (targetHealth != null) targetHealth.TakeDamage(info.Damage);
            if (character.IsDefeated) return;

            float threshold = Config != null ? Config.hitThreshold : 0.5f;
            if (info.Impulse < threshold) return;

            ApplyKnockback(info);
            character.EnterAirborne();
            GameEvents.RaiseCharacterKnockedBack(character, info);
        }

        private void ApplyKnockback(in HitInfo info)
        {
            CharacterConfig cfg = Config;
            Vector2 self = body != null ? body.position : (Vector2)transform.position;
            Vector2 direction = self - info.Point;

            if (direction.sqrMagnitude < 0.0001f)
                direction = info.Normal.sqrMagnitude > 0.0001f ? -info.Normal : Vector2.up;

            direction.Normalize();

            float scale = cfg != null ? cfg.knockbackScale : 1f;
            float min = cfg != null ? cfg.minKnockback : 2f;
            float max = cfg != null ? cfg.maxKnockback : 20f;
            float impulse = Mathf.Clamp(info.Impulse * scale + min, 0f, max);

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.AddForce(direction * impulse, ForceMode2D.Impulse);
            }

            if (cfg != null) character.LockControl(cfg.controlLockTime);
        }

        private void OnCollisionEnter2D(Collision2D collision) => ReportImpact(collision);

        /// <summary>撞击障碍：算冲量 → 过阈值上报 StrongImpact（带撞击力度）。</summary>
        private void ReportImpact(Collision2D collision)
        {
            CharacterConfig cfg = Config;
            if (character == null || cfg == null || collision.collider == null) return;

            int otherLayer = collision.collider.gameObject.layer;
            if ((cfg.obstacleMask.value & (1 << otherLayer)) == 0) return;

            float impulse = SumImpulse(collision);
            float speed = collision.relativeVelocity.magnitude;
            Vector2 point = body != null ? body.position : (Vector2)transform.position;
            Vector2 normal = Vector2.zero;

            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            bool above = impulse >= cfg.impactThreshold;
            float force = impulse / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            var info = new ImpactInfo(collision.collider, ImpactKind.Obstacle, point, normal,
                                      impulse, speed, force, above);

            GameEvents.RaiseImpactDetected(character, info);
            if (!above) return;

            // 同一碰撞体去重，避免一次碰撞在连续帧里重复上报
            if (collision.collider == lastImpactCollider && Time.time - lastImpactTime < cfg.impactCooldown) return;
            lastImpactCollider = collision.collider;
            lastImpactTime = Time.time;

            GameEvents.RaiseStrongImpact(character, info);
        }

        /// <summary>累加接触点冲量（N·s）。Collision2D 本身没有 impulse 属性。</summary>
        public static float SumImpulse(Collision2D collision)
        {
            if (collision == null) return 0f;

            float sum = 0f;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                sum += Mathf.Abs(contact.normalImpulse) + Mathf.Abs(contact.tangentImpulse);
            }
            return sum;
        }
    }
}
