using UnityEngine;
using UnityEngine.EventSystems;

namespace NineDays
{
    /// <summary>战斗输入适配层：只把输入转发给当前行动角色，不参与回合推进。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleInputController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private TurnController turnController;

        [Header("开关")]
        [Tooltip("关闭后立即取消当前瞄准，可用于暂停或 UI 接管")]
        [SerializeField] private bool enableGameplayInput = true;

        private Character cachedActor;
        private CharacterLauncher activeLauncher;
        private int suppressPressFrame = -1;

        public bool InputEnabled
        {
            get => enableGameplayInput;
            set
            {
                // 关闭 → 打开的那一帧，把同一次点击吃掉：
                // 否则「放置道具 / 点选卡片」的按下事件会在解锁后立刻被当成开始瞄准。
                if (value && !enableGameplayInput) suppressPressFrame = Time.frameCount;
                enableGameplayInput = value;
            }
        }

        private void Awake()
        {
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
        }

        private void OnDisable()
        {
            CancelActiveAim();
        }

        private void Update()
        {
            // 暂停中不接受任何战斗输入：否则点「停止」那一帧会顺手开镜，松手还会把弹投出去
            if (GamePause.IsPaused)
            {
                CancelActiveAim();
                return;
            }

            if (!enableGameplayInput)
            {
                CancelActiveAim();
                return;
            }

            CharacterLauncher launcher = ResolveActiveLauncher();
            if (launcher == null)
            {
                CancelActiveAim();
                return;
            }

            InputSnapshot input = GameInput.Current;
            Vector2 pointer = GameInput.PointerWorld(launcher.TargetCamera);

            if (!launcher.IsAiming)
            {
                if (!input.PrimaryPressed) return;
                if (Time.frameCount <= suppressPressFrame) return;

                // 点在 UI 上（比如角落的停止键）不开镜；已经开镜时不受影响
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                activeLauncher = launcher;
                launcher.BeginAim(pointer);
                launcher.UpdateAim(pointer);
                return;
            }

            if (input.PrimaryHeld) launcher.UpdateAim(pointer);

            if (input.PrimaryReleased) launcher.ReleaseAim(pointer);
            else if (input.SecondaryPressed || GameInput.ConsumeCancel()) launcher.CancelAim();
        }

        /// <summary>找到当前回合、且可操作角色的 Launcher；换人或结算中返回 null。</summary>
        private CharacterLauncher ResolveActiveLauncher()
        {
            if (turnController == null || turnController.IsResolving) return null;

            Team team = turnController.CurrentTeam;
            if (team == Team.None) return null;

            Character actor = turnController.GetCharacter(team);
            if (actor == null || !actor.CanAct) return null;

            if (actor != cachedActor)
            {
                CancelActiveAim();
                cachedActor = actor;
                activeLauncher = actor.GetComponent<CharacterLauncher>();
            }

            return activeLauncher;
        }

        private void CancelActiveAim()
        {
            if (activeLauncher != null && activeLauncher.IsAiming) activeLauncher.CancelAim();

            activeLauncher = null;
            cachedActor = null;
        }
    }
}
