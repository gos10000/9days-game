using System.Collections;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace NineDays
{
    /// <summary>
    /// 表现层：皮肤实例化 / 切换、朝向、受击闪白、Animator 参数桥。
    /// 挂在角色的 Visual 子节点上，皮肤 prefab 在运行时实例化到这里，逻辑 prefab 不感知具体美术。
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Character character;
        [SerializeField] private CharacterConfig config;
        [SerializeField] private Transform visualRoot;

        [Header("皮肤")]
        [Tooltip("默认皮肤下标，-1 表示使用 CharacterConfig.defaultSkinIndex")]
        [SerializeField] private int initialSkinIndex = -1;

        [Header("初始朝向")]
        [Tooltip("0 = 按阵营自动（Player1 朝右、Player2 朝左）；1 = 强制朝右；-1 = 强制朝左")]
        [SerializeField] private int initialFacing = 0;

        [Header("无动画时的兜底表现")]
        [SerializeField] private bool allowFallbackTilt = true;
        [SerializeField] private float fallbackTiltSpeed = 360f;

        [Tooltip("受击闪白时长（秒）")]
        [SerializeField] private float flashDuration = 0.1f;

        private static readonly Color FlashColor = new Color(1f, 0.35f, 0.35f, 1f);

        private GameObject skinInstance;
        private Animator animator;
        private SpriteRenderer[] renderers = new SpriteRenderer[0];
        private Color[] baseColors = new Color[0];
        private int[] baseSortingOrders = new int[0];
        private Coroutine flashRoutine;
        private int facing = 1;
        private float fallbackAngle;

        public int CurrentSkinIndex { get; private set; } = -1;

        private void Awake()
        {
            if (character == null) character = GetComponentInParent<Character>();
            if (visualRoot == null) visualRoot = transform;
            if (config == null && character != null) config = character.Config;
        }

        private void OnEnable()
        {
            if (character != null) character.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (character != null) character.StateChanged -= OnStateChanged;
        }

        private void Start()
        {
            facing = ResolveInitialFacing();

            int index = initialSkinIndex >= 0
                ? initialSkinIndex
                : (config != null ? config.defaultSkinIndex : -1);

            if (index >= 0) ApplySkin(index);
            else ApplyFacing();
        }

        /// <summary>
        /// 初始朝向：显式配置优先；未配置时按阵营给默认值——Player2 面朝左，这样双人对峙时两人相对而立。
        /// </summary>
        private int ResolveInitialFacing()
        {
            if (initialFacing > 0) return 1;
            if (initialFacing < 0) return -1;

            return character != null && character.Team == Team.Player2 ? -1 : 1;
        }

        private void Update()
        {
            if (!allowFallbackTilt || animator != null || visualRoot == null) return;

            fallbackAngle = Mathf.MoveTowardsAngle(fallbackAngle, GetFallbackAngle(), fallbackTiltSpeed * Time.deltaTime);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, fallbackAngle);
        }

        /// <summary>按索引切换皮肤（来自 CharacterConfig.skins）。</summary>
        public void ApplySkin(int index)
        {
            CurrentSkinIndex = index;
            ApplySkin(config != null ? config.GetSkin(index) : null);
        }

        /// <summary>切换皮肤：销毁旧表现 → 实例化新表现 → 重绑 Animator → 重写当前状态。</summary>
        public void ApplySkin(CharacterConfig.Skin skin)
        {
            if (skinInstance != null) Destroy(skinInstance);

            skinInstance = null;
            animator = null;
            renderers = new SpriteRenderer[0];
            baseColors = new Color[0];
            baseSortingOrders = new int[0];

            if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;

            if (skin == null || skin.visualPrefab == null) return;

            Transform parent = visualRoot != null ? visualRoot : transform;
            skinInstance = Instantiate(skin.visualPrefab, parent, false);
            skinInstance.transform.localPosition = skin.visualOffset;
            skinInstance.transform.localRotation = Quaternion.identity;
            skinInstance.transform.localScale = Vector3.one;

            renderers = skinInstance.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            baseSortingOrders = new int[renderers.Length];

            // 个别部件可能在美术 prefab 里挂错了 Sorting Layer。
            // SortingLayer 的优先级高于 Order in Layer，只要有一个部件层不对，
            // 整个角色的 order 排序就会被它打乱。这里取"多数部件所在的层"作为基准。
            int majorLayerId = renderers.Length > 0 ? renderers[0].sortingLayerID : 0;
            int maxCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                int id = renderers[i].sortingLayerID;
                int count = 0;
                for (int j = 0; j < renderers.Length; j++)
                    if (renderers[j].sortingLayerID == id) count++;
                if (count > maxCount) { maxCount = count; majorLayerId = id; }
            }

            // 排序规则：保留美术在 prefab 里为每个部件设好的相对顺序（0~4 那组值），
            // 再整体加上 skin.sortingOrder 作为与世界其他物体比较的基准。
            // 之前直接用遍历索引 i 覆盖，部件在层级里的深度优先顺序不是美术意图，
            // 会把"身体盖住头"这类错误固化进来。
            for (int i = 0; i < renderers.Length; i++)
            {
                baseSortingOrders[i] = renderers[i].sortingOrder;
                renderers[i].sortingLayerID = majorLayerId;
                renderers[i].sortingOrder = skin.sortingOrder + baseSortingOrders[i];
                baseColors[i] = renderers[i].color;
            }

            animator = skinInstance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (skin.controller != null) animator.runtimeAnimatorController = skin.controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            SpriteLibrary library = skinInstance.GetComponentInChildren<SpriteLibrary>(true);
            if (library != null && skin.spriteLibrary != null) library.spriteLibraryAsset = skin.spriteLibrary;

            ApplyFacing();
            OnStateChanged(character != null ? character.State : CharacterState.Idle);
        }

        /// <summary>朝向目标（只翻表现，不影响碰撞体）。</summary>
        public void FaceTo(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) < 0.01f) return;
            facing = direction.x >= 0f ? 1 : -1;
            ApplyFacing();
        }

        /// <summary>受击闪白（时长取 Inspector 配置）。</summary>
        public void Flash()
        {
            Flash(flashDuration);
        }

        /// <summary>受击闪白，可指定时长。</summary>
        public void Flash(float duration)
        {
            if (renderers.Length == 0) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(duration));
        }

        /// <summary>
        /// 仅为兼容 Character.SetAimPower 保留的接口。
        /// 瞄准力度已改为由 SkinAnimator 直接读取 CharacterLauncher.AimPower，这里不再处理。
        /// </summary>
        public void SetAimPower(float power) { }

        private IEnumerator FlashRoutine(float duration)
        {
            SetRendererColors(FlashColor);
            yield return new WaitForSeconds(duration);
            RestoreRendererColors();
            flashRoutine = null;
        }

        /// <summary>
        /// 翻转朝向。必须翻容器（visualRoot 的 localScale.x）而不是逐个 SpriteRenderer.flipX：
        /// flipX 只镜像图像、不动子物体位置，多部件拼图皮肤会变成"每个部件各自反了但位置没换"。
        /// 翻父节点则位置和图像一起镜像，骨骼 / 拼图皮肤都成立。
        /// </summary>
        private void ApplyFacing()
        {
            if (visualRoot == null) return;

            Vector3 scale = visualRoot.localScale;
            float magnitude = Mathf.Abs(scale.x);
            if (Mathf.Approximately(magnitude, 0f)) magnitude = 1f;
            scale.x = facing < 0 ? -magnitude : magnitude;
            visualRoot.localScale = scale;
        }

        private void SetRendererColors(Color color)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].color = color;
            }
        }

        private void RestoreRendererColors()
        {
            for (int i = 0; i < renderers.Length && i < baseColors.Length; i++)
            {
                if (renderers[i] != null) renderers[i].color = baseColors[i];
            }
        }

        /// <summary>状态变化：只维护兜底旋转的基准角度，动画推进交给 SkinAnimator。</summary>
        private void OnStateChanged(CharacterState state)
        {
            if (visualRoot != null)
                fallbackAngle = Mathf.DeltaAngle(0f, visualRoot.localEulerAngles.z);
        }

        /// <summary>
        /// 没有 Animator 时的兜底姿态：倒了就歪，飞出去就后仰。
        /// 角度不再乘 facing：visualRoot 的 localScale.x 已经镜像过了，
        /// 再乘一次会让左朝向时往错误的方向倒。
        /// </summary>
        private float GetFallbackAngle()
        {
            CharacterState state = character != null ? character.State : CharacterState.Idle;
            switch (state)
            {
                case CharacterState.Airborne: return -25f;
                case CharacterState.Downed: return -80f;
                default: return 0f;
            }
        }
    }
}
