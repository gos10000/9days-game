using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 可破坏障碍物：被任何子弹碰到就碎。
    ///
    /// 玩家侧：挂在道具预制体上，进 PropTable 就能被抽取、放置（PlankObstacle 就是这种用法）。
    /// 制作者侧：直接把它挂在场景里任意带 Collider2D 的物件上，或者用菜单
    /// 9Days/道具/放置木板障碍 把预制体摆进战斗场景，重开一局不会自动恢复（需要重摆或自己加恢复逻辑）。
    ///
    /// 判定用物理碰撞：子弹是 Projectile 层的非 Trigger 碰撞体，所以子弹撞过来会走到 OnCollisionEnter2D。
    /// 角色、地面走过去不会触发，因为对方身上没有 Projectile。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestructibleProp : MonoBehaviour
    {
        [Header("规则")]
        [Tooltip("任何子弹撞到就破坏")]
        [SerializeField] private bool breakOnProjectileHit = true;

        [Tooltip("算作「子弹」的层。默认 Projectile(9)。这一层上的任何碰撞体都会破坏它，\n" +
                 "这样以后新加的子弹/抛物物件不用改代码也生效")]
        [SerializeField] private LayerMask bulletLayers = 1 << 9;

        [Tooltip("破坏时广播 GameEvents.PropCleared（会让清场音效响一次）")]
        [SerializeField] private bool raisePropCleared = true;

        [Header("表现")]
        [Tooltip("破坏特效预制体；留空则只有销毁")]
        [SerializeField] private GameObject breakEffectPrefab;

        [Tooltip("破坏后延迟多少秒才销毁；0 = 立刻（碰撞体同时消失，子弹不会被卡住）")]
        [Min(0f)]
        [SerializeField] private float destroyDelay;

        private bool broken;

        /// <summary>是否已经碎过；避免同帧多颗子弹重复触发。</summary>
        public bool Broken => broken;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!breakOnProjectileHit || broken) return;
            if (collision.collider == null) return;
            if (!IsBullet(collision.collider)) return;

            Break();
        }

        /// <summary>兜底：碰到 Trigger 型的子弹也能碎。</summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!breakOnProjectileHit || broken) return;
            if (!IsBullet(other)) return;

            Break();
        }

        /// <summary>
        /// 判定是不是子弹。两条都认：
        /// 1) 碰撞体自己或任一父级挂 Projectile（覆盖所有现有弹种、含穿透/引导弹）
        /// 2) 碰撞体自己或任一父级位于 bulletLayers（默认 Projectile 层），
        ///    以后新做的子弹即使不继承 Projectile 也能打碎它
        /// </summary>
        private bool IsBullet(Collider2D other)
        {
            if (other == null) return false;

            int mask = bulletLayers.value;
            Transform current = other.transform;

            for (int depth = 0; current != null && depth < 4; depth++, current = current.parent)
            {
                if (current.GetComponent<Projectile>() != null) return true;
                if ((mask & (1 << current.gameObject.layer)) != 0) return true;
            }

            return false;
        }

        /// <summary>供爆炸、道具效果或调试直接调用。</summary>
        public void Break()
        {
            if (broken) return;
            broken = true;

            if (breakEffectPrefab != null)
                Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);

            if (raisePropCleared) GameEvents.RaisePropCleared(gameObject);

            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
    }
}
