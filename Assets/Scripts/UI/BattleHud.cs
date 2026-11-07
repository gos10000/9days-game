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
