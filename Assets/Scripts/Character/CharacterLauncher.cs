using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 拖拽瞄准 + 发射（两人轮流用鼠标）。只有当前回合角色（CanAct）才能操作。
    /// 瞄准方向 = 从鼠标指向角色的反方向（拉弓手感），力度 = 拖拽距离。
    /// </summary>
    public class CharacterLauncher : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Character character;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private Camera targetCamera;

        [Header("瞄准")]
        [Tooltip("最大拖拽距离，达到即满力")]
        [SerializeField] private float maxDragDistance = 3f;

        [Tooltip("最小发射速度")]
        [SerializeField] private float minLaunchSpeed = 4f;

        [Tooltip("最大发射速度")]
        [SerializeField] private float maxLaunchSpeed = 15f;

        [Tooltip("瞄准线满力时的长度")]
        [SerializeField] private float aimLineLength = 3f;

        private bool aiming;
        private Vector2 dragOrigin;
        private Vector2 pointerWorld;
        private float aimPower;

        public bool IsAiming => aiming;
        public float AimPower => aimPower;

        private void Awake()
        {
            if (character == null) character = GetComponent<Character>();
            if (targetCamera == null) targetCamera = Camera.main;

            if (aimLine != null)
            {
                aimLine.useWorldSpace = true;
                aimLine.positionCount = 2;
                aimLine.enabled = false;
            }
        }

        private void Update()
        {
            if (character == null) return;
            if (targetCamera == null) targetCamera = Camera.main;

            pointerWorld = InputRouter.PointerWorld(targetCamera);

            if (!character.CanAct)
            {
                if (aiming) CancelAim();
                return;
            }

            if (!aiming && InputRouter.LeftPressed) BeginAim();
            if (!aiming) return;

            UpdateAim();

            if (InputRouter.LeftReleased) Fire();
            else if (InputRouter.RightPressed || InputRouter.EscapePressed) CancelAim();
        }

        private void BeginAim()
        {
            aiming = true;
            dragOrigin = pointerWorld;
            aimPower = 0f;
            character.BeginAim();
        }

        private void UpdateAim()
        {
            Vector2 pull = GetPullVector(out float distance);
            aimPower = Mathf.Clamp01(distance / Mathf.Max(maxDragDistance, 0.01f));

            character.SetAimPower(aimPower);
            character.FaceTo((Vector2)character.AimPivot.position - pull);

            UpdateAimLine(pull);
        }

        private void Fire()
        {
            aiming = false;
            HideAimLine();

            if (projectilePrefab == null)
            {
                CancelAim();
                return;
            }

            Vector2 pull = GetPullVector(out float distance);
            if (distance < 0.05f)
            {
                CancelAim();
                return;
            }

            Vector2 direction = -pull.normalized;
            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, aimPower);

            Projectile projectile = ShotSpawner.Spawn(projectilePrefab, character, direction, speed);
            if (projectile == null)
            {
                CancelAim();
                return;
            }

            character.CancelAim();
            character.SetAimPower(0f);
            aimPower = 0f;

            GameEvents.RaiseShotFired(character, projectile);
        }

        private Vector2 GetPullVector(out float distance)
        {
            Vector2 pull = dragOrigin - pointerWorld;
            distance = Mathf.Min(pull.magnitude, maxDragDistance);
            return distance > 0.0001f ? pull.normalized * distance : Vector2.zero;
        }

        private void UpdateAimLine(Vector2 pull)
        {
            if (aimLine == null) return;

            Vector3 start = ShotSpawner.GetOrigin(character);
            Vector3 end = start + (Vector3)(-pull.normalized * (aimLineLength * Mathf.Max(aimPower, 0.05f)));

            aimLine.enabled = true;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, end);

            Color color = aimLine.startColor;
            color.a = 0.35f + 0.65f * aimPower;
            aimLine.startColor = color;
            aimLine.endColor = color;
        }

        private void HideAimLine()
        {
            if (aimLine != null) aimLine.enabled = false;
        }

        private void CancelAim()
        {
            aiming = false;
            aimPower = 0f;

            if (character != null)
            {
                character.CancelAim();
                character.SetAimPower(0f);
            }

            HideAimLine();
        }
    }
}
