using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 战斗轮驱动：把纯逻辑 BattleSession 接到场景、TurnController 与 GameEvents。
    /// 场景里由 ArenaBuilder 生成并连好引用；测试直接测 BattleSession。
    /// </summary>
    public class BattleController : MonoBehaviour, ITurnActionRunner
    {
        [Header("引用")]
        [SerializeField] private BattleConfig config;
        [SerializeField] private TurnController turnController;
        [SerializeField] private List<Character> characters = new List<Character>();

        [Header("启动")]
        [SerializeField] private bool autoStart = true;

        private BattleSession session;
        private Action<TurnResult> pendingCallback;
        private Coroutine roundDelayRoutine;
        private Coroutine turnDelayRoutine;

        public BattleSession Session => session;
        public int Round => session != null ? session.Round : 0;
        public int TotalRounds => config != null ? config.roundsToPlay : 0;
        public Team CurrentActor => session != null ? session.CurrentActor : Team.None;
        public bool IsRunning => session != null && session.IsRunning;

        private void Awake()
        {
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
            if (turnController != null) turnController.UseExternalDriver();

            EnsureTurnBanner();
        }

        /// <summary>场景里没有 TurnBanner 时自动补一个，保证「轮到谁」始终可见。</summary>
        private void EnsureTurnBanner()
        {
            if (FindAnyObjectByType<TurnBanner>() != null) return;
            gameObject.AddComponent<TurnBanner>();
        }

        private void OnEnable()
        {
            if (turnController != null) turnController.TurnResolved += HandleTurnControllerResolved;
        }

        private void OnDisable()
        {
            if (turnController != null) turnController.TurnResolved -= HandleTurnControllerResolved;
        }

        private void Start()
        {
            if (autoStart) BeginBattle();
        }

        /// <summary>开始一场战斗（回血、回出生点、进入第 1 轮）。</summary>
        public void BeginBattle()
        {
            if (session != null && session.IsRunning) return;

            if (config == null || turnController == null)
            {
                Debug.LogError("[Battle] BattleConfig 或 TurnController 未配置");
                return;
            }

            var participants = new List<IBattleParticipant>();
            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null) participants.Add(characters[i]);

            if (participants.Count < 2)
            {
                Debug.LogError("[Battle] 至少需要两名角色");
                return;
            }

            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null) characters[i].ResetForBattle();

            session = new BattleSession(config, participants, this);
            session.AutoAdvanceRounds = false;
            session.AutoAdvanceTurns = false;
            session.RoundStarted += HandleRoundStarted;
            session.RoundEnded += HandleRoundEnded;
            session.TurnResolved += HandleSessionTurnResolved;
            session.ExtraTurnGranted += GameEvents.RaiseExtraTurnGranted;
            session.BattleEnded += HandleBattleEnded;

            GameEvents.RaiseBattleStarted(config.roundsToPlay);
            session.Start();
        }

        /// <summary>追加额外行动（技能/连击以后调用）。</summary>
        public void RequestExtraTurn(Team team, bool immediate = false, int count = 1)
        {
            session?.RequestExtraTurn(team, immediate, count);
        }

        /// <summary>重开一局：清子弹、回满血、回出生点，再开始。</summary>
        public void RestartBattle()
        {
            if (session != null)
            {
                session.Abort();
                session = null;
            }

            pendingCallback = null;

            if (roundDelayRoutine != null)
            {
                StopCoroutine(roundDelayRoutine);
                roundDelayRoutine = null;
            }

            if (turnDelayRoutine != null)
            {
                StopCoroutine(turnDelayRoutine);
                turnDelayRoutine = null;
            }

            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
                if (projectiles[i] != null) projectiles[i].Despawn(ExpireReason.Aborted);

            BeginBattle();
        }

        public void Abort()
        {
            pendingCallback = null;
            turnController?.AbortTurn();
        }

        // ---- ITurnActionRunner ----

        public void BeginTurn(Team actor, Action<TurnResult> onResolved)
        {
            pendingCallback = onResolved;
            turnController.BeginTurn(actor);
        }

        private void HandleTurnControllerResolved(TurnResult result)
        {
            Action<TurnResult> callback = pendingCallback;
            pendingCallback = null;
            callback?.Invoke(result);
        }

        // ---- BattleSession 事件转发 ----

        private void HandleRoundStarted(int round, Team firstActor)
        {
            GameEvents.RaiseRoundStarted(round, firstActor);
        }

        private void HandleRoundEnded(int round)
        {
            GameEvents.RaiseRoundEnded(round);

            if (session == null || !session.IsRunning) return;

            if (config.roundResolveDelay > 0f)
                roundDelayRoutine = StartCoroutine(ContinueAfterDelay(config.roundResolveDelay));
            else
                session.ContinueAfterRound();
        }

        private IEnumerator ContinueAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            roundDelayRoutine = null;
            session?.ContinueAfterRound();
        }

        private void HandleSessionTurnResolved(TurnResult result)
        {
            GameEvents.RaiseTurnResolved(result);

            if (session != null && session.IsRunning && session.IsAwaitingTurnAdvance)
                turnDelayRoutine = StartCoroutine(ContinueTurnAfterDelay(config.turnResolveDelay));
        }

        private IEnumerator ContinueTurnAfterDelay(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            turnDelayRoutine = null;
            session?.ContinueAfterTurn();
        }

        private void HandleBattleEnded(BattleResult result)
        {
            if (roundDelayRoutine != null)
            {
                StopCoroutine(roundDelayRoutine);
                roundDelayRoutine = null;
            }

            if (turnDelayRoutine != null)
            {
                StopCoroutine(turnDelayRoutine);
                turnDelayRoutine = null;
            }

            GameEvents.RaiseBattleEnded(result);
        }
    }
}
