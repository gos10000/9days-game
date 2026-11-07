using UnityEngine;

/// <summary>
/// 进入触发区就切场景（开始界面用：把石头丢进盒子 → 进主场景）。
/// 注意：Unity 2D 的 OnTriggerEnter2D 需要至少一方有 Rigidbody2D，
/// 所以触发它的东西必须是带刚体的物体（子弹/石头有，纯静态物没有）。
/// </summary>
public class StartBoxTrigger : MonoBehaviour
{
    /// <summary>开始盒：子弹进入后通知 StartScene 更新页面并进入战斗场景。</summary>
    public class StartBoxTrigger : MonoBehaviour
    {
        private static int ProjectileLayer;

        void Awake()
        {
            ProjectileLayer = LayerMask.NameToLayer("Projectile");
        }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isLoading) return;

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError($"[StartBoxTrigger] {name} 没有填 targetScene", this);
            isLoading = true;   // 这种情况再触发也没用，别每帧刷日志
            return;
        }

        isLoading = true;

        try
        {
            SceneManager.LoadScene(targetScene);
        }
        catch (System.Exception e)
        {
            // 失败时把状态放回去，否则这个触发器会永久失灵（旧版的坑）
            isLoading = false;
            Debug.LogError($"[StartBoxTrigger] 加载场景 '{targetScene}' 失败：{e.Message}", this);
        }
    }
}
