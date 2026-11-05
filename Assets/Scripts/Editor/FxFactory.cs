using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 在当前场景里生成三套测试粒子特效：撞击碎石子 / 爆炸 / 强化爆炸。
    /// 只创建对象、材质与两张程序化贴图，不改场景里其它东西；
    /// 重复执行会先删掉旧的同名根物体，所以可以反复调参数后重跑。
    /// </summary>
    internal static class FxFactory
    {
        private const string FxTextureRoot = "Assets/Art/FX";
        private const string FxMaterialRoot = "Assets/Art/Material";
        private const string FxSortingLayer = "FX";

        private const string ImpactName = "FX_Impact_Debris";
        private const string BoomName = "FX_Explosion";
        private const string BoomStrongName = "FX_Explosion_Strong";

        [MenuItem("9Days/FX/Build Test Effects (当前场景)")]
        internal static void Build()
        {
            Texture2D soft = EnsureSoftCircle();
            Texture2D ring = EnsureRing();

            Material add = EnsureMaterial(FxMaterialRoot + "/FxAdditive.mat", "NineDays/FX/ParticleAdd", soft);
            Material alpha = EnsureMaterial(FxMaterialRoot + "/FxAlpha.mat", "NineDays/FX/ParticleAlpha", soft);
            Material shock = EnsureMaterial(FxMaterialRoot + "/FxShockwave.mat", "NineDays/FX/ParticleAdd", ring);

            if (add == null || alpha == null || shock == null)
            {
                Debug.LogError("[FxFactory] 材质创建失败：确认 Assets/Art/Shaders/Effect 下的 ParticleAdd / ParticleAlpha 已导入且编译通过");
                return;
            }

            Vector3[] slots = ResolveSlots();

            GameObject impact = RecreateRoot(ImpactName, slots[0]);
            BuildDebris(impact.transform, alpha);

            GameObject boom = RecreateRoot(BoomName, slots[1]);
            BuildExplosion(boom.transform, add, alpha, shock, 1f, false);

            GameObject strong = RecreateRoot(BoomStrongName, slots[2]);
            BuildExplosion(strong.transform, add, alpha, shock, 1.6f, true);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[FxFactory] 已生成 {ImpactName} / {BoomName} / {BoomStrongName}。"
                    + "选中任一物体，Inspector 底部 Particle Effect 面板点 Restart 可反复预览。");
        }

        // ───────────────────────── 三种特效 ─────────────────────────

        /// <summary>撞击碎石子：灰色小颗粒向四周崩开，受重力、带旋转、尾段淡出。</summary>
        private static void BuildDebris(Transform root, Material mat)
        {
            ParticleSystem ps = NewSystem(root, "Debris");

            SetupMain(ps, 0.5f, 0.35f, 0.8f, 2.5f, 6.5f, 0.045f, 0.11f, 1.4f,
                      new Color(0.62f, 0.62f, 0.62f, 1f), 160);
            SetBurst(ps, 26);
            SetCircleShape(ps, 0.08f, 0.55f);

            SetColor(ps, MakeGradient(
                new[] { new Color(0.80f, 0.80f, 0.80f), new Color(0.34f, 0.34f, 0.34f) },
                new[] { 0f, 1f },
                new[] { new Vector2(1f, 0f), new Vector2(1f, 0.60f), new Vector2(0f, 1f) }));

            SetSize(ps, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f));
            SetSpin(ps, 720f);

            // 空气阻力：飞一段后速度衰减，不然像直线喷水
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = new ParticleSystem.MinMaxCurve(4.5f);
            drag.dampen = 0.25f;
            drag.drag = 0.15f;

            SetRenderer(ps, mat, 4, 0f, 0f);
            ps.Play();
        }

        /// <summary>爆炸：闪白 + 火星 + 烟，可选冲击波环。power 用来整体放大。</summary>
        private static void BuildExplosion(Transform root, Material addMat, Material alphaMat,
                                           Material shockMat, float power, bool withShockwave)
        {
            BuildFlash(root, addMat, power);
            BuildSparks(root, addMat, power);
            BuildSmoke(root, alphaMat, power);
            if (withShockwave) BuildShockwave(root, shockMat, power);
        }

        private static void BuildFlash(Transform root, Material mat, float power)
        {
            ParticleSystem ps = NewSystem(root, "Flash");

            SetupMain(ps, 0.2f, 0.12f, 0.16f, 0f, 0f,
                      1.5f * power, 2.1f * power, 0f,
                      new Color(1f, 0.96f, 0.80f, 1f), 4);
            SetBurst(ps, 1);
            SetCircleShape(ps, 0.02f, 0f);

            // 从 0.45 涨到 1：一闪即散
            SetSize(ps, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1f));
            SetColor(ps, MakeGradient(
                new[] { new Color(1f, 0.98f, 0.88f), new Color(1f, 0.72f, 0.30f) },
                new[] { 0f, 1f },
                new[] { new Vector2(1f, 0f), new Vector2(0f, 1f) }));

            SetRenderer(ps, mat, 10, 0f, 0f);
            ps.Play();
        }

        private static void BuildSparks(Transform root, Material mat, float power)
        {
            ParticleSystem ps = NewSystem(root, "Sparks");

            SetupMain(ps, 0.5f, 0.28f, 0.62f, 4.5f * power, 9.5f * power,
                      0.10f, 0.20f, 0.8f,
                      new Color(1f, 0.85f, 0.45f, 1f), 400);
            SetBurst(ps, Mathf.RoundToInt(28 * power));
            SetCircleShape(ps, 0.10f, 0.75f);

            SetColor(ps, MakeGradient(
                new[] { new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.15f), new Color(0.55f, 0.12f, 0.05f) },
                new[] { 0f, 0.45f, 1f },
                new[] { new Vector2(1f, 0f), new Vector2(1f, 0.55f), new Vector2(0f, 1f) }));

            SetSize(ps, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.4f));

            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = new ParticleSystem.MinMaxCurve(8f);
            drag.dampen = 0.2f;

            // 拉长的火星：沿速度方向拖出流线
            SetRenderer(ps, mat, 9, 0.045f, 1.8f);
            ps.Play();
        }

        private static void BuildSmoke(Transform root, Material mat, float power)
        {
            ParticleSystem ps = NewSystem(root, "Smoke");

            SetupMain(ps, 0.8f, 0.9f, 1.6f, 0.5f, 1.8f,
                      0.55f * power, 0.9f * power, -0.06f,
                      new Color(0.22f, 0.22f, 0.24f, 1f), 120);
            SetBurst(ps, Mathf.RoundToInt(14 * power));
            SetCircleShape(ps, 0.28f * power, 0.85f);

            SetColor(ps, MakeGradient(
                new[] { new Color(0.30f, 0.30f, 0.33f), new Color(0.10f, 0.10f, 0.12f) },
                new[] { 0f, 1f },
                new[] { new Vector2(0.85f, 0f), new Vector2(0.55f, 0.4f), new Vector2(0f, 1f) }));

            // 烟要越飘越大
            SetSize(ps, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.7f));
            SetSpin(ps, 60f);

            SetRenderer(ps, mat, 2, 0f, 0f);
            ps.Play();
        }

        private static void BuildShockwave(Transform root, Material mat, float power)
        {
            ParticleSystem ps = NewSystem(root, "Shockwave");

            SetupMain(ps, 0.4f, 0.32f, 0.38f, 0f, 0f,
                      0.5f, 0.7f, 0f,
                      new Color(1f, 0.85f, 0.55f, 1f), 4);
            SetBurst(ps, 1);
            SetCircleShape(ps, 0.02f, 0f);

            // 快速扩张后消失，这是“强化”和普通爆炸最直观的差别
            SetSize(ps, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 5.5f * power));
            SetColor(ps, MakeGradient(
                new[] { new Color(1f, 0.92f, 0.70f), new Color(1f, 0.45f, 0.15f) },
                new[] { 0f, 1f },
                new[] { new Vector2(1f, 0f), new Vector2(0f, 1f) }));

            SetRenderer(ps, mat, 6, 0f, 0f);
            ps.Play();
        }

        // ───────────────────────── 粒子模块封装 ─────────────────────────

        private static ParticleSystem NewSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static void SetupMain(ParticleSystem ps, float duration,
                                      float lifeMin, float lifeMax,
                                      float speedMin, float speedMax,
                                      float sizeMin, float sizeMax,
                                      float gravity, Color startColor, int maxParticles)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = true;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(startColor);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(gravity);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        private static void SetBurst(ParticleSystem ps, int count, float time = 0f)
        {
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = true;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            em.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        private static void SetCircleShape(ParticleSystem ps, float radius, float randomDirection)
        {
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.randomDirectionAmount = Mathf.Clamp01(randomDirection);
        }

        private static void SetColor(ParticleSystem ps, Gradient gradient)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetSize(ParticleSystem ps, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>自转速度（度/秒）；API 里单位是弧度，这里转好再传。</summary>
        private static void SetSpin(ParticleSystem ps, float degreesPerSecond)
        {
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = false;
            float rad = degreesPerSecond * Mathf.Deg2Rad;
            rot.z = new ParticleSystem.MinMaxCurve(-rad, rad);
        }

        private static ParticleSystemRenderer SetRenderer(ParticleSystem ps, Material mat, int order,
                                                          float velocityScale, float lengthScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = FxSortingLayer;
            r.sortingOrder = order;
            r.alignment = ParticleSystemRenderSpace.View;
            r.minParticleSize = 0f;
            r.maxParticleSize = 30f;   // 默认 0.5 会把闪光和冲击波裁掉

            if (velocityScale > 0f)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = velocityScale;
                r.lengthScale = lengthScale;
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
            }

            return r;
        }

        // ───────────────────────── 资源生成 ─────────────────────────

        private static Vector3[] ResolveSlots()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (cam == null)
            {
                return new[] { new Vector3(-4f, 0f, 0f), Vector3.zero, new Vector3(4f, 0f, 0f) };
            }

            float halfH = cam.orthographic ? cam.orthographicSize : 5.5f;
            float halfW = halfH * Mathf.Max(0.1f, cam.aspect);
            Vector3 c = cam.transform.position;
            float y = c.y + halfH * 0.15f;

            return new[]
            {
                new Vector3(c.x - halfW * 0.55f, y, 0f),
                new Vector3(c.x, y, 0f),
                new Vector3(c.x + halfW * 0.55f, y, 0f),
            };
        }

        private static GameObject RecreateRoot(string name, Vector3 position)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var go = new GameObject(name);
            go.transform.position = position;
            return go;
        }

        private static Texture2D EnsureSoftCircle()
        {
            const string path = FxTextureRoot + "/FxSoftCircle.png";
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (loaded != null) return loaded;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);          // 圆头软边
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            return WriteTexture(path, tex, 128);
        }

        private static Texture2D EnsureRing()
        {
            const string path = FxTextureRoot + "/FxRing.png";
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (loaded != null) return loaded;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float band = 1f - Mathf.Abs(d - 0.80f) / 0.16f;
                    float a = Mathf.Clamp01(band);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            return WriteTexture(path, tex, 256);
        }

        private static Texture2D WriteTexture(string path, Texture2D tex, int maxSize)
        {
            EditorAssets.EnsureFolderOfFile(path);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material EnsureMaterial(string path, string shaderName, Texture texture)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[FxFactory] 找不到 shader: {shaderName}");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EditorAssets.EnsureFolderOfFile(path);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.mainTexture = texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Gradient MakeGradient(Color[] colors, float[] colorTimes, Vector2[] alphaKeys)
        {
            var gradient = new Gradient();
            var colorKeys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) colorKeys[i] = new GradientColorKey(colors[i], colorTimes[i]);

            var alpha = new GradientAlphaKey[alphaKeys.Length];
            for (int i = 0; i < alphaKeys.Length; i++) alpha[i] = new GradientAlphaKey(alphaKeys[i].x, alphaKeys[i].y);

            gradient.SetKeys(colorKeys, alpha);
            return gradient;
        }
    }
}
