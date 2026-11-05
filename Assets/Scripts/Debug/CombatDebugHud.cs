using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 测试场景专用的调试面板（不进正式玩法）。
    /// 显示回合 / 角色状态 / 最近事件，并提供发球、切回合、重置、慢动作等热键。
    /// </summary>
    public class CombatDebugHud : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private TurnController turnController;
        [SerializeField] private BattleController battleController;
        [SerializeField] private Projectile testProjectilePrefab;

        [Header("显示")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private int maxLogLines = 8;

        [Header("调试")]
        [SerializeField] private bool verboseImpactLog;
        [SerializeField] private float autoFireDelay = 0.6f;

        [Header("自动测试")]
        [Tooltip("F2 / T 自动对射的发射次数")]
        [SerializeField] private int autoTestRounds = 20;

        private readonly List<string> logLines = new List<string>();
        private bool autoTest;
        private float autoTimer;
        private int autoShots;
        private int autoHits;
        private int autoKnockbacks;
        private int autoStrongImpacts;
        private int autoGetUps;
        private string winnerText = string.Empty;

        private void Awake()
        {
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
            if (battleController == null) battleController = FindAnyObjectByType<BattleController>();
        }

        private void OnEnable()
        {
            GameEvents.CharacterHit += OnCharacterHit;
            GameEvents.CharacterKnockedBack += OnKnockedBack;
            GameEvents.ImpactDetected += OnImpactDetected;
            GameEvents.StrongImpact += OnStrongImpact;
            GameEvents.TurnStarted += OnTurnStarted;
            GameEvents.CharacterDowned += OnDowned;
            GameEvents.CharacterRecovered += OnRecovered;
            GameEvents.CharacterOutOfBounds += OnOutOfBounds;
            GameEvents.BattleStarted += OnBattleStarted;
            GameEvents.RoundStarted += OnRoundStarted;
            GameEvents.RoundEnded += OnRoundEnded;
            GameEvents.BattleEnded += OnBattleEnded;
            GameEvents.ExtraTurnGranted += OnExtraTurnGranted;
            GameEvents.CharacterDefeated += OnCharacterDefeated;
        }

        private void OnDisable()
        {
            GameEvents.CharacterHit -= OnCharacterHit;
            GameEvents.CharacterKnockedBack -= OnKnockedBack;
            GameEvents.ImpactDetected -= OnImpactDetected;
            GameEvents.StrongImpact -= OnStrongImpact;
            GameEvents.TurnStarted -= OnTurnStarted;
            GameEvents.CharacterDowned -= OnDowned;
            GameEvents.CharacterRecovered -= OnRecovered;
            GameEvents.CharacterOutOfBounds -= OnOutOfBounds;
            GameEvents.BattleStarted -= OnBattleStarted;
            GameEvents.RoundStarted -= OnRoundStarted;
            GameEvents.RoundEnded -= OnRoundEnded;
            GameEvents.BattleEnded -= OnBattleEnded;
            GameEvents.ExtraTurnGranted -= OnExtraTurnGranted;
            GameEvents.CharacterDefeated -= OnCharacterDefeated;
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameInputAction.TestShot)) FireTestShot();
            if (GameInput.WasPressed(GameInputAction.ForcePlayer1)) turnController?.ForceSwitch(Team.Player1);
            if (GameInput.WasPressed(GameInputAction.ForcePlayer2)) turnController?.ForceSwitch(Team.Player2);

            if (GameInput.WasPressed(GameInputAction.ExtraTurn))
            {
                Team actor = battleController != null ? battleController.CurrentActor : Team.None;
                if (actor == Team.None && turnController != null) actor = turnController.CurrentTeam;

                if (actor != Team.None && battleController != null)
                {
                    battleController.RequestExtraTurn(actor, true);
                    Log($"extra turn requested: {actor}");
                }
            }

            if (GameInput.WasPressed(GameInputAction.Restart))
            {
                if (battleController != null) battleController.RestartBattle();
                else turnController?.ResetMatch();

                logLines.Clear();
                winnerText = string.Empty;
                Log("===== reset =====");
            }

            if (GameInput.WasPressed(GameInputAction.ToggleImpactLog))
            {
                verboseImpactLog = !verboseImpactLog;
                Log($"verbose impact = {verboseImpactLog}");
            }

            if (GameInput.WasPressed(GameInputAction.ToggleAutoTest)) ToggleAutoTest();
            if (GameInput.WasPressed(GameInputAction.ToggleSlowMotion)) ToggleSlowMotion();

            if (autoTest) UpdateAutoTest();
        }

        private void UpdateAutoTest()
        {
            if (turnController == null || turnController.IsResolving) return;

            if (autoShots >= autoTestRounds)
            {
                FinishAutoTest();
                return;
            }

            autoTimer += Time.deltaTime;
            if (autoTimer < autoFireDelay) return;

            autoTimer = 0f;
            FireTestShot();
        }

        private void ToggleAutoTest()
        {
            autoTest = !autoTest;
            autoTimer = 0f;

            if (!autoTest)
            {
                Log("auto test OFF");
                return;
            }

            autoShots = 0;
            autoHits = 0;
            autoKnockbacks = 0;
            autoStrongImpacts = 0;
            autoGetUps = 0;
            Log($"auto test ON ({autoTestRounds} shots)");
        }

        private void FinishAutoTest()
        {
            autoTest = false;
            Log($"auto test DONE: shots {autoShots} / hits {autoHits} / knockback {autoKnockbacks} / " +
                $"strong {autoStrongImpacts} / getup {autoGetUps}");
        }

        /// <summary>当前回合角色直接朝对手发一颗球（跳过拖拽，用于快速回归测试）。</summary>
        public void FireTestShot()
        {
            if (testProjectilePrefab == null || turnController == null) return;

            Character shooter = turnController.GetCharacter(turnController.CurrentTeam);
            if (shooter == null) return;

            Character target = turnController.GetOpponent(turnController.CurrentTeam);
            Vector2 origin = ShotSpawner.GetOrigin(shooter);
            Vector2 direction = target != null
                ? ((Vector2)target.transform.position - origin).normalized
                : Vector2.right;

            float speed = testProjectilePrefab.Config != null ? testProjectilePrefab.Config.initialSpeed : 15f;
            Projectile projectile = ShotSpawner.Spawn(testProjectilePrefab, shooter, direction, speed);
            if (projectile == null) return;

            shooter.FaceTo(direction);
            if (autoTest) autoShots++;

            GameEvents.RaiseShotFired(shooter, projectile);
            Log($"{shooter.Team} shot -> {(target != null ? target.Team.ToString() : "-")}");
        }

        private void ToggleSlowMotion()
        {
            Time.timeScale = Time.timeScale > 0.5f ? 0.2f : 1f;
            Log($"timeScale = {Time.timeScale:0.0}");
        }

        private void OnCharacterHit(Character c, HitInfo info)
        {
            if (autoTest) autoHits++;
            Log($"{NameOf(c)} HIT {info}");
        }

        private void OnKnockedBack(Character c, HitInfo info)
        {
            if (autoTest) autoKnockbacks++;
            Log($"{NameOf(c)} KNOCKBACK impulse={info.Impulse:0.00}");
        }

        private void OnStrongImpact(Character c, ImpactInfo info)
        {
            if (autoTest) autoStrongImpacts++;
            Log($"{NameOf(c)} STRONG {info}");
        }

        private void OnTurnStarted(Team team) => Log($"--- turn {team} ---");
        private void OnDowned(Character c) => Log($"{NameOf(c)} DOWNED");

        private void OnRecovered(Character c)
        {
            if (autoTest) autoGetUps++;
            Log($"{NameOf(c)} GET UP");
        }
        private void OnOutOfBounds(Character c) => Log($"{NameOf(c)} OUT OF BOUNDS");

        private void OnBattleStarted(int rounds) => Log($"=== BATTLE START ({rounds} rounds) ===");

        private void OnRoundStarted(int round, Team first) => Log($"--- ROUND {round} (first: {first}) ---");

        private void OnRoundEnded(int round) => Log($"--- ROUND {round} END ---");

        private void OnBattleEnded(BattleResult result)
        {
            winnerText = result.Winner == Team.None ? "DRAW" : $"WINNER: {result.Winner}";
            Log($"=== BATTLE END {winnerText} | rounds {result.RoundsPlayed} | HP {result.HealthA:0}/{result.HealthB:0} ===");
        }

        private void OnExtraTurnGranted(Team team, bool immediate) =>
            Log($"EXTRA TURN {team} ({(immediate ? "next" : "last")})");

        private void OnCharacterDefeated(Character c) => Log($"{NameOf(c)} DEFEATED");

        private void OnImpactDetected(Character c, ImpactInfo info)
        {
            if (verboseImpactLog) Log($"{NameOf(c)} impact {info}");
        }

        private void Log(string message)
        {
            string line = $"[{Time.time:0.00}] {message}";
            logLines.Add(line);
            while (logLines.Count > Mathf.Max(1, maxLogLines)) logLines.RemoveAt(0);
            Debug.Log($"[HUD] {line}");
        }

        private static string NameOf(Character c) => c != null ? c.name : "?";

        private void OnGUI()
        {
            if (!showHud) return;

            GUILayout.BeginArea(new Rect(10f, 10f, 640f, 270f), GUI.skin.box);

            string turn = turnController != null ? turnController.CurrentTeam.ToString() : "-";
            bool resolving = turnController != null && turnController.IsResolving;
            int round = battleController != null ? battleController.Round : 0;
            int totalRounds = battleController != null ? battleController.TotalRounds : 0;
            string auto = autoTest ? $"ON {autoShots}/{autoTestRounds}" : "OFF";
            GUILayout.Label($"Round: {round}/{totalRounds}   Turn: {turn}   Resolving: {resolving}   AutoTest: {auto}   TimeScale: {Time.timeScale:0.0}");

            if (!string.IsNullOrEmpty(winnerText)) GUILayout.Label(winnerText);

            if (turnController != null)
            {
                IReadOnlyList<Character> list = turnController.Characters;
                for (int i = 0; i < list.Count; i++)
                {
                    Character c = list[i];
                    if (c == null) continue;

                    float hp = c.CurrentHealth;
                    float maxHp = c.Health != null ? c.Health.Max : 0f;
                    GUILayout.Label($"{c.Team} [{c.State}] HP {hp:0}/{maxHp:0} vel {c.Velocity.magnitude:0.00} " +
                                    $"grounded {(c.IsGrounded ? "Y" : "N")} canAct {(c.CanAct ? "Y" : "N")}");
                }
            }

            GUILayout.Space(4f);
            for (int i = logLines.Count - 1; i >= 0; i--) GUILayout.Label(logLines[i]);

            GUILayout.Label("Space=shoot  1/2=turn  E=extra turn  R=reset  F1=impact log  T/F2=auto test  G/F3=slow motion");
            GUILayout.EndArea();
        }
    }
}
