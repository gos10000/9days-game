using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 回合流程（文档「战斗」）：开始回合 → 瞄准 → 结算 → 换人。
    /// 两人轮流用同一套鼠标，只有当前回合的角色可操作。
    /// </summary>
    public class TurnController : MonoBehaviour
    {
        [Header("角色")]
        [SerializeField] private List<Character> characters = new List<Character>();

        [Header("规则")]
        [SerializeField] private Team firstTeam = Team.Player1;
        [SerializeField] private bool autoStart = true;

        [Tooltip("true = 结算后自动换人（旧行为）；false = 交给 BattleController 决定下一个行动者")]
        [SerializeField] private bool autoAdvance = true;

        [Tooltip("结算最长等待时间，超时强制换人")]
        [SerializeField] private float resolveTimeout = 3f;

        [Header("调试")]
        [Tooltip("每次开始回合在 Console 打印行动方、firstTeam 与各角色的队伍，用于排查「谁先手」")]
        [SerializeField] private bool logTurns = true;

        private float resolveTimer;
        private Team turnActor = Team.None;
        private bool shotFiredThisTurn;
        private bool hitLandedThisTurn;
        private float damageThisTurn;
        private bool targetDefeatedThisTurn;

        /// <summary>一次行动结算完成（供 BattleController 推进）。</summary>
        public event Action<TurnResult> TurnResolved;

        public Team CurrentTeam { get; private set; } = Team.None;
        public bool IsResolving { get; private set; }
        public int TurnIndex { get; private set; }
        public float ResolveElapsed => resolveTimer;
        public IReadOnlyList<Character> Characters => characters;

        private void Awake()
        {
            if (characters.Count == 0)
            {
                characters.AddRange(FindObjectsByType<Character>(FindObjectsSortMode.None));
                characters.Sort((a, b) => a.Team.CompareTo(b.Team));
            }
        }

        private void OnEnable()
        {
            GameEvents.ShotFired += OnShotFired;
            GameEvents.CharacterHit += OnCharacterHit;
        }

        private void OnDisable()
        {
            GameEvents.ShotFired -= OnShotFired;
            GameEvents.CharacterHit -= OnCharacterHit;
        }

        private void Start()
        {
            if (autoStart) BeginTurn(firstTeam);
        }

        private void Update()
        {
            if (!IsResolving) return;

            resolveTimer += Time.deltaTime;
            if (HasSettled() || resolveTimer >= resolveTimeout) EndTurn();
        }

        public void BeginTurn(Team team)
        {
            CurrentTeam = team;
            IsResolving = false;
            resolveTimer = 0f;
            TurnIndex++;

            turnActor = team;
            shotFiredThisTurn = false;
            hitLandedThisTurn = false;
            damageThisTurn = 0f;
            targetDefeatedThisTurn = false;

            for (int i = 0; i < characters.Count; i++)
            {
                Character c = characters[i];
                if (c == null) continue;

                c.SetCanAct(c.Team == team);
                if (c.Team == team)
                {
                    Character opponent = GetOpponent(team);
                    if (opponent != null) c.FaceTo(opponent.transform.position);
                }
            }

            GameEvents.RaiseTurnStarted(team);

            if (logTurns)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[Turn] 第 {TurnIndex} 次 BeginTurn → 行动方 {team}（firstTeam={firstTeam}，autoAdvance={autoAdvance}）");
                for (int i = 0; i < characters.Count; i++)
                {
                    Character c = characters[i];
                    sb.Append(c == null ? " | <空引用>" : $" | {c.name}={c.Team}");
                }
                Debug.Log(sb.ToString(), this);
            }
        }

        /// <summary>强制切换到某队（调试热键 1 / 2）。</summary>
        public void ForceSwitch(Team team) => BeginTurn(team);

        public Character GetCharacter(Team team)
        {
            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null && characters[i].Team == team) return characters[i];
            return null;
        }

        public Character GetOpponent(Team team) => GetCharacter(OpponentOf(team));

        public static Team OpponentOf(Team team)
        {
            if (team == Team.Player1) return Team.Player2;
            if (team == Team.Player2) return Team.Player1;
            return Team.None;
        }

        /// <summary>重开一局：角色回出生点、清掉场上子弹、回到第一回合。</summary>
        public void ResetMatch()
        {
            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
                if (projectiles[i] != null) projectiles[i].Despawn(ExpireReason.Aborted);

            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null) characters[i].ResetToSpawn();

            BeginTurn(firstTeam);
        }

        private void OnShotFired(Character shooter, Projectile projectile)
        {
            if (shooter == null || shooter.Team != CurrentTeam || IsResolving) return;

            shotFiredThisTurn = true;
            IsResolving = true;
            resolveTimer = 0f;

            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null) characters[i].SetCanAct(false);
        }

        private void OnCharacterHit(Character target, HitInfo info)
        {
            if (!IsResolving || target == null) return;
            if (target.Team == turnActor) return;

            hitLandedThisTurn = true;
            damageThisTurn += Mathf.Max(0f, info.Damage);
            if (target.IsDefeated) targetDefeatedThisTurn = true;
        }

        /// <summary>由 BattleController 接管时调用：停用自动开局 / 自动换人。</summary>
        public void UseExternalDriver()
        {
            autoStart = false;
            autoAdvance = false;
        }

        /// <summary>强制结束当前行动（战斗结束 / 切场景 / 调试用）。</summary>
        public void AbortTurn()
        {
            if (turnActor == Team.None && CurrentTeam == Team.None) return;
            EndTurn();
        }

        /// <summary>
        /// 取消本回合的这一次发射：回到「还没开枪」的状态。
        /// 不推进回合（TurnIndex 不变）、不移动角色、不换队，只把发射权还给当前角色。
        /// 用于开始界面的“无限重打”，以及调试时想重来一枪。
        /// </summary>
        public void CancelShot()
        {
            Team team = CurrentTeam != Team.None ? CurrentTeam
                      : (turnActor != Team.None ? turnActor : firstTeam);

            if (team == Team.None) return;

            IsResolving = false;
            resolveTimer = 0f;
            CurrentTeam = team;
            turnActor = team;

            // 这几个标记是“本回合战果”，取消发射后应当一起清掉，否则回合结算会带着旧数据
            shotFiredThisTurn = false;
            hitLandedThisTurn = false;
            damageThisTurn = 0f;
            targetDefeatedThisTurn = false;

            for (int i = 0; i < characters.Count; i++)
            {
                Character c = characters[i];
                if (c == null) continue;

                c.SetCanAct(c.Team == team);
                if (c.Team == team) c.CancelAim();
            }
        }

        private void EndTurn()
        {
            Team finished = turnActor != Team.None ? turnActor : CurrentTeam;

            IsResolving = false;
            resolveTimer = 0f;
            CurrentTeam = Team.None;

            var result = new TurnResult(finished, shotFiredThisTurn, hitLandedThisTurn,
                                        damageThisTurn, targetDefeatedThisTurn);

            TurnResolved?.Invoke(result);
            GameEvents.RaiseTurnEnded(finished);

            if (autoAdvance) BeginTurn(OpponentOf(finished));
        }

        /// <summary>结算完成：场上没有子弹，且双方都停稳。</summary>
        private bool HasSettled()
        {
            if (FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0) return false;

            for (int i = 0; i < characters.Count; i++)
            {
                Character c = characters[i];
                if (c != null && !c.IsSettled) return false;
            }
            return true;
        }
    }
}
