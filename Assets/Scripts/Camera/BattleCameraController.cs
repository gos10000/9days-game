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

        public CameraDirector Director => director;
        public bool FollowProjectiles => followProjectiles;

        private void Awake()
        {
            if (director == null) director = GetComponent<CameraDirector>();
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            trackedProjectiles.Clear();
            turnTargets.Clear();
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
            director?.ShowOverview(true);
        }

        private void OnTurnStarted(Team team)
        {
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
            if (director == null) return;

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
