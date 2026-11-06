using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 彩虹染色：让 SpriteRenderer 的颜色随时间在色相环上循环，做出「彩虹小球」。
    /// 只改 renderer.color，不影响物理、命中与对象池逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainbowTint : MonoBehaviour
    {
        [Header("色彩")]
        [Tooltip("色相每秒循环速度，1 表示 1 秒走完一圈")]
        [SerializeField] private float hueSpeed = 0.5f;

        [Tooltip("初始色相偏移 0~1")]
        [SerializeField, Range(0f, 1f)] private float hueOffset = 0f;

        [SerializeField, Range(0f, 1f)] private float saturation = 0.85f;
        [SerializeField, Range(0f, 1f)] private float brightness = 1f;

        [Tooltip("保留贴图原有透明度时勾选；取消勾选会强制用下面的 Alpha 值")]
        [SerializeField] private bool keepOriginalAlpha = true;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;

        [Header("呼吸")]
        [Tooltip("亮度按正弦轻微起伏，让小球更有「能量球」感")]
        [SerializeField] private bool pulseBrightness = true;
        [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.15f;
        [SerializeField] private float pulseSpeed = 2f;

        [Header("范围")]
        [SerializeField] private bool includeChildren = false;

        private SpriteRenderer[] renderers = new SpriteRenderer[0];
        private float[] baseAlphas = new float[0];

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            if (renderers.Length == 0) CacheRenderers();
        }

        private void Update()
        {
            if (renderers.Length == 0) return;

            float hue = Mathf.Repeat(hueOffset + Time.time * hueSpeed, 1f);
            float value = brightness;

            if (pulseBrightness)
                value = Mathf.Clamp01(brightness - pulseAmount * 0.5f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * 0.5f);

            Color color = Color.HSVToRGB(hue, saturation, value);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;

                color.a = keepOriginalAlpha ? baseAlphas[i] : alpha;
                sr.color = color;
            }
        }

        private void CacheRenderers()
        {
            renderers = includeChildren
                ? GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponents<SpriteRenderer>();

            baseAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) baseAlphas[i] = renderers[i].color.a;
            }
        }
    }
}
