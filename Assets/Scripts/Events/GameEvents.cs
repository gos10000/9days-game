using System;
using UnityEngine;

namespace NineDays
{
    /// <summary>阵营。双人对战只有两个玩家，None 预留给中立物体。</summary>
    public enum Team { None = 0, Player1 = 1, Player2 = 2 }

    /// <summary>命中来源类型。</summary>
    public enum HitKind { Unknown = 0, Projectile = 1, Obstacle = 2, Character = 3 }

    /// <summary>被撞到的对象类型。</summary>
    public enum ImpactKind { Obstacle = 0, Character = 1, Projectile = 2 }

    /// <summary>角色状态。Animator 的 State 参数使用同样的整数值。</summary>
    public enum CharacterState { Idle = 0, Aiming = 1, Airborne = 2, Downed = 3, GettingUp = 4 }

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

        /// <summary>回合开始。</summary>
        public static event Action<Team> TurnStarted;

        /// <summary>回合结束。</summary>
        public static event Action<Team> TurnEnded;

        public static void RaiseCharacterHit(Character character, in HitInfo info) => CharacterHit?.Invoke(character, info);
        public static void RaiseCharacterKnockedBack(Character character, in HitInfo info) => CharacterKnockedBack?.Invoke(character, info);
        public static void RaiseImpactDetected(Character character, in ImpactInfo info) => ImpactDetected?.Invoke(character, info);
        public static void RaiseStrongImpact(Character character, in ImpactInfo info) => StrongImpact?.Invoke(character, info);
        public static void RaiseCharacterDowned(Character character) => CharacterDowned?.Invoke(character);
        public static void RaiseCharacterRecovered(Character character) => CharacterRecovered?.Invoke(character);
        public static void RaiseCharacterOutOfBounds(Character character) => CharacterOutOfBounds?.Invoke(character);
        public static void RaiseShotFired(Character shooter, Projectile projectile) => ShotFired?.Invoke(shooter, projectile);
        public static void RaiseTurnStarted(Team team) => TurnStarted?.Invoke(team);
        public static void RaiseTurnEnded(Team team) => TurnEnded?.Invoke(team);

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
            TurnStarted = null;
            TurnEnded = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => ClearAll();
    }
}
