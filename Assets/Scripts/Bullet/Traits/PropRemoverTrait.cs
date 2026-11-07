using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 清除道具：子弹经过任何 ProjectileField（反重力场、黑洞、白洞等）时把该道具销毁。
    /// 子弹本身继续飞行，不受影响。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/特性/清除道具", fileName = "PropRemoverTrait")]
    public sealed class PropRemoverTrait : ProjectileTrait
    {
        [Tooltip("销毁整个道具物体；关闭时只禁用它的 ProjectileField 组件")]
        [SerializeField] private bool destroyWholeObject = true;

        [Tooltip("在 Console 打印清除了哪个道具")]
        [SerializeField] private bool logRemoval;

        public override void OnTrigger(Projectile projectile, Collider2D other)
        {
            if (other == null) return;

            ProjectileField field = other.GetComponentInParent<ProjectileField>();
            if (field == null) return;

            if (logRemoval)
                Debug.Log($"[PropRemover] 清除道具 {field.name}", field);

            if (destroyWholeObject) Object.Destroy(field.gameObject);
            else field.enabled = false;
        }
    }
}
