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
