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

        [Tooltip("屏幕上额外的「暂停 / 停止」按钮；点它等同于按 ESC。留空则只响应 ESC")]
        [SerializeField] private Button stopButton;

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
            if (stopButton != null) stopButton.onClick.AddListener(Toggle);

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartBattle);
            if (quitButton != null) quitButton.onClick.RemoveListener(ReturnToStartScene);
            if (stopButton != null) stopButton.onClick.RemoveListener(Toggle);

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

            // 暂停面板遮住全屏时，把角落的停止键收起来，避免它盖在面板上面
            if (stopButton != null) stopButton.gameObject.SetActive(false);
        }

        public void Resume()
        {
            if (!IsPaused) return;

            GamePause.IsPaused = false;
            RestoreTimeScale();

            if (panelRoot != null) panelRoot.SetActive(false);
            if (stopButton != null) stopButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// 重新开始：**重新加载当前战斗场景**，而不是就地重置。
        ///
        /// 为什么必须重载：就地重开只会重建地形 / 复位角色，场景里手摆的东西（比如可破坏木板、
        /// 被推开的箱子、已消耗的道具）会保持被破坏后的状态。重载场景等于把整局恢复到初始状态。
        ///
        /// 场景不在 Build Settings 里时无法按名字加载，退回 BattleController 的就地重开并给出警告。
        /// </summary>
        public void RestartBattle()
        {
            // 先恢复时间和暂停状态，否则新场景一进来就是冻结的。
            // 暂停中走 Resume()：它顺带把面板收起、把停止键放回来；
            // 从结算界面进来时通常不是「暂停」状态，但 timeScale 可能被压成 0，所以再兜一层。
            if (IsPaused) Resume();
            Time.timeScale = 1f;
            GamePause.IsPaused = false;
            cachedTimeScale = 1f;

            if (panelRoot != null) panelRoot.SetActive(false);

            Scene active = SceneManager.GetActiveScene();

            if (!string.IsNullOrEmpty(active.name) && Application.CanStreamedLevelBeLoaded(active.name))
            {
                Debug.Log($"[PauseMenu] 重新开始：重新加载场景 \"{active.name}\"");
                SceneManager.LoadScene(active.name, LoadSceneMode.Single);
                return;
            }

            Debug.LogWarning($"[PauseMenu] 场景 \"{active.name}\" 不在 Build Settings 里，无法重载；" +
                             "退回就地重开（场景里摆放的障碍物不会恢复）。" +
                             "要完整重置请把该场景勾进 File > Build Settings > Scenes In Build");

            if (battleController == null) battleController = FindAnyObjectByType<BattleController>();
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
