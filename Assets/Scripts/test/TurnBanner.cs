    using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 回合提示横幅（占位 UI）：显示当前轮次与行动方，只订阅事件、不影响战斗逻辑。
    /// 以后换正式 UI（Canvas / TMP）时只改这个组件。
    /// </summary>
    public class TurnBanner : MonoBehaviour
    {
        [Header("显示")]
        [SerializeField] private bool showBanner = true;

        [Tooltip("完全显示后的停留时间（秒）")]
        [SerializeField] private float holdSeconds = 1.2f;

        [Tooltip("淡入 / 淡出时长（秒）")]
        [SerializeField] private float fadeSeconds = 0.25f;

        [Header("颜色")]
        [SerializeField] private Color player1Color = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color player2Color = new Color(0.45f, 0.78f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color drawColor = new Color(1f, 0.85f, 0.4f);

        private string title = string.Empty;
        private string subtitle = string.Empty;
        private Color color = Color.white;
        private float timer;
        private float alpha;
        private int round;
        private int totalRounds;

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

        private void Update()
        {
            if (timer <= 0f)
            {
                alpha = 0f;
                return;
            }

            timer -= Time.unscaledDeltaTime;
            float elapsed = holdSeconds + fadeSeconds - timer;
            float fadeIn = fadeSeconds > 0f ? Mathf.Clamp01(elapsed / fadeSeconds) : 1f;
            float fadeOut = fadeSeconds > 0f ? Mathf.Clamp01(timer / fadeSeconds) : 1f;
            alpha = Mathf.Min(fadeIn, fadeOut);
        }

        private void Show(string titleText, string subtitleText, Color textColor)
        {
            if (!showBanner) return;

            title = titleText;
            subtitle = subtitleText;
            color = textColor;
            timer = holdSeconds + fadeSeconds;
        }

        private void OnBattleStarted(int rounds)
        {
            totalRounds = rounds;
            Show("BATTLE START", $"共 {rounds} 轮", neutralColor);
        }

        private void OnRoundStarted(int roundIndex, Team firstActor)
        {
            round = roundIndex;
        }

        private void OnTurnStarted(Team team)
        {
            string who = team == Team.Player1 ? "Player1" : team == Team.Player2 ? "Player2" : "-";
            Show($"第 {round} / {totalRounds} 轮", $"{who} 的回合 · 拖拽松开发射", TeamColor(team));
        }

        private void OnTurnResolved(TurnResult result)
        {
            if (!string.IsNullOrEmpty(title) && timer > 0f) return;   // 不打断「轮到谁」提示
            Show($"第 {round} 轮", "结算中…", neutralColor);
        }

        private void OnBattleEnded(BattleResult result)
        {
            if (result.Winner == Team.None) Show("DRAW", $"共 {result.RoundsPlayed} 轮", drawColor);
            else Show($"WINNER: {result.Winner}", $"共 {result.RoundsPlayed} 轮", TeamColor(result.Winner));
        }

        private Color TeamColor(Team team)
        {
            if (team == Team.Player1) return player1Color;
            if (team == Team.Player2) return player2Color;
            return neutralColor;
        }

        private void OnGUI()
        {
            if (!showBanner || alpha <= 0.01f) return;

            float width = Mathf.Min(720f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.16f, width, 130f);

            GUI.color = new Color(0f, 0f, 0f, 0.35f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
            };
            titleStyle.normal.textColor = new Color(color.r, color.g, color.b, alpha);

            GUIStyle subtitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Normal,
            };

            GUI.Label(new Rect(rect.x, rect.y + 16f, width, 48f), title, titleStyle);
            GUI.Label(new Rect(rect.x, rect.y + 70f, width, 34f), subtitle, subtitleStyle);
        }
    }
}