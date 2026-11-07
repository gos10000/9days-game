using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 清除道具：子弹进入道具「放置时禁止其它道具压上来的范围」时，把该道具销毁
    /// （或只禁用它的 ProjectileField，由 destroyWholeObject 决定）。
    ///
    /// 判定半径用 <see cref="ProjectileField.BlockRadius"/>，也就是放置阶段画出来的红色虚线圈，
    /// **不是**道具的影响范围。黑洞/白洞的引力圈比这个圈大得多，
    /// 用影响范围会出现「子弹还在圈外很远，洞就被清掉了」。
    ///
    /// 用 OnTick 按距离判，而不是 OnTrigger：黑洞/白洞的碰撞体是 EdgeCollider2D（空心圈），
    /// 子弹穿过圈之后就不再与它重叠，OnTriggerStay 收不到，会出现「贴脸飞过却不消除」。
    ///
    /// 场的 BlockRadius 配成 0（不阻挡）时退回旧的「碰到就清」。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/特性/清除道具", fileName = "PropRemoverTrait")]
    public sealed class PropRemoverTrait : ProjectileTrait
    {
        [Tooltip("销毁整个道具物体；关闭时只禁用它的 ProjectileField 组件")]
        [SerializeField] private bool destroyWholeObject = true;

        [Tooltip("在 Console 打印清除了哪个道具")]
        [SerializeField] private bool logRemoval;

        public override void OnTick(Projectile projectile, float deltaTime)
        {
            if (projectile == null) return;

            IReadOnlyList<ProjectileField> fields = ProjectileField.Active;
            if (fields.Count == 0) return;

            Vector2 point = projectile.transform.position;

            // 倒序：清除会让场立刻从列表里退出
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                ProjectileField field = fields[i];
                if (field == null || !field.enabled) continue;

                float radius = field.BlockRadius;
                if (radius <= 0f) continue;     // 没配半径的场仍走「碰到就清」

                Vector2 fieldPoint = field.transform.position;
                if ((point - fieldPoint).sqrMagnitude > radius * radius) continue;

                Remove(field);
            }
        }

        public override void OnTrigger(Projectile projectile, Collider2D other)
        {
            if (projectile == null || other == null) return;

            ProjectileField field = other.GetComponentInParent<ProjectileField>();
            if (field == null || !field.enabled) return;

            // 配了不可放置半径的场，接触不算数，交给 OnTick 按半径判定
            if (field.BlockRadius > 0f) return;

            Remove(field);
        }

        private void Remove(ProjectileField field)
        {
            if (field == null || !field.enabled) return;

            if (logRemoval)
                Debug.Log($"[PropRemover] 清除道具 {field.name}（半径 {field.BlockRadius:0.##}）", field);

            GameEvents.RaisePropCleared(field.gameObject);

            if (destroyWholeObject) Object.Destroy(field.gameObject);
            else field.enabled = false;
        }
    }
}
