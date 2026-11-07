using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 结算画面：战斗结束时显示 WE WINNER 字样，下方给出「重新开始 / 退出」。
    ///
    /// 面板默认隐藏，只订阅 GameEvents，不参与任何战斗逻辑。
    /// 两个按钮直接复用暂停面板的 PauseMenu 方法，保证行为完全一致
    /// （重新开始会先恢复 timeScale，退出会先恢复时间再切场景）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleResultView : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("整块结算内容；默认隐藏，战斗结束时显示")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private Text winnerText;

        [SerializeField] private Button restartButton;
        [SerializeField] private Button exitButton;

        [Tooltip("留空则自动查找场景里的 PauseMenu")]
        [SerializeField] private PauseMenu pauseMenu;

        [Header("文案")]
        [Tooltip("获胜文案；{0} 会替换成获胜方名称")]
        [SerializeField] private string winnerFormat = "WINNER！！！";

        [Tooltip("胜负未分（同时倒下 / 时间到）时的文案")]
        [SerializeField] private string drawText = "DRAW";

        private void Awake()
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();

            if (restartButton != null) restartButton.onClick.AddListener(RestartBattle);
            if (exitButton != null) exitButton.onClick.AddListener(ExitToStart);

            Hide();
        }

        private void OnEnable()
        {
            GameEvents.BattleEnded += OnBattleEnded;
            GameEvents.BattleStarted += OnBattleStarted;
        }

        private void OnDisable()
        {
            GameEvents.BattleEnded -= OnBattleEnded;
            GameEvents.BattleStarted -= OnBattleStarted;
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartBattle);
            if (exitButton != null) exitButton.onClick.RemoveListener(ExitToStart);
        }

        private void OnBattleEnded(BattleResult result)
        {
            if (winnerText != null)
            {
                winnerText.text = result.Winner == Team.None
                    ? drawText
                    : winnerFormat;
            }

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>重开一局时收起来，避免盖住新一局的画面。</summary>
        private void OnBattleStarted(int rounds) => Hide();

        private void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void RestartBattle()
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (pauseMenu == null)
            {
                Debug.LogError("[BattleResultView] 找不到 PauseMenu，无法重新开始", this);
                return;
            }

            Hide();
            pauseMenu.RestartBattle();
        }

        private void ExitToStart()
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (pauseMenu == null)
            {
                Debug.LogError("[BattleResultView] 找不到 PauseMenu，无法退出", this);
                return;
            }

            pauseMenu.ReturnToStartScene();
        }
    }
}
