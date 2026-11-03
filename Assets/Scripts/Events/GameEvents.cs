using System;
using UnityEngine;

namespace NineDays
{
    /// <summary>阵营。双人对战只有两个玩家，None 预留给中立物体。</summary>
    public enum Team { None = 0, Player1 = 1, Player2 = 2 }

    /// <summary>命中来源类型。</summary>
    public enum HitKind { Unknown = 0, Projectile = 1, Obstacle = 2, Character = 3, Burst = 4 }

    /// <summary>被撞到的对象类型。</summary>
    public enum ImpactKind { Obstacle = 0, Character = 1, Projectile = 2 }

    /// <summary>角色状态。Animator 的 State 参数使用同样的整数值。</summary>
    public enum CharacterState { Idle = 0, Aiming = 1, Airborne = 2, Downed = 3, GettingUp = 4, Defeated = 5 }

    /// <summary>Animator 参数契约：任何皮肤控制器都必须实现这 5 个参数。</summary>
    public static class AnimParams
    {
        public static readonly int State = Animator.StringToHash("State");
        public static readonly int Grounded = Animator.StringToHash("Grounded");
        public static readonly int AimPower = Animator.StringToHash("AimPower");
        public static readonly int Hit = Animator.StringToHash("Hit");
        public static readonly int GetUp = Animator.StringToHash("GetUp");
    }

    /// <summary>角色被命中时的完整信息（"被什么击中"）。</summary>
    public readonly struct HitInfo
    {
        public readonly GameObject Source;
        public readonly GameObject Attacker;
        public readonly Team SourceTeam;
        public readonly HitKind Kind;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly float Impulse;
        public readonly float Speed;
        public readonly float Damage;

        public HitInfo(GameObject source, GameObject attacker, Team sourceTeam, HitKind kind,
                       Vector2 point, Vector2 normal, float impulse, float speed, float damage)
        {
            Source = source;
            Attacker = attacker;
            SourceTeam = sourceTeam;
            Kind = kind;
            Point = point;
            Normal = normal;
            Impulse = impulse;
            Speed = speed;
            Damage = damage;
        }

        public string SourceName => Source != null ? Source.name : "Unknown";
        public string AttackerName => Attacker != null ? Attacker.name : "-";

        public override string ToString()
        {
            return $"Hit({Kind} from {SourceName} owner={AttackerName} team={SourceTeam} " +
                   $"impulse={Impulse:0.00} speed={Speed:0.0} dmg={Damage:0.0})";
        }
    }

    /// <summary>角色撞到东西时的冲量信息（"撞击力度"）。</summary>
    public readonly struct ImpactInfo
    {
        public readonly Collider2D Other;
        public readonly ImpactKind Kind;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly float Impulse;
        public readonly float Speed;
        public readonly float Force;
        public readonly bool AboveThreshold;

        public ImpactInfo(Collider2D other, ImpactKind kind, Vector2 point, Vector2 normal,
                          float impulse, float speed, float force, bool aboveThreshold)
        {
            Other = other;
            Kind = kind;
            Point = point;
            Normal = normal;
            Impulse = impulse;
            Speed = speed;
            Force = force;
            AboveThreshold = aboveThreshold;
        }

        public string OtherName => Other != null ? Other.name : "Unknown";

        public override string ToString()
        {
            string mark = AboveThreshold ? "OK" : "low";
            return $"Impact({Kind} {OtherName} impulse={Impulse:0.00} force={Force:0.0} " +
                   $"speed={Speed:0.0} {mark})";
        }
    }

    /// <summary>一次行动的结果（战斗会话用于推进与判定）。</summary>
    public readonly struct TurnResult
    {
        public readonly Team Actor;
        public readonly bool ShotFired;
        public readonly bool HitLanded;
        public readonly float DamageDealt;
        public readonly bool TargetDefeated;

        public TurnResult(Team actor, bool shotFired, bool hitLanded, float damageDealt, bool targetDefeated)
        {
            Actor = actor;
            ShotFired = shotFired;
            HitLanded = hitLanded;
            DamageDealt = damageDealt;
            TargetDefeated = targetDefeated;
        }

        public static TurnResult Empty(Team actor) => new TurnResult(actor, false, false, 0f, false);
    }

