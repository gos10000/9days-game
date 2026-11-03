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
            int index = initialSkinIndex >= 0
                ? initialSkinIndex
                : (config != null ? config.defaultSkinIndex : -1);

            if (index >= 0) ApplySkin(index);
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

            if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;

            if (skin == null || skin.visualPrefab == null) return;

            Transform parent = visualRoot != null ? visualRoot : transform;
            skinInstance = Instantiate(skin.visualPrefab, parent, false);
            skinInstance.transform.localPosition = skin.visualOffset;
            skinInstance.transform.localRotation = Quaternion.identity;
            skinInstance.transform.localScale = Vector3.one;

            renderers = skinInstance.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = skin.sortingOrder + i;
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

        private void ApplyFacing()
        {
            bool flip = facing < 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].flipX = flip;
            }
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

        /// <summary>没有 Animator 时的兜底姿态：倒了就歪，飞出去就后仰。</summary>
        private float GetFallbackAngle()
        {
            CharacterState state = character != null ? character.State : CharacterState.Idle;
            switch (state)
            {
                case CharacterState.Airborne: return -25f * facing;
                case CharacterState.Downed: return -80f * facing;
                default: return 0f;
            }
        }
    }
}
