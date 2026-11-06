using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 暂停菜单：ESC 开关，用 Time.timeScale 冻结游戏。
    /// 三个选项分别是回到游戏 / 重新开始 / 回到开始界面。
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        [Header("目标")]
        [Tooltip("留空则自动查找场景里的 BattleController")]
        [SerializeField] private BattleController battleController;

        [Tooltip("「回到开始界面」要加载的场景名，需在 Build Settings 里")]
        [SerializeField] private string startSceneName = "StartScene";

        [Header("输入")]
        [Tooltip("允许用 ESC 开关暂停")]
        [SerializeField] private bool toggleWithEscape = true;

        /// <summary>暂停中：其它输入入口（瞄准/发射）应据此屏蔽。</summary>
        public static bool IsPaused => GamePause.IsPaused;

        private float cachedTimeScale = 1f;

        private void Awake()
        {
            GamePause.IsPaused = false;

            if (battleController == null) battleController = FindAnyObjectByType<BattleController>();

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(RestartBattle);
            if (quitButton != null) quitButton.onClick.AddListener(ReturnToStartScene);

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartBattle);
            if (quitButton != null) quitButton.onClick.RemoveListener(ReturnToStartScene);

            // 场景切换 / 停止播放时不要把 timeScale 留在 0
            if (IsPaused) RestoreTimeScale();
            GamePause.IsPaused = false;
        }

        private void Update()
        {
            if (!toggleWithEscape) return;
            if (GameInput.ConsumeCancel()) Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;

            GamePause.IsPaused = true;
            cachedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;

            GamePause.IsPaused = false;
            RestoreTimeScale();

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        /// <summary>重新开始：先恢复时间，再让 BattleController 重开一局。</summary>
        public void RestartBattle()
        {
            Resume();

            if (battleController == null)
            {
                Debug.LogError("[PauseMenu] 找不到 BattleController，无法重新开始", this);
                return;
            }

            battleController.RestartBattle();
        }

        /// <summary>回到开始界面：恢复时间后加载开始场景。</summary>
        public void ReturnToStartScene()
        {
            Time.timeScale = 1f;
            GamePause.IsPaused = false;

            if (string.IsNullOrEmpty(startSceneName))
            {
                Debug.LogError("[PauseMenu] 没有填写 startSceneName", this);
                return;
            }

            SceneManager.LoadScene(startSceneName);
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = cachedTimeScale > 0f ? cachedTimeScale : 1f;
        }
    }
}
