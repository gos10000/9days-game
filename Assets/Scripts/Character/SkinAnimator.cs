using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 皮肤自带的动画驱动：只管表现节奏，不参与任何玩法判定。
    /// 与 Animator 挂在同一个物体上（皮肤 prefab 根），随皮肤实例化 / 销毁。
    ///
    /// 阶段流转：
    ///   按下瞄准 → PreAiming →（片段播完）→ Aiming（循环）
    ///   发射     → Fire     →（片段播完）→ Idle
    ///   被打飞 / 倒地 / 起身 → 强制让出，播同名状态
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SkinAnimator : MonoBehaviour
    {
        [Header("引用（留空则运行时自动查找）")]
        [SerializeField] private Animator animator;
        [SerializeField] private Character character;
        [SerializeField] private CharacterLauncher launcher;

        [Header("状态名（须与 Controller 中的状态同名）")]
        [SerializeField] private string idleState = "Idle";
        [SerializeField] private string preAimState = "PreAiming";
        [SerializeField] private string aimState = "Aiming";
        [SerializeField] private string fireState = "Fire";

        [Header("过渡")]
        [SerializeField] private float crossFadeTime = 0.05f;

        [Header("节奏（0 = 使用动画片段自身长度）")]
        [SerializeField] private float preAimDurationOverride;
        [SerializeField] private float fireDurationOverride;

        [Header("调试")]
        [SerializeField] private bool logPhaseChanges;

        private enum Phase { None, PreAim, Aim, Fire }

        private static readonly int AimPowerParam = Animator.StringToHash("AimPower");

        private Phase phase = Phase.None;
        private float phaseTimer;
        private float phaseDuration;
        private bool ready;

        public CharacterState LogicState => character != null ? character.State : CharacterState.Idle;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (character == null) character = GetComponentInParent<Character>();
            if (launcher == null) launcher = GetComponentInParent<CharacterLauncher>();

            // Prefab 预览模式下没有父级 Character，直接停用，避免刷红字
            ready = animator != null && character != null;
            if (!ready) return;

            if (animator.runtimeAnimatorController == null)
                Debug.LogWarning($"[SkinAnimator] {name} 没有 AnimatorController，动画不会播放", this);
            else if (!HasState(idleState))
                Debug.LogWarning($"[SkinAnimator] Controller 里找不到状态 {idleState}", this);
        }

        private void OnEnable()
        {
            if (!ready) return;
            character.StateChanged += OnLogicState;
            GameEvents.ShotFired += OnShotFired;
        }

        private void OnDisable()
        {
            if (!ready) return;
            character.StateChanged -= OnLogicState;
            GameEvents.ShotFired -= OnShotFired;
        }

        private void Update()
        {
            if (!ready) return;

            // 力度是连续值，独立于阶段机
            if (launcher != null)
                animator.SetFloat(AimPowerParam, Mathf.Clamp01(launcher.AimPower));

            if (phase != Phase.PreAim && phase != Phase.Fire) return;

            phaseTimer += Time.deltaTime;
            if (phaseTimer < phaseDuration) return;

            if (phase == Phase.PreAim) Enter(Phase.Aim);
            else Enter(Phase.None);
        }

        /// <summary>逻辑状态变化：决定表现层该播什么，以及谁可以打断谁。</summary>
        private void OnLogicState(CharacterState state)
        {
            switch (state)
            {
                case CharacterState.Aiming:
                    Enter(Phase.PreAim);
                    break;

                case CharacterState.Idle:
                    // 开火途中不打断，等 Fire 播完自己回 Idle
                    if (phase != Phase.Fire) Enter(Phase.None);
                    break;

                default:
                    // Airborne / Downed / GettingUp 抢占一切
                    Enter(Phase.None);
                    CrossFade(state.ToString(), crossFadeTime);
                    break;
            }
        }

        private void OnShotFired(Character shooter, Projectile projectile)
        {
            if (shooter != character || phase == Phase.Fire) return;
            Enter(Phase.Fire);
        }

        private void Enter(Phase next)
        {
            Phase previous = phase;
            phase = next;
            phaseTimer = 0f;
            phaseDuration = 0f;

            switch (next)
            {
                case Phase.None:
                    CrossFade(idleState, crossFadeTime);
                    break;

                case Phase.PreAim:
                    phaseDuration = preAimDurationOverride > 0f
                        ? preAimDurationOverride
                        : CrossFade(preAimState, crossFadeTime);
                    break;

                case Phase.Aim:
                    CrossFade(aimState, crossFadeTime);
                    break;

                case Phase.Fire:
                    phaseDuration = fireDurationOverride > 0f
                        ? fireDurationOverride
                        : CrossFade(fireState, crossFadeTime);
                    break;
            }

            if (logPhaseChanges && previous != next)
                Debug.Log($"[SkinAnimator] {character.name} {previous} -> {next}", this);
        }

        /// <summary>切换状态并返回该状态的时长；找不到状态返回 0。</summary>
        private float CrossFade(string stateName, float fade)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return 0f;

            if (!HasState(stateName))
            {
                Debug.LogWarning($"[SkinAnimator] Controller 里找不到状态 {stateName}", this);
                return 0f;
            }

            if (fade <= 0f) animator.Play(stateName, 0, 0f);
            else animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);

            return GetClipLength(stateName);
        }

        private bool HasState(string stateName)
        {
            return animator != null && animator.HasState(0, Animator.StringToHash(stateName));
        }

        /// <summary>
        /// 从 Controller 的 clip 列表里取长度。
        /// 不能用 GetCurrentAnimatorStateInfo：CrossFade 当帧读到的还是上一个状态的时长。
        /// </summary>
        private float GetClipLength(string stateName)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips
                : null;

            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null && clips[i].name == stateName)
                        return clips[i].length;
                }
            }

            // 状态名和 clip 名不一致时的兜底，避免卡在 PreAim / Fire 出不来
            return 0.2f;
        }
    }
}
