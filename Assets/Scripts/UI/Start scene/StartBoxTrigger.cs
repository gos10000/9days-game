using UnityEngine;

namespace NineDays
{
    /// <summary>开始盒：子弹进入后通知 StartScene 更新页面并进入战斗场景。</summary>
    public class StartBoxTrigger : MonoBehaviour
    {
        private static int ProjectileLayer;

        void Awake()
        {
            ProjectileLayer = LayerMask.NameToLayer("Projectile");
        }

        private bool triggered;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (triggered || collision == null) return;
            if (ProjectileLayer >= 0 && collision.gameObject.layer != ProjectileLayer) return;

            triggered = true;

            if (StartScene.Instance != null) StartScene.Instance.RequestStart();
            else Debug.LogWarning("[StartBox] 场景里没有 StartScene，无法进入战斗场景");
        }
    }
}
