using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 回合信息常驻 HUD（占位实现）：屏幕正上方固定显示「第 X / N 轮」与当前行动方。
    /// 只订阅事件、不影响战斗逻辑；以后换正式 UI（Canvas / TMP）时只改这个组件。
    /// </summary>
    public class TurnBanner : MonoBehaviour
    {
        [Header("显示")]
        [SerializeField] private bool showBanner = true;

        [Tooltip("面板距屏幕顶部的像素距离")]
        [SerializeField] private float topMargin = 12f;

        [Tooltip("面板宽度（像素）")]
        [SerializeField] private float panelWidth = 380f;

        [Tooltip("面板高度（像素）")]
        [SerializeField] private float panelHeight = 66f;

        [Header("颜色")]
        [SerializeField] private Color player1Color = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color player2Color = new Color(0.45f, 0.78f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color drawColor = new Color(1f, 0.85f, 0.4f);

        private int round;
        private int totalRounds;
        private Team currentTeam = Team.None;
        private bool resolving;
        private bool battleRunning;
        private string resultText = string.Empty;
        private Color resultColor = Color.white;

        /// <summary>运行时显隐；非战斗场景可强制关闭。</summary>
        public bool Visible
        {
            get => showBanner;
            set
            {
                showBanner = value;
                if (!value) alpha = 0f;
            }
        }

        private void OnEnable()
        {
            GameEvents.BattleStarted += OnBattleStarted;
            GameEvents.RoundStarted += OnRoundStarted;
            GameEvents.TurnStarted += OnTurnStarted;
            GameEvents.TurnResolved += OnTurnResolved;
            GameEvents.BattleEnded += OnBattleEnded;
        }

        private void OnDisable()
        {
            GameEvents.BattleStarted -= OnBattleStarted;
            GameEvents.RoundStarted -= OnRoundStarted;
            GameEvents.TurnStarted -= OnTurnStarted;
            GameEvents.TurnResolved -= OnTurnResolved;
            GameEvents.BattleEnded -= OnBattleEnded;
        }

        private void OnBattleStarted(int rounds)
        {
            totalRounds = rounds;
            round = 0;
            currentTeam = Team.None;
            resolving = false;
            battleRunning = true;
            resultText = string.Empty;
        }

        private void OnRoundStarted(int roundIndex, Team firstActor)
        {
            round = roundIndex;
        }

        private void OnTurnStarted(Team team)
        {
            currentTeam = team;
            resolving = false;
        }

        private void OnTurnResolved(TurnResult result)
        {
            resolving = true;
        }

        private void OnBattleEnded(BattleResult result)
        {
            battleRunning = false;
            resolving = false;

            resultText = result.Winner == Team.None ? "DRAW" : $"WINNER: {TeamName(result.Winner)}";
            resultColor = result.Winner == Team.None ? drawColor : TeamColor(result.Winner);
        }

        private void OnGUI()
        {
            if (!showBanner) return;

            ResolveLines(out string title, out string subtitle, out Color color);

            float width = Mathf.Min(Mathf.Max(120f, panelWidth), Screen.width - 24f);
            float height = Mathf.Max(40f, panelHeight);
            var rect = new Rect((Screen.width - width) * 0.5f, topMargin, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
            };
            titleStyle.normal.textColor = color;

            var subtitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 15,
                fontStyle = FontStyle.Normal,
            };

            float titleHeight = Mathf.Round(height * 0.52f);
            GUI.Label(new Rect(rect.x, rect.y, width, titleHeight), title, titleStyle);

            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(rect.x, rect.y + titleHeight - 4f, width, height - titleHeight), subtitle, subtitleStyle);
        }

        private void ResolveLines(out string title, out string subtitle, out Color color)
        {
            if (!battleRunning && !string.IsNullOrEmpty(resultText))
            {
                title = resultText;
                subtitle = totalRounds > 0 ? $"共 {totalRounds} 轮" : string.Empty;
                color = resultColor;
                return;
            }

            if (round <= 0)
            {
                title = "准备中";
                subtitle = totalRounds > 0 ? $"共 {totalRounds} 轮" : string.Empty;
                color = neutralColor;
                return;
            }

            title = totalRounds > 0 ? $"第 {round} / {totalRounds} 轮" : $"第 {round} 轮";

            if (resolving)
            {
                subtitle = "结算中…";
                color = neutralColor;
            }
            else if (currentTeam != Team.None)
            {
                subtitle = $"{TeamName(currentTeam)} 的回合 · 拖拽松开发射";
                color = TeamColor(currentTeam);
            }
            else
            {
                subtitle = "准备中…";
                color = neutralColor;
            }
        }

        private static string TeamName(Team team)
        {
            if (team == Team.Player1) return "Player1";
            if (team == Team.Player2) return "Player2";
            return "-";
        }

        private Color TeamColor(Team team)
        {
            if (team == Team.Player1) return player1Color;
            if (team == Team.Player2) return player2Color;
            return neutralColor;
        }
    }
}
