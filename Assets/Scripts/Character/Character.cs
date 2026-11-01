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
    public class Character : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private CharacterConfig config;
        [SerializeField] private Team team = Team.Player1;

        [Header("引用")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private CharacterCombat combat;
        [SerializeField] private CharacterVisual visual;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform hitPoint;

        [Header("接地")]
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private float groundCheckRadius = 0.12f;

        private readonly List<Collider2D> groundHits = new List<Collider2D>();
        private ContactFilter2D groundFilter;
        private Vector3 spawnPoint;
        private float controlLockTimer;
        private float settleTimer;
        private float stateTimer;
        private bool turnPermission;
        private bool isGrounded;

        public CharacterConfig Config => config;
        public Team Team => team;
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
                    if (isGrounded && Mathf.Abs(Velocity.y) <= LandedSpeed) EnterDowned();
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
            SetState(CharacterState.Airborne);
        }

        public void EnterDowned()
        {
            stateTimer = 0f;
            SetState(CharacterState.Downed);
            GameEvents.RaiseCharacterDowned(this);
        }

        public void LockControl(float seconds)
        {
            controlLockTimer = Mathf.Max(controlLockTimer, seconds);
        }

        /// <summary>被命中的统一入口（CharacterCombat 负责具体处理）。</summary>
        public void ReceiveHit(in HitInfo info)
        {
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
