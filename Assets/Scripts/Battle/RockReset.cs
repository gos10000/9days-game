using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 开始界面专用：按 R 把这一枪作废——收回场上的石头，把发射权还给角色。
    /// 只复位「石头在哪」和「能不能再投」，不移动角色、不推进回合。
    /// 挂在 Rock 预制体上，这颗石头只在开始界面使用。
    /// </summary>
    [RequireComponent(typeof(Projectile))]
    public sealed class RockReset : MonoBehaviour
    {
        [Header("重置")]
        [Tooltip("重开键由输入层统一映射：GameInputAction.Restart → R 键（见 UnityInputSource）")]
        [SerializeField] private bool enableKeyboardReset = true;

        private Projectile projectile;

        private void Awake()
        {
            projectile = GetComponent<Projectile>();
        }

        private void Update()
        {
            if (!enableKeyboardReset) return;
            if (GameInput.WasPressed(GameInputAction.Restart)) ResetNow();
        }

        /// <summary>也可以给 UI 按钮或调试热键调用。</summary>
        public void ResetNow()
        {
            Character owner = projectile != null ? projectile.Owner : null;

            // 1) 收回场上所有石头。子弹是池化的，Despawn 等于“位置复位”——
            //    下一次拖拽发射时，ShotSpawner 会从角色身上重新生成一颗。
            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
                if (projectiles[i] != null) projectiles[i].Despawn(ExpireReason.Aborted);

            // 2) 取消这一枪的结算：IsResolving 归零、发射权还给当前队，回合和角色位置都不动
            TurnController turn = FindFirstObjectByType<TurnController>();
            if (turn != null)
            {
                turn.CancelShot();
                return;
            }

            // 兜底：场景里没有回合控制器时，自己把发射权还回去
            if (owner == null) return;
            owner.SetCanAct(true);
            owner.CancelAim();
        }
    }
}
