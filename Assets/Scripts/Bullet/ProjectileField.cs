using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 区域场基类：Trigger 内的子弹每物理帧累加一次影响。
    /// 技能 / 道具只需要写子类，不用改 Projectile 核心。
    /// </summary>
    public abstract class ProjectileField : MonoBehaviour
    {
        [Header("过滤")]
        [Tooltip("只影响这些层的子弹")]
        [SerializeField] private LayerMask targetLayers = ~0;

        [Tooltip("是否影响 Player1 的子弹")]
        [SerializeField] private bool affectPlayer1 = true;

        [Tooltip("是否影响 Player2 的子弹")]
        [SerializeField] private bool affectPlayer2 = true;

        [Header("放置")]
        [Tooltip("不可放置半径：以道具中心为原点，圆内不能再放道具。\n-1 = 自动取碰撞体世界半径；0 = 不阻挡（可以叠放）；> 0 = 手动指定")]
        [SerializeField] private float placementBlockRadius = -1f;

        /// <summary>
        /// 不可放置半径（世界单位）。-1 回退到碰撞体世界半径，0 表示不阻挡。
        /// 红圈画多大、判定就有多大，用的是同一个数。
        /// </summary>
        public float BlockRadius
        {
            get
            {
                if (placementBlockRadius >= 0f) return placementBlockRadius;

                Collider2D fieldCollider = GetComponent<Collider2D>();
                if (fieldCollider == null) return 0f;

                Bounds bounds = fieldCollider.bounds;
                return Mathf.Max(bounds.extents.x, bounds.extents.y);
            }
        }

        /// <summary>子类只描述“这颗子弹在这里受到什么影响”。</summary>
        public abstract void Accumulate(Projectile projectile, ref ProjectileInfluence influence, float deltaTime);

        [Header("音频")]
        [Tooltip("场存在期间循环播放的环境音；留空则完全静音")]
        [SerializeField] private AudioClip ambienceClip;

        [Tooltip("环境音音量")]
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.6f;

        [Tooltip("0 = 2D 全局音，1 = 完全 3D 定位")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.85f;

        [Tooltip("音高随机范围，避免同类场叠在一起听感发闷")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.06f);

        private AudioSource ambienceSource;

        private static readonly List<ProjectileField> active = new List<ProjectileField>();

        /// <summary>
        /// 当前场上所有启用中的道具场。
        /// 供需要「按半径判定」的效果遍历（例如消除弹按不可放置半径清场）——
        /// 黑洞用的是 EdgeCollider2D（空心圈），子弹穿过圈之后就不再与它重叠，
        /// 靠 OnTriggerStay 是收不到的，只能自己按距离算。
        /// </summary>
        public static IReadOnlyList<ProjectileField> Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive()
        {
            active.Clear();
        }

        protected virtual void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);

            EnsureTriggerCollider();
            StartAmbience();
        }

        /// <summary>
        /// 场的碰撞体必须是 Trigger：
        /// 实体碰撞会让子弹走 OnCollisionEnter2D → 直接引爆，同时 OnTriggerStay2D 也不会触发，
        /// 结果是「道具变成一堵会炸子弹的墙」。这里兜底，防止以后新道具再踩一次。
        /// </summary>
        private void EnsureTriggerCollider()
        {
            Collider2D fieldCollider = GetComponent<Collider2D>();
            if (fieldCollider == null || fieldCollider.isTrigger) return;

            fieldCollider.isTrigger = true;
            Debug.LogWarning(
                $"[ProjectileField] {name} 的碰撞体不是 Trigger，已强制改为 Trigger。" +
                "实体碰撞会让子弹撞上就爆，且场效果不会生效。", this);
        }

        protected virtual void OnDisable()
        {
            active.Remove(this);
            if (ambienceSource != null) ambienceSource.Stop();
        }

        /// <summary>没有配置音频就什么都不做，不影响原有逻辑。</summary>
        private void StartAmbience()
        {
            if (ambienceClip == null) return;

            if (ambienceSource == null)
            {
                ambienceSource = GetComponent<AudioSource>();
                if (ambienceSource == null) ambienceSource = gameObject.AddComponent<AudioSource>();
            }

            ambienceSource.clip = ambienceClip;
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
            ambienceSource.volume = Mathf.Clamp01(ambienceVolume);
            ambienceSource.spatialBlend = Mathf.Clamp01(spatialBlend);
            ambienceSource.rolloffMode = AudioRolloffMode.Linear;
            ambienceSource.minDistance = 2f;
            ambienceSource.maxDistance = 30f;

            float minPitch = Mathf.Min(pitchRange.x, pitchRange.y);
            float maxPitch = Mathf.Max(pitchRange.x, pitchRange.y);
            ambienceSource.pitch = Random.Range(minPitch, maxPitch);

            ambienceSource.Play();
        }

        protected virtual void Reset()
        {
            Collider2D fieldCollider = GetComponent<Collider2D>();
            if (fieldCollider != null) fieldCollider.isTrigger = true;
        }

        protected virtual void OnTriggerStay2D(Collider2D other)
        {
            if (other == null) return;
            if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

            Projectile projectile = other.GetComponentInParent<Projectile>();
            if (projectile == null || !projectile.IsLaunched) return;
            if (!MatchesTeam(projectile.Team)) return;

            // 免疫道具场的子弹（中和重力弹）直接跳过，黑洞 / 白洞 / 重力场都拉不动它
            if (projectile.Stats != null && projectile.Stats.ignoreFields) return;

            ProjectileInfluence influence = ProjectileInfluence.None;
            Accumulate(projectile, ref influence, Time.fixedDeltaTime);
            projectile.AddInfluence(influence);
        }

        private bool MatchesTeam(Team team)
        {
            if (team == Team.Player1) return affectPlayer1;
            if (team == Team.Player2) return affectPlayer2;
            return true;
        }
    }
}
