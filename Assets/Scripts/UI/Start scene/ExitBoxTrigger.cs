using UnityEngine;

namespace NineDays
{
    /// <summary>退出盒：子弹进入后通知 StartScene 更新页面并退出游戏（编辑器内停止 Play）。</summary>
    public class ExitBoxTrigger : MonoBehaviour
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

            if (StartScene.Instance != null) StartScene.Instance.RequestExit();
            else StartScene.QuitApplication();
        }
    }
}
