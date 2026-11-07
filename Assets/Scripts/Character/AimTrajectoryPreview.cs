using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 瞄准时的弹道提示：从出膛点沿发射方向画一段**虚线抛物线**。
    ///
    /// 用动态 Mesh 拼出一条一条的短矩形（每段即一个“虚线段”），不依赖任何贴图与材质资源，
    /// 也不需要 LineRenderer 的 UV 平铺技巧。采样步长按“虚线段长度 ÷ 速度”反推，
    /// 所以不同速度下虚线的疏密是一致的。
    ///
    /// 只做表现：不产生碰撞、不影响任何判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AimTrajectoryPreview : MonoBehaviour
    {
        private const int MaxPoints = 96;
        private const int MaxVertices = MaxPoints * 4;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private int sortingOrder = 8;

        private Mesh mesh;
        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private Material material;

        private readonly List<Vector3> vertices = new List<Vector3>(MaxVertices);
        private readonly List<Color> colors = new List<Color>(MaxVertices);
        private readonly List<int> triangles = new List<int>(MaxVertices * 2);

        /// <summary>运行时创建；不挂在任何物体下（Mesh 顶点直接用世界坐标）。</summary>
        public static AimTrajectoryPreview Create()
        {
            var go = new GameObject("AimTrajectoryPreview");
            return go.AddComponent<AimTrajectoryPreview>();
        }

        private void Awake()
        {
            mesh = new Mesh { name = "AimTrajectoryPreview" };
            mesh.MarkDynamic();

            filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                material = new Material(shader) { name = "AimTrajectoryPreview" };
                meshRenderer.sharedMaterial = material;
            }
            else
            {
                Debug.LogWarning("[AimTrajectoryPreview] 找不到可用 shader，弹道预览将无法按设定颜色渲染", this);
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = sortingOrder;

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 画出一段虚线弹道。
        /// </summary>
        /// <param name="origin">出膛点（世界坐标）</param>
        /// <param name="direction">发射方向（单位向量）</param>
        /// <param name="speed">初速</param>
        /// <param name="gravityScale">重力倍率（负值 = 上飘）</param>
        /// <param name="linearDamping">空气阻力</param>
        /// <param name="duration">预览时长（秒），越大越长</param>
        /// <param name="dashLength">单个虚线段长度（世界单位）</param>
        /// <param name="width">线宽</param>
        /// <param name="color">颜色（末端会自动淡出）</param>
        /// <param name="obstacleMask">撞到这些层就截断；0 表示不检测</param>
        public void Draw(Vector2 origin, Vector2 direction, float speed, float gravityScale,
                         float linearDamping, float duration, float dashLength, float width,
                         Color color, LayerMask obstacleMask)
        {
            if (mesh == null) return;
            if (direction.sqrMagnitude < 0.0001f || speed < 0.01f)
            {
                Hide();
                return;
            }

            direction = direction.normalized;

            // 采样步长 = 虚线段长度 / 速度 → 相邻采样点在世界里正好隔一个虚线长度
            float dt = Mathf.Clamp(dashLength / Mathf.Max(speed, 0.01f), 0.004f, 0.05f);

            Vector2 velocity = direction * speed;
            Vector2 gravity = Physics2D.gravity * gravityScale;
            Vector2 point = origin;

            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            Vector2 previous = point;
            float elapsed = 0f;
            bool draw = true;           // 奇偶交替，形成虚线
            int dashIndex = 0;

            for (int i = 0; i < MaxPoints && elapsed < duration; i++)
            {
                velocity += gravity * dt;
                if (linearDamping > 0f) velocity /= 1f + linearDamping * dt;
                point += velocity * dt;
                elapsed += dt;

                if (draw)
                {
                    Vector2 segmentStart = previous;
                    Vector2 segmentEnd = point;

                    // 撞到地形就截断，不再往后画
                    if (obstacleMask.value != 0)
                    {
                        RaycastHit2D hit = Physics2D.Linecast(segmentStart, segmentEnd, obstacleMask);
                        if (hit.collider != null)
                        {
                            AddDash(segmentStart, hit.point, width, color, elapsed / Mathf.Max(duration, 0.01f), dashIndex++);
                            break;
                        }
                    }

                    AddDash(segmentStart, segmentEnd, width, color, elapsed / Mathf.Max(duration, 0.01f), dashIndex++);
                }

                draw = !draw;
                previous = point;
            }

            if (vertices.Count == 0)
            {
                Hide();
                return;
            }

            // 颜色除了写进顶点色，也写进材质：部分 shader（URP 2D / 自定义）不吃顶点色，
            // 只靠 mesh.SetColors 会出现「改了颜色没反应」。alpha 仍由顶点色控制淡出。
            ApplyMaterialColor(color);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        /// <summary>把线色写进材质（rgb 走材质、alpha 走顶点，避免两处相乘过淡）。</summary>
        private void ApplyMaterialColor(Color color)
        {
            if (material == null) return;
            if (!material.HasProperty(ColorPropertyId)) return;

            material.SetColor(ColorPropertyId, new Color(color.r, color.g, color.b, 1f));
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void AddDash(Vector2 a, Vector2 b, float width, Color color, float t, int index)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.0005f) return;

            Vector2 normal = new Vector2(-delta.y, delta.x) / length * (width * 0.5f);

            // 越远越淡，避免整条线抢眼
            float alpha = color.a * Mathf.Clamp01(1f - t * 0.75f);
            Color near = new Color(color.r, color.g, color.b, alpha);
            Color far = new Color(color.r, color.g, color.b, alpha * 0.55f);

            int v = vertices.Count;
            vertices.Add(a + normal);
            vertices.Add(b + normal);
            vertices.Add(b - normal);
            vertices.Add(a - normal);

            colors.Add(near);
            colors.Add(far);
            colors.Add(far);
            colors.Add(near);

            triangles.Add(v);
            triangles.Add(v + 2);
            triangles.Add(v + 1);
            triangles.Add(v);
            triangles.Add(v + 3);
            triangles.Add(v + 2);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
