using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 战斗镜头适配层：把回合 / 发射 / 子弹结束 / 命中事件翻译成 CameraDirector 的目标和震动。
    /// 核心取景逻辑不在这里。
    /// </summary>
    public sealed class BattleCameraController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private CameraDirector director;
        [SerializeField] private TurnController turnController;
        [SerializeField] private List<Character> characters = new List<Character>();

        [Header("目标")]
        [SerializeField] private bool followProjectiles = true;

        [Header("开场介绍")]
        [Tooltip("进入场景后先依次特写双方，再交给常规回合镜头")]
        [SerializeField] private bool playIntro = true;

        [Tooltip("先特写哪一方；另一方按对手顺序紧随其后")]
        [SerializeField] private Team introFirstTeam = Team.Player1;

        [Tooltip("特写时的相机 orthographicSize（越小越近）")]
        [SerializeField] private float introOrthographicSize = 3.2f;

        [Tooltip("每个角色特写停留时间（秒）")]
        [SerializeField] private float introHoldSeconds = 0.9f;

        [Tooltip("从第一个角色平滑移动到第二个角色所需时间（秒）")]
        [SerializeField] private float introMoveSeconds = 0.8f;

        [Header("震动")]
        [SerializeField] private float hitShakeBase = 0.12f;
        [SerializeField] private float hitShakePerImpulse = 0.02f;
        [SerializeField] private float strongImpactShakeBase = 0.2f;
        [SerializeField] private float strongImpactShakePerImpulse = 0.02f;
        [SerializeField] private float defeatedShake = 0.6f;
        [SerializeField] private float burstShake = 0.3f;

        private readonly List<Transform> turnTargets = new List<Transform>();
        private readonly List<Projectile> trackedProjectiles = new List<Projectile>();
        private bool subscribed;
        private bool introPlaying;
        private Coroutine introRoutine;

        public CameraDirector Director => director;
        public bool FollowProjectiles => followProjectiles;

        private void Awake()
        {
            if (director == null) director = GetComponent<CameraDirector>();
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
        }

        private void Start()
        {
            StartIntro();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopIntro();
            trackedProjectiles.Clear();
            turnTargets.Clear();
        }

        private void StartIntro()
        {
            if (!playIntro || director == null || introRoutine != null) return;
            introRoutine = StartCoroutine(IntroRoutine());
        }

        private void StopIntro()
        {
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }

            introPlaying = false;
            director?.ClearSizeOverride();
        }

        /// <summary>开场：先特写 P1，再平滑移到 P2，然后交回常规回合镜头。</summary>
        private IEnumerator IntroRoutine()
        {
            Character first = FindCharacter(introFirstTeam);
            Character second = FindOpponent(introFirstTeam);

            if (first == null && second == null)
            {
                introRoutine = null;
                yield break;
            }

            introPlaying = true;
            turnTargets.Clear();

            if (first != null)
            {
                director.FocusSingle(first.transform, introOrthographicSize, true);
                yield return new WaitForSeconds(introHoldSeconds);
            }

            if (second != null)
            {
                // snap = false：由 CameraRig 的 positionSmoothTime / sizeSmoothTime 平滑过渡
                director.FocusSingle(second.transform, introOrthographicSize, false);
                yield return new WaitForSeconds(introMoveSeconds + introHoldSeconds);
            }
            else
            {
                yield return new WaitForSeconds(introHoldSeconds);
            }

            introPlaying = false;
            introRoutine = null;
            director.ClearSizeOverride();

            RestoreNormalTargets();
        }

        /// <summary>开场结束：按「当前回合」重算常规目标；没有回合时回全景。</summary>
        private void RestoreNormalTargets()
        {
            Team current = turnController != null ? turnController.CurrentTeam : Team.None;
            if (current != Team.None)
            {
                OnTurnStarted(current);
                return;
            }

            ApplyTargets(false);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            GameEvents.BattleStarted += OnBattleStarted;
            GameEvents.TurnStarted += OnTurnStarted;
            GameEvents.ShotFired += OnShotFired;
            GameEvents.ProjectileExpired += OnProjectileExpired;
            GameEvents.CharacterHit += OnCharacterHit;
            GameEvents.StrongImpact += OnStrongImpact;
            GameEvents.CharacterDefeated += OnCharacterDefeated;
            GameEvents.BurstTriggered += OnBurstTriggered;
            GameEvents.BattleEnded += OnBattleEnded;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;

            GameEvents.BattleStarted -= OnBattleStarted;
            GameEvents.TurnStarted -= OnTurnStarted;
            GameEvents.ShotFired -= OnShotFired;
            GameEvents.ProjectileExpired -= OnProjectileExpired;
            GameEvents.CharacterHit -= OnCharacterHit;
            GameEvents.StrongImpact -= OnStrongImpact;
            GameEvents.CharacterDefeated -= OnCharacterDefeated;
            GameEvents.BurstTriggered -= OnBurstTriggered;
            GameEvents.BattleEnded -= OnBattleEnded;
        }

        private void OnBattleStarted(int rounds)
        {
            trackedProjectiles.Clear();
            turnTargets.Clear();

            // 开场介绍期间不抢镜头，等序列结束再回常规逻辑
            if (introPlaying) return;

            director?.ShowOverview(true);
        }

        private void OnTurnStarted(Team team)
        {
            if (introPlaying) return;

            turnTargets.Clear();

            Character actor = FindCharacter(team);
            Character opponent = turnController != null ? turnController.GetOpponent(team) : null;

            if (actor != null) turnTargets.Add(actor.transform);
            if (opponent != null && opponent != actor) turnTargets.Add(opponent.transform);

            ApplyTargets(false);
        }

        private void OnShotFired(Character shooter, Projectile projectile)
        {
            if (!followProjectiles || projectile == null) return;
            if (!trackedProjectiles.Contains(projectile)) trackedProjectiles.Add(projectile);
            ApplyTargets(false);
        }

        private void OnProjectileExpired(Projectile projectile, ExpireReason reason)
        {
            if (projectile == null) return;
            if (trackedProjectiles.Remove(projectile)) ApplyTargets(false);
        }

        private void OnCharacterHit(Character character, HitInfo info)
        {
            Shake(hitShakeBase, info.Impulse, hitShakePerImpulse);
        }

        private void OnStrongImpact(Character character, ImpactInfo info)
        {
            Shake(strongImpactShakeBase, info.Impulse, strongImpactShakePerImpulse);
        }

        private void OnCharacterDefeated(Character character)
        {
            director?.AddShake(defeatedShake);
        }

        private void OnBurstTriggered(Projectile projectile, BurstDefinition definition, BurstContext context)
        {
            director?.AddShake(burstShake);
        }

        private void OnBattleEnded(BattleResult result)
        {
            trackedProjectiles.Clear();
            turnTargets.Clear();
            director?.ShowOverview(false);
        }

        private void ApplyTargets(bool snap)
        {
            if (director == null || introPlaying) return;

            var targets = new List<Transform>(turnTargets.Count + trackedProjectiles.Count);
            for (int i = 0; i < turnTargets.Count; i++)
                if (turnTargets[i] != null) targets.Add(turnTargets[i]);

            if (followProjectiles)
            {
                for (int i = trackedProjectiles.Count - 1; i >= 0; i--)
                {
                    Projectile projectile = trackedProjectiles[i];
                    if (projectile == null)
                    {
                        trackedProjectiles.RemoveAt(i);
                        continue;
                    }

                    targets.Add(projectile.transform);
                }
            }

            director.SetTargets(targets, snap);
        }

        private Character FindOpponent(Team team)
        {
            if (turnController != null)
            {
                Character found = turnController.GetOpponent(team);
                if (found != null) return found;
            }

            return FindCharacter(TurnController.OpponentOf(team));
        }

        private Character FindCharacter(Team team)
        {
            if (turnController != null)
            {
                Character found = turnController.GetCharacter(team);
                if (found != null) return found;
            }

            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null && characters[i].Team == team) return characters[i];

            return null;
        }

        private void Shake(float baseAmount, float impulse, float perImpulse)
        {
            director?.AddShake(baseAmount + Mathf.Max(0f, impulse) * perImpulse);
        }
    }
}
