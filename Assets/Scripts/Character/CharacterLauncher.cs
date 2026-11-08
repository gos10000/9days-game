using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 弹弓式拖拽瞄准 + 发射（两人轮流用鼠标）。只有当前回合角色（CanAct）才能操作。
    /// 手感参照弹弓：往哪边拉，子弹就往反方向飞；拖拽距离决定力度。
    /// 瞄准时会画出「后方实线（拉的方向）+ 前方虚线抛物线（脱手后的前段路径）」。
    /// </summary>
    public class CharacterLauncher : MonoBehaviour, IAimInputReceiver
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

        [Header("弹道预览")]
        [Tooltip("预览覆盖的飞行时长（秒）；越大画得越长")]
        [SerializeField] private float previewDuration = 0.35f;

        [Tooltip("单个虚线段长度（世界单位）")]
        [SerializeField] private float previewDashLength = 0.22f;

        [Tooltip("预览线宽")]
        [SerializeField] private float previewWidth = 0.07f;

        [Tooltip("预览线颜色（纯黑、不透明）；末端会自动淡出")]
        [SerializeField] private Color previewColor = new Color(0f, 0f, 0f, 1f);

        private bool aiming;
        private Vector2 dragOrigin;
        private Vector2 pointerWorld;
        private float aimPower;
        private Projectile selectedPrefab;

        private AimTrajectoryPreview preview;
        private ProjectileLoadout cachedLoadout;
        private Projectile cachedPrefab;
        private float cachedSpeed = -1f;

        public bool IsAiming => aiming;
        public float AimPower => aimPower;
        public Camera TargetCamera => targetCamera != null ? targetCamera : Camera.main;
        public Projectile ActiveProjectilePrefab =>
            selectedPrefab != null ? selectedPrefab : projectilePrefab;

        /// <summary>选弹后覆盖本角色默认发射 prefab；null 表示回退默认。</summary>
        public void SetProjectilePrefab(Projectile prefab)
        {
            selectedPrefab = prefab;
            cachedLoadout = null;
        }

        /// <summary>清除选弹覆盖，下一发使用 Inspector 默认 prefab。</summary>
        public void ResetProjectilePrefab()
        {
            selectedPrefab = null;
            cachedLoadout = null;
        }

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

        /// <summary>由 BattleInputController 在按下左键时调用；只允许当前可操作角色进入瞄准。</summary>
        public void BeginAim(Vector2 pointerWorld)
        {
            if (character == null) return;

            // 暂停只停物理与 timeScale，Update 仍会跑，所以这里必须显式挡掉瞄准与发射
            if (GamePause.IsPaused)
            {
                if (aiming) CancelAim();
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;

            // 只有当前回合可操作、且尚未处于瞄准中的角色才能起手
            if (aiming || !character.CanAct) return;

            this.pointerWorld = pointerWorld;
            aiming = true;
            dragOrigin = pointerWorld;
            aimPower = 0f;
            cachedSpeed = -1f;
            character.BeginAim();
        }

        /// <summary>拖拽过程中更新拉拽方向、力度和瞄准线。</summary>
        public void UpdateAim(Vector2 pointerWorld)
        {
            if (!aiming) return;

            if (character == null || !character.CanAct)
            {
                CancelAim();
                return;
            }

            this.pointerWorld = pointerWorld;

            Vector2 pull = GetPullVector(out float distance);
            aimPower = Mathf.Clamp01(distance / Mathf.Max(maxDragDistance, 0.01f));

            character.SetAimPower(aimPower);
            // pull 指向「发射方向」（拉的反方向），所以朝向 = 支点 + pull
            character.FaceTo((Vector2)character.AimPivot.position + pull);

            UpdateAimLine(pull);
            UpdatePreview(pull);
        }

        /// <summary>松开左键时调用；距离过短或没有子弹 prefab 会取消瞄准。</summary>
        public void ReleaseAim(Vector2 pointerWorld)
        {
            if (!aiming) return;

            if (character == null || !character.CanAct)
            {
                CancelAim();
                return;
            }

            this.pointerWorld = pointerWorld;
            Fire();
        }

        private void Fire()
        {
            aiming = false;
            HideAimLine();
            HidePreview();

            Projectile prefab = ActiveProjectilePrefab;
            if (prefab == null)
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

            // 弹弓：pull 是「拉的反方向」，也就是子弹实际飞行的方向
            Vector2 direction = pull.normalized;
            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, aimPower);

            Projectile projectile = ShotSpawner.Spawn(prefab, character, direction, speed);
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

        // ---------------- 弹道预览（虚线抛物线） ----------------

        /// <summary>
        /// 沿发射方向画一段虚线抛物线。用与发射完全相同的数值（特性、道具修改器都算进去），
        /// 所以提示和实际弹道是一致的。
        /// </summary>
        private void UpdatePreview(Vector2 pull)
        {
            if (pull.sqrMagnitude < 0.0001f)
            {
                HidePreview();
                return;
            }

            Projectile prefab = ActiveProjectilePrefab;
            if (prefab == null || prefab.Config == null)
            {
                HidePreview();
                return;
            }

            EnsurePreview();
            if (preview == null) return;

            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, aimPower);
            ProjectileStats stats = ResolvePreviewStats(prefab, speed);
            if (stats == null)
            {
                HidePreview();
                return;
            }

            LayerMask obstacleMask = character != null && character.Config != null
                ? character.Config.obstacleMask
                : default;

            preview.Draw(
                ShotSpawner.GetOrigin(character),
                pull.normalized,
                stats.speed,
                stats.gravityScale,
                stats.linearDamping,
                previewDuration,
                previewDashLength,
                previewWidth,
                previewColor,
                obstacleMask);
        }

        /// <summary>
        /// 复用上一次的构建结果：只有速度变化足够大才重算，
        /// 避免每帧都跑一遍特性 / 修改器（它们可能有副作用或分配）。
        /// </summary>
        private ProjectileStats ResolvePreviewStats(Projectile prefab, float speed)
        {
            if (cachedLoadout != null && cachedPrefab == prefab && Mathf.Abs(cachedSpeed - speed) < 0.08f)
                return cachedLoadout.Stats;

            cachedPrefab = prefab;
            cachedSpeed = speed;

            var modifiers = ShotSpawner.CollectModifiers(character);
            cachedLoadout = ProjectileBuild.Build(prefab.Config, modifiers, character, speed);
            return cachedLoadout != null ? cachedLoadout.Stats : null;
        }

        private void EnsurePreview()
        {
            if (preview != null) return;
            preview = AimTrajectoryPreview.Create();
        }

        private void HidePreview()
        {
            if (preview != null) preview.Hide();
        }

        private void OnDestroy()
        {
            if (preview != null) Destroy(preview.gameObject);
        }

        /// <summary>取消当前瞄准并清理角色瞄准状态和瞄准线。</summary>
        public void CancelAim()
        {
            aiming = false;
            aimPower = 0f;

            if (character != null)
            {
                character.CancelAim();
                character.SetAimPower(0f);
            }

            HideAimLine();
            HidePreview();
        }
    }
}
