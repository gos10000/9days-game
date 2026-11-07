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

        protected virtual void OnEnable()
        {
            StartAmbience();
        }

        protected virtual void OnDisable()
        {
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
