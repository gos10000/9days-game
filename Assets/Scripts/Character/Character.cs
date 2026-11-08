using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 角色主体：状态机 + 队伍 + 接地检测 + 对外 API。
    /// 挂在角色 prefab 根节点（同节点还有 Rigidbody2D / CapsuleCollider2D）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Character : MonoBehaviour, IBattleParticipant
    {
        [Header("配置")]
        [SerializeField] private CharacterConfig config;
        [SerializeField] private Team team = Team.Player1;

        [Header("引用")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private CharacterCombat combat;
        [SerializeField] private CharacterVisual visual;
        [SerializeField] private CharacterHealth health;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform hitPoint;

        [Header("接地")]
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private float groundCheckRadius = 0.12f;

        [Header("击飞兜底")]
        [Tooltip("击飞状态超过这个时长且几乎不动（速度低于 settleSpeed）视为卡住，强制走倒地流程")]
        [Min(0.1f)]
        [SerializeField] private float wedgedTimeout = 1.2f;

        [Tooltip("击飞状态最长持续时间；超过就送回出生点。防止掉出地形/卡在缝里永远不落地")]
        [Min(0.5f)]
        [SerializeField] private float airborneTimeout = 5f;

        private readonly List<Collider2D> groundHits = new List<Collider2D>();
        private ContactFilter2D groundFilter;
        private Vector3 spawnPoint;
        private float controlLockTimer;
        private float settleTimer;
        private float stateTimer;
        private float airborneTimer;
        private bool turnPermission;
        private bool isGrounded;

        public CharacterConfig Config => config;
        public Team Team => team;

        /// <summary>血量组件（惰性解析，兼容运行时动态添加组件）。</summary>
        public CharacterHealth Health
        {
            get
            {
                if (health == null) health = GetComponent<CharacterHealth>();
                return health;
            }
        }

        public float CurrentHealth => Health != null ? Health.Current : 0f;
        public bool IsDefeated => Health != null && Health.IsDefeated;
        public CharacterState State { get; private set; } = CharacterState.Idle;
        public Rigidbody2D Body => body;
        public Transform AimPivot => aimPivot != null ? aimPivot : transform;
        public Transform HitPoint => hitPoint != null ? hitPoint : transform;
        public Vector3 SpawnPoint => spawnPoint;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool IsGrounded => isGrounded;
        public bool HasControlLock => controlLockTimer > 0f;

        public bool CanAct =>
            turnPermission && controlLockTimer <= 0f &&
            (State == CharacterState.Idle || State == CharacterState.Aiming);

        public bool IsSettled => isGrounded && settleTimer >= SettleTime;

        public event Action<CharacterState> StateChanged;

        private float SettleSpeed => config != null ? config.settleSpeed : 0.15f;
        private float SettleTime => config != null ? config.settleTime : 0.3f;
        private float GetUpDelay => config != null ? config.getUpDelay : 0.5f;
        private float GetUpDuration => config != null ? config.getUpDuration : 0.4f;
        private float LandedSpeed => config != null ? config.landedSpeed : 1f;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (combat == null) combat = GetComponent<CharacterCombat>();
            if (visual == null) visual = GetComponentInChildren<CharacterVisual>(true);
            if (health == null) health = GetComponent<CharacterHealth>();

            spawnPoint = transform.position;
            ApplyConfig();
            RebuildGroundFilter();
        }

        private void Update()
        {
            UpdateGrounded();
            UpdateControlLock();
            UpdateSettle();
            UpdateStateMachine();
        }

        private void ApplyConfig()
        {
            if (config == null) return;
            if (body != null)
            {
                body.mass = Mathf.Max(0.01f, config.mass);
                body.freezeRotation = config.freezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            if (bodyCollider is CapsuleCollider2D capsule) capsule.size = config.colliderSize;
            if (config.groundCheckRadius > 0f) groundCheckRadius = config.groundCheckRadius;
        }

        private void RebuildGroundFilter()
        {
            groundFilter = new ContactFilter2D();
            groundFilter.useLayerMask = true;
            groundFilter.layerMask = groundMask;
            groundFilter.useTriggers = false;
        }

        private void UpdateGrounded()
        {
            Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
            groundHits.Clear();
            int count = Physics2D.OverlapCircle(origin, groundCheckRadius, groundFilter, groundHits);
            isGrounded = false;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = groundHits[i];
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                isGrounded = true;
                break;
            }
        }

        private void UpdateControlLock()
        {
            if (controlLockTimer > 0f) controlLockTimer = Mathf.Max(0f, controlLockTimer - Time.deltaTime);
        }

        private void UpdateSettle()
        {
            bool still = isGrounded && Velocity.magnitude <= SettleSpeed;
            settleTimer = still ? settleTimer + Time.deltaTime : 0f;
        }

        private void UpdateStateMachine()
        {
            switch (State)
            {
                case CharacterState.Aiming:
                    if (!turnPermission) CancelAim();
                    break;

                case CharacterState.Airborne:
                    airborneTimer += Time.deltaTime;

                    if (isGrounded && Mathf.Abs(Velocity.y) <= LandedSpeed)
                    {
                        EnterDowned();
                        break;
                    }

                    // 兜底 1：明显卡在几何/缝里——几乎不动，却一直判定不到地面。
                    // 没有这一条时，被击退后落在被挖掉的地形边缘或轮廓缝隙里会永久停在 Airborne，
                    // 动画定格在 HeaveHit，回合也因为 IsSettled 一直 false 而推不下去。
                    if (airborneTimer >= wedgedTimeout && Velocity.magnitude <= SettleSpeed)
                    {
                        Debug.LogWarning($"[Character] {name} 击飞 {airborneTimer:F2}s 仍未落地但几乎静止（{Velocity.magnitude:F3}），强制结束击飞", this);
                        EnterDowned();
                        break;
                    }

                    // 兜底 2：长时间不落地（掉进坑里 / 掉出地形）
                    if (airborneTimer >= airborneTimeout)
                    {
                        Debug.LogWarning($"[Character] {name} 击飞超时 {airborneTimeout:F1}s，送回出生点", this);
                        ResetToSpawn();
                    }
                    break;

                case CharacterState.Downed:
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= GetUpDelay)
                    {
                        stateTimer = 0f;
                        SetState(CharacterState.GettingUp);
                    }
                    break;

                case CharacterState.GettingUp:
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= GetUpDuration)
                    {
                        SetState(CharacterState.Idle);
                        GameEvents.RaiseCharacterRecovered(this);
                    }
                    break;
            }
        }

        /// <summary>回合切换时由 TurnController 调用。</summary>
        public void SetCanAct(bool value)
        {
            turnPermission = value;
            if (!value) CancelAim();
        }

        public void BeginAim()
        {
            if (!CanAct) return;
            SetState(CharacterState.Aiming);
        }

        public void CancelAim()
        {
            if (State == CharacterState.Aiming) SetState(CharacterState.Idle);
        }

        public void EnterAirborne()
        {
            stateTimer = 0f;
            airborneTimer = 0f;
            SetState(CharacterState.Airborne);
        }

        public void EnterDowned()
        {
            stateTimer = 0f;
            SetState(CharacterState.Downed);
            GameEvents.RaiseCharacterDowned(this);
        }

        /// <summary>血量归零：进入不可操作的战败状态。</summary>
        public void EnterDefeated()
        {
            controlLockTimer = 0f;
            SetState(CharacterState.Defeated);
        }

        /// <summary>设置战斗出生点；默认只写值，由 ResetForBattle 真正移动。</summary>
        public void SetSpawnPoint(Vector3 point, bool moveImmediately = false)
        {
            spawnPoint = point;
            if (!moveImmediately || body == null) return;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = new Vector2(point.x, point.y);
            body.rotation = 0f;
        }

        /// <summary>战斗开始时重置：回满血、回出生点、清状态并交出操作权。</summary>
        public void ResetForBattle()
        {
            if (health != null) health.ResetHealth();

            controlLockTimer = 0f;
            settleTimer = 0f;
            stateTimer = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = spawnPoint;
                body.rotation = 0f;
            }

            SetState(CharacterState.Idle);
            SetCanAct(false);
        }

        public void LockControl(float seconds)
        {
            controlLockTimer = Mathf.Max(controlLockTimer, seconds);
        }

        /// <summary>被命中的统一入口（CharacterCombat 负责具体处理）。</summary>
        public void ReceiveHit(in HitInfo info)
        {
            if (combat == null) combat = GetComponent<CharacterCombat>();
            if (combat != null) combat.ReceiveHit(info);
        }

        public void FaceTo(Vector2 worldPoint)
        {
            if (visual != null) visual.FaceTo(worldPoint - (Vector2)transform.position);
        }

        public void Flash()
        {
            if (visual != null) visual.Flash();
        }

        public void SetAimPower(float power)
        {
            if (visual != null) visual.SetAimPower(power);
        }

        /// <summary>回到出生点：清速度、进入倒地→起身流程（用于出界兜底 / 重开）。</summary>
        public void ResetToSpawn()
        {
            controlLockTimer = 0f;
            settleTimer = 0f;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = spawnPoint;
                body.rotation = 0f;
            }
            EnterDowned();
        }

        public void SetGroundMask(LayerMask mask)
        {
            groundMask = mask;
            RebuildGroundFilter();
        }

        private void SetState(CharacterState next)
        {
            if (State == next) return;
            State = next;
            stateTimer = 0f;
            StateChanged?.Invoke(next);
        }
    }
}
