using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 左上角（玩家 1）/ 右上角（玩家 2）的双人血条，纯 Canvas 实现。
    ///
    /// 正常情况下它作为预制体 <c>Assets/Prefabs/UI/HealthBarsCanvas.prefab</c> 的实例摆在场景里，
    /// 层级和样式可以在编辑器里直接调；若场景里没有摆，它也能在战斗场景加载后自己生成一份，
    /// 保证任何场景都不会缺 UI。
    ///
    /// 所有图元 raycastTarget = false，并且不挂 GraphicRaycaster，因此完全不拦截点击。
    /// 数据只来自场景里的 Character 和 GameEvents.CharacterHealthChanged，
    /// 自身不持有任何战斗状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHealthBars : MonoBehaviour
    {
        // ---- 布局常量（基于 1920x1080 参考分辨率）----
        private const float BarWidth = 430f;
        private const float BarHeight = 132f;
        private const float ScreenMargin = 36f;
        private const float TrackInset = 32f;
        private const float TrackHeight = 24f;
        private const float TrackBottom = 34f;

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ---- 配色 ----
        private static readonly Color PlateColor = new Color(0.86f, 0.77f, 0.64f, 0.96f);
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color LagColor = new Color(1f, 0.93f, 0.70f, 0.85f);
        private static readonly Color LabelColor = new Color(0.90f, 0.93f, 0.98f, 1f);
        private static readonly Color Player1Color = new Color(0.31f, 0.61f, 1f, 1f);
        private static readonly Color Player2Color = new Color(1f, 0.47f, 0.36f, 1f);

        private sealed class Bar
        {
            public bool Mirrored;
            public RectTransform Fill;
            public RectTransform Lag;
            public Text Value;

            /// <summary>目标比例 0~1。</summary>
            public float Target = 1f;

            /// <summary>实际显示的快速条。</summary>
            public float Shown = 1f;

            /// <summary>拖尾条，掉血时滞后回落，用来做「残影」。</summary>
            public float LagValue = 1f;
        }

        [Header("外观")]
        [Tooltip("留空时自动复用场景里已有的字体（UICanvas 的中文字体）；再找不到就用系统中文字体")]
        [SerializeField] private Font fontOverride;

        private readonly Bar[] bars = new Bar[2];
        private readonly List<Character> bound = new List<Character>();

        private Canvas canvas;
        private bool built;

        // ---------------- 创建 ----------------

        /// <summary>运行时创建血条画布；战斗场景自动调用。</summary>
        public static BattleHealthBars Create()
        {
            var go = new GameObject("BattleHealthBars");

            Canvas created = go.AddComponent<Canvas>();
            created.renderMode = RenderMode.ScreenSpaceOverlay;
            // 低于场景 UICanvas（200），暂停菜单 / 选择页能盖住血条
            created.sortingOrder = 190;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 不挂 GraphicRaycaster：血条不需要交互，也不该挡住任何点击
            return go.AddComponent<BattleHealthBars>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene();

        /// <summary>只在战斗场景（有 BattleController）里建，菜单场景不会冒出空血条。</summary>
        private static void EnsureForScene()
        {
            if (FindAnyObjectByType<BattleHealthBars>() != null) return;
            if (FindAnyObjectByType<BattleController>() == null) return;

            Create();
        }

        // ---------------- 生命周期 ----------------

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            EnsureBuilt();
        }

        private void OnEnable()
        {
            GameEvents.CharacterHealthChanged += OnHealthChanged;
            GameEvents.BattleStarted += OnBattleStarted;

            EnsureBuilt();
            Discover();
        }

        private void OnDisable()
        {
            GameEvents.CharacterHealthChanged -= OnHealthChanged;
            GameEvents.BattleStarted -= OnBattleStarted;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Animate(bars[0], dt);
            Animate(bars[1], dt);
        }

        // ---------------- 数据 ----------------

        private void OnHealthChanged(Character character, float current, float max) =>
            Bind(character, current, max);

        private void OnBattleStarted(int rounds) => Discover();

        /// <summary>扫描场景里的角色，把当前血量灌进对应阵营的血条。</summary>
        private void Discover()
        {
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null) continue;

                CharacterHealth health = character.Health;
                if (health == null) continue;

                Bind(character, character.CurrentHealth, health.Max);
            }

            RefreshVisibility();
        }

        private void Bind(Character character, float current, float max)
        {
            if (character == null) return;

            Bar bar = BarFor(character.Team);
            if (bar == null) return;

            if (!bound.Contains(character)) bound.Add(character);

            if (max <= 0f) max = 1f;
            current = Mathf.Clamp(current, 0f, max);

            bar.Target = current / max;
            if (bar.Value != null)
                bar.Value.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            RefreshVisibility();
        }

        private Bar BarFor(Team team)
        {
            if (team == Team.Player1) return bars[0];
            if (team == Team.Player2) return bars[1];
            return null;
        }

        /// <summary>没有绑定到任何角色时整块画布不渲染，菜单 / 空场景里看不到东西。</summary>
        private void RefreshVisibility()
        {
            if (canvas != null) canvas.enabled = bound.Count > 0;
        }

        private void Animate(Bar bar, float dt)
        {
            if (bar == null) return;

            // 快速条：约 60ms 内到位
            bar.Shown = Mathf.Lerp(bar.Shown, bar.Target, 1f - Mathf.Exp(-16f * dt));
            if (Mathf.Abs(bar.Shown - bar.Target) < 0.001f) bar.Shown = bar.Target;

            // 残影条：慢一拍回落，掉血时能看出「刚失去的那段」
            bar.LagValue = Mathf.Lerp(bar.LagValue, bar.Target, 1f - Mathf.Exp(-4f * dt));
            if (Mathf.Abs(bar.LagValue - bar.Target) < 0.001f) bar.LagValue = bar.Target;

            SetRatio(bar.Fill, bar.Mirrored, bar.Shown);
            SetRatio(bar.Lag, bar.Mirrored, Mathf.Max(bar.LagValue, bar.Shown));
        }

        // ---------------- 构建 UI ----------------

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            // 场景 / 预制体里已经摆好了层级 → 直接接管，不再重复生成
            if (TryAdoptAuthored()) return;

            Font font = ResolveFont();

            bars[0] = BuildBar(Team.Player1, false, Player1Color, "玩家 1", font);
            bars[1] = BuildBar(Team.Player2, true, Player2Color, "玩家 2", font);

            RefreshVisibility();
        }

        /// <summary>
        /// 预制体里的层级约定：
        /// HealthBar_Player1 / HealthBar_Player2 下各有 Track/Lag、Track/Fill、Value。
        /// 找不到就返回 false，走运行时生成。
        /// </summary>
        private bool TryAdoptAuthored()
        {
            bars[0] = AdoptBar("HealthBar_Player1");
            bars[1] = AdoptBar("HealthBar_Player2");

            if (bars[0] == null || bars[1] == null) return false;

            RefreshVisibility();
            return true;
        }

        private Bar AdoptBar(string childName)
        {
            Transform root = transform.Find(childName);
            if (root == null) return null;

            Transform track = root.Find("Track");
            if (track == null) return null;

            Transform fill = track.Find("Fill");
            Transform lag = track.Find("Lag");
            if (fill == null || lag == null) return null;

            Transform value = root.Find("Value");

            // 靠锚点判断是不是镜像布局（右条锚在右上）
            var rootRect = root as RectTransform;
            bool mirrored = rootRect != null && rootRect.anchorMin.x > 0.5f;

            return new Bar
            {
                Mirrored = mirrored,
                Fill = fill as RectTransform,
                Lag = lag as RectTransform,
                Value = value != null ? value.GetComponent<Text>() : null,
            };
        }

        private Bar BuildBar(Team team, bool mirrored, Color fillColor, string label, Font font)
        {
            var rootGo = new GameObject(mirrored ? "HealthBar_Player2" : "HealthBar_Player1",
                                        typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);

            var root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(mirrored ? 1f : 0f, 1f);
            root.anchorMax = root.anchorMin;
            root.pivot = new Vector2(mirrored ? 1f : 0f, 1f);
            root.sizeDelta = new Vector2(BarWidth, BarHeight);
            root.anchoredPosition = new Vector2(mirrored ? -ScreenMargin : ScreenMargin, -ScreenMargin);

            // 底板
            Image plate = CreateImage(root, "Plate", PlateColor);
            Stretch(plate.rectTransform);

            // 血槽
            Image track = CreateImage(root, "Track", TrackColor);
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.sizeDelta = new Vector2(-TrackInset * 2f, TrackHeight);
            trackRect.anchoredPosition = new Vector2(0f, TrackBottom);

            // 残影条先建 → 渲染在下面，快速条盖住它的左（右）侧
            Image lag = CreateImage(trackRect, "Lag", LagColor);
            Image fill = CreateImage(trackRect, "Fill", fillColor);

            // 名字与数值：左右镜像，让两边朝屏幕内侧对齐
            Text nameText = CreateText(root, "Name", label, font, 24, LabelColor,
                                       mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(mirrored ? 1f : 0f, 1f);
            nameRect.anchorMax = nameRect.anchorMin;
            nameRect.pivot = nameRect.anchorMin;
            nameRect.sizeDelta = new Vector2(300f, 34f);
            nameRect.anchoredPosition = new Vector2(mirrored ? -TrackInset : TrackInset, -34f);

            Text valueText = CreateText(root, "Value", "100 / 100", font, 24, LabelColor,
                                        mirrored ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(mirrored ? 0f : 1f, 1f);
            valueRect.anchorMax = valueRect.anchorMin;
            valueRect.pivot = valueRect.anchorMin;
            valueRect.sizeDelta = new Vector2(220f, 34f);
            valueRect.anchoredPosition = new Vector2(mirrored ? TrackInset : -TrackInset, -34f);

            var bar = new Bar
            {
                Mirrored = mirrored,
                Fill = fill.rectTransform,
                Lag = lag.rectTransform,
                Value = valueText,
            };

            SetRatio(bar.Fill, mirrored, 1f);
            SetRatio(bar.Lag, mirrored, 1f);
            return bar;
        }

        private static void SetRatio(RectTransform rect, bool mirrored, float ratio)
        {
            if (rect == null) return;

            ratio = Mathf.Clamp01(ratio);

            if (mirrored)
            {
                rect.anchorMin = new Vector2(1f - ratio, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(ratio, 1f);
            }

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font,
                                       int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 字体来源优先级：Inspector 指定 → 场景里已有的 Text（UICanvas 用的中文字体）
        /// → 系统中文字体。项目没有 Resources 目录，所以不能用 Resources.Load。
        /// </summary>
        private Font ResolveFont()
        {
            if (fontOverride != null) return fontOverride;

            Text[] texts = FindObjectsByType<Text>(FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null) return texts[i].font;
            }

            string[] candidates =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun",
                "Noto Sans CJK SC", "PingFang SC", "Arial",
            };

            Font osFont = Font.CreateDynamicFontFromOSFont(candidates, 24);
            if (osFont != null) return osFont;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
