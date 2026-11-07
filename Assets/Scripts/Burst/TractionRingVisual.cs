using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 牵引区的视觉：一圈圈从外向内收缩并淡出的圆环，用 LineRenderer 画，不依赖贴图。
    /// 由 TractionZone 在创建时动态挂上。
    /// </summary>
    public sealed class TractionRingVisual : MonoBehaviour
    {
        [SerializeField] private int ringCount = 4;
        [SerializeField] private float ringLifetime = 0.9f;
        [SerializeField] private float lineWidth = 0.07f;
        [SerializeField] private int segments = 48;

        private LineRenderer[] rings;
        private float[] progress;
        private Vector2[] unitCircle;
        private float radius = 4f;
        private Color baseColor = new Color(0.35f, 0.95f, 0.85f, 1f);

        public void Initialize(float effectRadius, Color color, int count, float lifetime, float width)
        {
            radius = Mathf.Max(0.01f, effectRadius);
            baseColor = color;
            ringCount = Mathf.Clamp(count, 1, 12);
            ringLifetime = Mathf.Max(0.05f, lifetime);
            lineWidth = Mathf.Max(0f, width);

            BuildRings();
        }

        private void Update()
        {
            if (rings == null) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            for (int i = 0; i < rings.Length; i++)
            {
                LineRenderer ring = rings[i];
                if (ring == null) continue;

                progress[i] += deltaTime / ringLifetime;
                if (progress[i] >= 1f) progress[i] -= 1f;

                // 从满半径收缩到 0，同时淡出
                float t = progress[i];
                float currentRadius = radius * (1f - t);
                float alpha = (1f - t) * baseColor.a;

                ApplyCircle(ring, currentRadius, alpha);
            }
        }

        private void BuildRings()
        {
            rings = new LineRenderer[ringCount];
            progress = new float[ringCount];

            if (unitCircle == null || unitCircle.Length != segments)
            {
                unitCircle = new Vector2[segments];
                for (int i = 0; i < segments; i++)
                {
                    float angle = (i / (float)segments) * Mathf.PI * 2f;
                    unitCircle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }

            Material material = CreateMaterial();

            for (int i = 0; i < ringCount; i++)
            {
                var go = new GameObject($"Ring_{i}");
                go.transform.SetParent(transform, false);

                var ring = go.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = segments;
                ring.startWidth = lineWidth;
                ring.endWidth = lineWidth;
                ring.numCapVertices = 2;
                ring.numCornerVertices = 2;
                ring.textureMode = LineTextureMode.Stretch;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.receiveShadows = false;
                if (material != null) ring.material = material;

                // 相位错开，形成连续不断的收缩
                progress[i] = i / (float)ringCount;
                rings[i] = ring;

                ApplyCircle(ring, radius * (1f - progress[i]), 1f - progress[i]);
            }
        }

        private void ApplyCircle(LineRenderer ring, float currentRadius, float alpha)
        {
            if (unitCircle == null || ring == null) return;

            for (int i = 0; i < unitCircle.Length; i++)
                ring.SetPosition(i, unitCircle[i] * currentRadius);

            Color color = baseColor;
            color.a = Mathf.Clamp01(alpha);
            ring.startColor = color;
            ring.endColor = color;
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) return null;

            return new Material(shader);
        }

        private void OnDestroy()
        {
            if (rings == null) return;

            for (int i = 0; i < rings.Length; i++)
                if (rings[i] != null && rings[i].material != null)
                    Destroy(rings[i].material);
        }
    }
}