    /// <summary>一场战斗结束时的结算数据。</summary>
    public readonly struct BattleResult
    {
        public readonly Team Winner;
        public readonly int RoundsPlayed;
        public readonly float HealthA;
        public readonly float HealthB;

        public BattleResult(Team winner, int roundsPlayed, float healthA, float healthB)
        {
            Winner = winner;
            RoundsPlayed = roundsPlayed;
            HealthA = healthA;
            HealthB = healthB;
        }
    }

    /// <summary>
    /// 全局事件总线（文档「事件单独脚本定义」）。
    /// 模块之间只通过这里通信；订阅必须成对（OnEnable / OnDisable）。
    /// </summary>
    public static class GameEvents
    {
        /// <summary>角色被命中（子弹、角色、障碍都算）。</summary>
        public static event Action<Character, HitInfo> CharacterHit;

        /// <summary>角色被击飞（冲量达到 hitThreshold）。</summary>
        public static event Action<Character, HitInfo> CharacterKnockedBack;

        /// <summary>角色与障碍碰撞（每次都触发，用 AboveThreshold 区分是否过阈值）。</summary>
        public static event Action<Character, ImpactInfo> ImpactDetected;

        /// <summary>角色与障碍碰撞且冲量超过阈值。</summary>
        public static event Action<Character, ImpactInfo> StrongImpact;

        /// <summary>角色进入倒地状态。</summary>
        public static event Action<Character> CharacterDowned;

        /// <summary>角色起身完成。</summary>
        public static event Action<Character> CharacterRecovered;

        /// <summary>角色被打出场地边界。</summary>
        public static event Action<Character> CharacterOutOfBounds;

        /// <summary>某一方发射了子弹。</summary>
        public static event Action<Character, Projectile> ShotFired;

        /// <summary>子弹命中角色（伤害已结算）。</summary>
        public static event Action<Character, Projectile, HitInfo> ProjectileHitLanded;

        /// <summary>子弹消失（命中 / 寿命 / 中止）。</summary>
        public static event Action<Projectile, ExpireReason> ProjectileExpired;

        /// <summary>一次爆发成功触发；表现层以后订阅它接动画/特效/音效。</summary>
        public static event Action<Projectile, BurstDefinition, BurstContext> BurstTriggered;

        /// <summary>爆发中的单个效果执行完成；用于效果级表现/统计。</summary>
        public static event Action<Projectile, BurstEffect, BurstContext> BurstEffectExecuted;

        /// <summary>回合开始。</summary>
        public static event Action<Team> TurnStarted;

        /// <summary>回合结束。</summary>
        public static event Action<Team> TurnEnded;

        /// <summary>战斗开始（参数为总轮数）。</summary>
        public static event Action<int> BattleStarted;

        /// <summary>一轮开始（轮次、先手方）。</summary>
        public static event Action<int, Team> RoundStarted;

        /// <summary>一轮结束。</summary>
        public static event Action<int> RoundEnded;

        /// <summary>一次行动结算完成。</summary>
        public static event Action<TurnResult> TurnResolved;

        /// <summary>追加了一次额外行动（阵营、是否插队）。</summary>
        public static event Action<Team, bool> ExtraTurnGranted;

        /// <summary>角色血量变化。</summary>
        public static event Action<Character, float, float> CharacterHealthChanged;

        /// <summary>角色被击败。</summary>
        public static event Action<Character> CharacterDefeated;

        /// <summary>战斗结束。</summary>
        public static event Action<BattleResult> BattleEnded;

        /// <summary>地形搭建完成。</summary>
        public static event Action<ArenaController> TerrainBuilt;

        /// <summary>地形被抹除。</summary>
        public static event Action<TerrainBreakRequest, TerrainBreakResult> TerrainBroken;

        /// <summary>地形被追加。</summary>
        public static event Action<string, int> TerrainAppended;

