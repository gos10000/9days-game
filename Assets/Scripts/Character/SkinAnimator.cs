using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 皮肤自带的动画驱动：只管表现节奏，不参与任何玩法判定。
    /// 与 Animator 挂在同一个物体上（皮肤 prefab 根），随皮肤实例化 / 销毁。
    ///
        /// 阶段流转：
        ///   按下瞄准 → PreAiming →（片段播完）→ Aiming（循环）
        ///   发射     → Fire     →（片段播完）→ Idle / Aiming
        ///   轻受击   → LightHit →（片段播完）→ Idle / Aiming
        ///   重受击   → 由 CharacterState.Airborne 映射到 HeaveHit，倒地 / 起身同理由 Inspector 指定
        ///   死亡     → Death →（片段播完）→ Died（循环保持）
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

        [Header("受击状态名")]
        [Tooltip("冲量未达到 hitThreshold 的轻受击")]
        [SerializeField] private string lightHitState = "LightHIt";

        [Tooltip("冲量达到 hitThreshold 被击飞时播的状态。\nController 里没有 Airborne 状态，所以这里填击飞动画 HeaveHit")]
        [SerializeField] private string airborneState = "HeaveHit";

        [Tooltip("倒地 / 起身：Controller 里没有对应状态就留空，保持上一帧表现不动")]
        [SerializeField] private string downedState = "";
        [SerializeField] private string gettingUpState = "";

        [Header("死亡状态名")]
        [Tooltip("血量归零瞬间的死亡过程动画（不循环）")]
        [SerializeField] private string deathState = "Death";

        [Tooltip("死亡过程播完后保持的姿势（循环）。留空表示没有，播完停在最后一帧")]
        [SerializeField] private string diedState = "Died";

        [Header("过渡")]
        [SerializeField] private float crossFadeTime = 0.05f;

        [Header("节奏（0 = 使用动画片段自身长度）")]
        [SerializeField] private float preAimDurationOverride;
        [SerializeField] private float fireDurationOverride;
        [SerializeField] private float lightHitDurationOverride;
        [SerializeField] private float deathDurationOverride;

        [Header("调试")]
        [SerializeField] private bool logPhaseChanges;

        private enum Phase { None, PreAim, Aim, Fire, LightHit, Dying, Dead }

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
            GameEvents.CharacterHit += OnCharacterHit;
        }

        private void OnDisable()
        {
            if (!ready) return;
            character.StateChanged -= OnLogicState;
            GameEvents.ShotFired -= OnShotFired;
            GameEvents.CharacterHit -= OnCharacterHit;
        }

        private void Update()
        {
            if (!ready) return;

            // 力度是连续值，独立于阶段机
            if (launcher != null)
                animator.SetFloat(AimPowerParam, Mathf.Clamp01(launcher.AimPower));

            // 只有这四个阶段会自己结束并回到下一阶段
            if (phase != Phase.PreAim && phase != Phase.Fire && phase != Phase.LightHit && phase != Phase.Dying) return;

            phaseTimer += Time.deltaTime;
            if (phaseTimer < phaseDuration) return;

            if (phase == Phase.PreAim) Enter(Phase.Aim);
            else if (phase == Phase.Dying)
            {
                // 死亡过程播完：落到循环的保持姿势，从此停在 Phase.Dead，
                // 直到 ResetForBattle 把逻辑状态拉回 Idle 才会退出
                CrossFade(diedState, crossFadeTime);
                Enter(Phase.Dead);
            }
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

                case CharacterState.Defeated:
                    // 终态：播死亡过程，播完后由 Update 落到 Died 保持
                    Enter(Phase.Dying);
                    break;

                default:
                    // Airborne / Downed / GettingUp 抢占一切
                    Enter(Phase.None);
                    CrossFade(ResolveStateName(state), crossFadeTime);
                    break;
            }
        }

        /// <summary>
        /// 受击表现分流：
        ///   冲量 &lt; hitThreshold → 播 LightHit（逻辑状态不变，纯表现）
        ///   冲量 ≥ hitThreshold → 交给状态机，Airborne 时由 ResolveStateName 映射到 HeaveHit
        /// 阈值直接取 Config.hitThreshold，和 CharacterCombat 用同一个来源，不会不同步。
        /// </summary>
        private void OnCharacterHit(Character target, HitInfo info)
        {
            if (target != character) return;

            float threshold = character.Config != null ? character.Config.hitThreshold : 0.5f;
            if (info.Impulse >= threshold) return;   // 会被击飞，走 LightHit 之外的路径

            // 已经在飞 / 倒地 / 起身 / 战败时不播轻受击，避免打断击飞动作
            CharacterState state = character.State;
            if (state == CharacterState.Airborne || state == CharacterState.Downed ||
                state == CharacterState.GettingUp || state == CharacterState.Defeated)
                return;

            Enter(Phase.LightHit);
        }

        private void OnShotFired(Character shooter, Projectile projectile)
        {
            if (shooter != character || phase == Phase.Fire) return;
            Enter(Phase.Fire);
        }

        /// <summary>逻辑状态 → Controller 里的状态名。返回空串表示这个逻辑状态没有对应动画。</summary>
        private string ResolveStateName(CharacterState state)
        {
            switch (state)
            {
                case CharacterState.Airborne: return airborneState;
                case CharacterState.Downed: return downedState;
                case CharacterState.GettingUp: return gettingUpState;
                default: return string.Empty;
            }
        }

        /// <summary>阶段结束后该回到哪个循环动画。返回空串表示交给别的分支处理，不要动。</summary>
        private string ResolveIdleState()
        {
            switch (character.State)
            {
                case CharacterState.Idle: return idleState;
                case CharacterState.Aiming: return aimState;
                // Airborne / Downed / GettingUp 由 OnLogicState 的 default 分支单独接管，
                // 这里切回 Idle 会闪一帧待机，所以返回空串跳过
                default: return string.Empty;
            }
        }

        private void Enter(Phase next)
        {
            // 没有轻受击状态就别切，免得卡在半路
            if (next == Phase.LightHit && !HasState(lightHitState)) return;

            Phase previous = phase;
            phase = next;
            phaseTimer = 0f;
            phaseDuration = 0f;

            switch (next)
            {
                case Phase.None:
                    CrossFade(ResolveIdleState(), crossFadeTime);
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

                case Phase.LightHit:
                    phaseDuration = lightHitDurationOverride > 0f
                        ? lightHitDurationOverride
                        : CrossFade(lightHitState, crossFadeTime);
                    break;

                case Phase.Dying:
                    // Controller 里没有 Death 状态时 duration 为 0，下一帧直接落到 Died
                    phaseDuration = deathDurationOverride > 0f
                        ? deathDurationOverride
                        : CrossFade(deathState, crossFadeTime);
                    break;

                // Phase.Dead：什么都不做，保持 Died 循环
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
