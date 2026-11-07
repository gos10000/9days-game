using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 开始界面专用输入：直接驱动一个 CharacterLauncher，不依赖回合系统。
    /// 战斗场景请用 BattleInputController（它按回合只把输入给当前行动方）；
    /// 本组件用于没有回合推进、只有「拖着扔一颗石头」的场景，比如开始界面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartSceneThrowInput : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("留空则自动查找场景里的第一个 CharacterLauncher")]
        [SerializeField] private CharacterLauncher launcher;

        [Header("权限")]
        [Tooltip("开始界面没有回合控制器，需要这里把发射权授给角色，否则 CanAct 恒为 false")]
        [SerializeField] private bool grantControlOnEnable = true;

        private Character character;

        private void Awake()
        {
            ResolveLauncher();
        }

        private void OnEnable()
        {
            ResolveLauncher();
            if (grantControlOnEnable && character != null) character.SetCanAct(true);
        }

        private void Update()
        {
            if (launcher == null) return;

            if (GamePause.IsPaused)
            {
                if (launcher.IsAiming) launcher.CancelAim();
                return;
            }

            // 兜底：没有回合系统时，只要不在瞄准中就把操作权保持住
            if (grantControlOnEnable && character != null && !launcher.IsAiming && !character.CanAct)
                character.SetCanAct(true);

            InputSnapshot input = GameInput.Current;
            Vector2 pointer = GameInput.PointerWorld(launcher.TargetCamera);

            if (!launcher.IsAiming)
            {
                if (!input.PrimaryPressed) return;

                launcher.BeginAim(pointer);
                launcher.UpdateAim(pointer);
                return;
            }

            if (input.PrimaryHeld) launcher.UpdateAim(pointer);

            if (input.PrimaryReleased) launcher.ReleaseAim(pointer);
            else if (input.SecondaryPressed || GameInput.ConsumeCancel()) launcher.CancelAim();
        }

        private void ResolveLauncher()
        {
            if (launcher == null) launcher = FindAnyObjectByType<CharacterLauncher>();
            if (launcher == null) return;

            character = launcher.GetComponent<Character>();
        }
    }
}