        public static void RaiseCharacterHit(Character character, in HitInfo info) => CharacterHit?.Invoke(character, info);
        public static void RaiseCharacterKnockedBack(Character character, in HitInfo info) => CharacterKnockedBack?.Invoke(character, info);
        public static void RaiseImpactDetected(Character character, in ImpactInfo info) => ImpactDetected?.Invoke(character, info);
        public static void RaiseStrongImpact(Character character, in ImpactInfo info) => StrongImpact?.Invoke(character, info);
        public static void RaiseCharacterDowned(Character character) => CharacterDowned?.Invoke(character);
        public static void RaiseCharacterRecovered(Character character) => CharacterRecovered?.Invoke(character);
        public static void RaiseCharacterOutOfBounds(Character character) => CharacterOutOfBounds?.Invoke(character);
        public static void RaiseShotFired(Character shooter, Projectile projectile) => ShotFired?.Invoke(shooter, projectile);
        public static void RaiseProjectileHitLanded(Character target, Projectile projectile, in HitInfo info) => ProjectileHitLanded?.Invoke(target, projectile, info);
        public static void RaiseProjectileExpired(Projectile projectile, ExpireReason reason) => ProjectileExpired?.Invoke(projectile, reason);
        public static void RaiseBurstTriggered(Projectile projectile, BurstDefinition definition, in BurstContext context) => BurstTriggered?.Invoke(projectile, definition, context);
        public static void RaiseBurstEffectExecuted(Projectile projectile, BurstEffect effect, in BurstContext context) => BurstEffectExecuted?.Invoke(projectile, effect, context);
        public static void RaiseTurnStarted(Team team) => TurnStarted?.Invoke(team);
        public static void RaiseTurnEnded(Team team) => TurnEnded?.Invoke(team);
        public static void RaiseBattleStarted(int rounds) => BattleStarted?.Invoke(rounds);
        public static void RaiseRoundStarted(int round, Team firstActor) => RoundStarted?.Invoke(round, firstActor);
        public static void RaiseRoundEnded(int round) => RoundEnded?.Invoke(round);
        public static void RaiseTurnResolved(in TurnResult result) => TurnResolved?.Invoke(result);
        public static void RaiseExtraTurnGranted(Team team, bool immediate) => ExtraTurnGranted?.Invoke(team, immediate);
        public static void RaiseCharacterHealthChanged(Character character, float current, float max) => CharacterHealthChanged?.Invoke(character, current, max);
        public static void RaiseCharacterDefeated(Character character) => CharacterDefeated?.Invoke(character);
        public static void RaiseBattleEnded(in BattleResult result) => BattleEnded?.Invoke(result);
        public static void RaiseTerrainBuilt(ArenaController arena) => TerrainBuilt?.Invoke(arena);
        public static void RaiseTerrainBroken(in TerrainBreakRequest request, in TerrainBreakResult result) => TerrainBroken?.Invoke(request, result);
        public static void RaiseTerrainAppended(string layerId, int pixels) => TerrainAppended?.Invoke(layerId, pixels);

        /// <summary>退出 Play / 关闭 Domain Reload 时清空静态订阅，避免事件翻倍。</summary>
        public static void ClearAll()
        {
            CharacterHit = null;
            CharacterKnockedBack = null;
            ImpactDetected = null;
            StrongImpact = null;
            CharacterDowned = null;
            CharacterRecovered = null;
            CharacterOutOfBounds = null;
            ShotFired = null;
            ProjectileHitLanded = null;
            ProjectileExpired = null;
            BurstTriggered = null;
            BurstEffectExecuted = null;
            TurnStarted = null;
            TurnEnded = null;
            BattleStarted = null;
            RoundStarted = null;
            RoundEnded = null;
            TurnResolved = null;
            ExtraTurnGranted = null;
            CharacterHealthChanged = null;
            CharacterDefeated = null;
            BattleEnded = null;
            TerrainBuilt = null;
            TerrainBroken = null;
            TerrainAppended = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => ClearAll();
    }
}
