using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 彩虹描边：把挂载对象及其子物体上的所有 SpriteRenderer 换成 "NineDays/SpriteRainbowOutline" 材质，
    /// 再按时间循环调色，形成随时间流动的彩虹光边。只改渲染，不碰碰撞与逻辑。
    /// 禁用组件会在 OnDisable 里把原材质还原回去。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainbowOutline : MonoBehaviour
    {
        [Header("描边")]
        [Tooltip("描边宽度（贴图像素），建议 1~4")]
        [SerializeField, Range(0f, 10f)] private float outlineWidth = 2f;

        [Tooltip("光晕强度，越大越亮")]
        [SerializeField, Range(0f, 4f)] private float glowStrength = 1.6f;

        [Header("彩虹")]
        [Tooltip("色相每秒循环速度，1 表示 1 秒走完一圈")]
        [SerializeField] private float hueSpeed = 0.35f;

        [Tooltip("初始色相偏移 0~1，可让两个角色从不同颜色开始")]
        [SerializeField, Range(0f, 1f)] private float hueOffset = 0f;

        [SerializeField, Range(0f, 1f)] private float saturation = 1f;
        [SerializeField, Range(0f, 1f)] private float brightness = 1f;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;

        [Header("作用范围")]
        [Tooltip("勾选后把子物体里的 SpriteRenderer 也一起描边")]
        [SerializeField] private bool includeChildren = true;

        private SpriteRenderer[] renderers = new SpriteRenderer[0];
        private Material[] originalMaterials = new Material[0];
        private Material outlineMaterial;
        private bool applied;

        private void OnEnable()
        {
            if (!EnsureMaterial()) return;

            CacheRenderers();
            ApplyMaterial();
            ApplyColor(true);
        }

        private void OnDisable()
        {
            RestoreMaterials();
        }

        private void OnDestroy()
        {
            if (outlineMaterial != null)
            {
                if (Application.isPlaying) Destroy(outlineMaterial);
                else DestroyImmediate(outlineMaterial);

                outlineMaterial = null;
            }
        }

        private void Update()
        {
            if (!applied || outlineMaterial == null) return;
            ApplyColor(false);
        }

        private bool EnsureMaterial()
        {
            if (outlineMaterial != null) return true;

            Shader shader = Resources.Load<Shader>("SpriteRainbowOutline");
            if (shader == null) shader = Shader.Find("NineDays/SpriteRainbowOutline");

            if (shader == null)
            {
                Debug.LogWarning("[RainbowOutline] 找不到 NineDays/SpriteRainbowOutline 着色器，描边已禁用", this);
                enabled = false;
                return false;
            }

            outlineMaterial = new Material(shader) { name = "RainbowOutline (Runtime)", hideFlags = HideFlags.DontSave };
            outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
            outlineMaterial.SetFloat("_GlowStrength", glowStrength);
            return true;
        }

        private void CacheRenderers()
        {
            renderers = includeChildren
                ? GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponents<SpriteRenderer>();

            originalMaterials = new Material[renderers.Length];
        }

        private void ApplyMaterial()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;

                originalMaterials[i] = sr.sharedMaterial;
                if (sr.sharedMaterial != outlineMaterial) sr.sharedMaterial = outlineMaterial;
            }

            applied = true;
        }

        private void ApplyColor(bool force)
        {
            if (outlineMaterial == null) return;

            float hue = Mathf.Repeat(hueOffset + Time.unscaledTime * hueSpeed, 1f);
            Color rainbow = Color.HSVToRGB(hue, saturation, brightness);
            rainbow.a = alpha;

            outlineMaterial.SetColor("_OutlineColor", rainbow);

            if (force)
            {
                outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
                outlineMaterial.SetFloat("_GlowStrength", glowStrength);
            }
        }

        private void RestoreMaterials()
        {
            if (!applied || renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;

                if (originalMaterials != null && i < originalMaterials.Length)
                    sr.sharedMaterial = originalMaterials[i];
            }

            applied = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || outlineMaterial == null) return;

            outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
            outlineMaterial.SetFloat("_GlowStrength", glowStrength);
            ApplyColor(true);
        }
#endif
    }
}
