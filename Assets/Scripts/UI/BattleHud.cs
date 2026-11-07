using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 战斗回合 HUD（Canvas 版）：只显示轮次与当前行动方，不显示血量等其它信息。
    /// 数据全部来自 GameEvents，自身不持有任何战斗状态。
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Text roundText;
        [SerializeField] private Text actorText;

        [Tooltip("留空则自动在场景里找；用于「事件漏收」时按真实状态纠正显示")]
        [SerializeField] private TurnController turnController;

        [Tooltip("检测到事件没更新、改由运行时状态纠正时，在 Console 打一条日志")]
        [SerializeField] private bool logCorrections = true;

        [Header("文案")]
        [Tooltip("未开局 / 无回合时的占位文字")]
        [SerializeField] private string idleText = "准备中";

        [Tooltip("战斗结束时的提示文字")]
        [SerializeField] private string endedText = "战斗结束";

        [Header("颜色")]
        [Tooltip("「谁的回合」这一行的颜色")]
        [SerializeField] private Color actorColor = new Color(0.42f, 1f, 0.55f);

        [Tooltip("无回合 / 战斗结束时的颜色")]
        [SerializeField] private Color idleColor = new Color(0.72f, 0.75f, 0.82f);

        private int round;
        private int totalRounds;
        private Team actor = Team.None;
        private bool ended;

        private void Awake()
        {
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
        }

        private void Update()
        {
            SyncFromRuntime();
        }

        /// <summary>
        /// 兜底刷新：事件（TurnStarted）可能因为 UI 重建、订阅被清空、或流程中途被打断而漏收，
        /// 此时文字会永远停在上一个行动方。这里每帧比对 TurnController 的真实状态并纠正。
        ///
        /// 只在「运行时确实有一个行动方」且与当前显示不同时才改：
        /// 结算间隙 CurrentTeam 会被置成 None，直接镜像会把文字闪成空白，所以不跟 None。
        /// </summary>
        private void SyncFromRuntime()
        {
            if (turnController == null)
            {
                turnController = FindAnyObjectByType<TurnController>();
                if (turnController == null) return;
            }

            Team live = turnController.CurrentTeam;
            if (live == Team.None || live == actor) return;

            if (logCorrections && !ended)
                Debug.Log($"[BattleHud] 未收到 TurnStarted，已按运行时状态纠正为 {live}", this);

            actor = live;
            ended = false;
            Refresh();
        }

        private void OnEnable()
        {
            GameEvents.BattleStarted += OnBattleStarted;
            GameEvents.RoundStarted += OnRoundStarted;
            GameEvents.TurnStarted += OnTurnStarted;
            GameEvents.BattleEnded += OnBattleEnded;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.BattleStarted -= OnBattleStarted;
            GameEvents.RoundStarted -= OnRoundStarted;
            GameEvents.TurnStarted -= OnTurnStarted;
            GameEvents.BattleEnded -= OnBattleEnded;
        }

        private void OnBattleStarted(int rounds)
        {
            totalRounds = rounds;
            round = 1;
            actor = Team.None;
            ended = false;
            Refresh();
        }

        private void OnRoundStarted(int roundIndex, Team firstActor)
        {
            round = roundIndex;
            if (firstActor != Team.None) actor = firstActor;
            ended = false;
            Refresh();
        }

        private void OnTurnStarted(Team team)
        {
            actor = team;
            ended = false;
            Refresh();
        }

        private void OnBattleEnded(BattleResult result)
        {
            ended = true;
            Refresh();
        }

        private void Refresh()
        {
            if (roundText != null)
                roundText.text = totalRounds > 0 ? $"第 {round} / {totalRounds} 轮" : idleText;

            if (actorText == null) return;

            if (ended)
            {
                actorText.text = endedText;
                actorText.color = idleColor;
            }
            else if (actor == Team.None)
            {
                actorText.text = string.Empty;
                actorText.color = idleColor;
            }
            else
            {
                actorText.text = $"{TeamName(actor)} 的回合";
                actorText.color = actorColor;
            }
        }

        private static string TeamName(Team team)
        {
            if (team == Team.Player1) return "玩家 1";
            if (team == Team.Player2) return "玩家 2";
            return "-";
        }
    }
}
