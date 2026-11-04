using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>UI 根节点：创建 Canvas 五层、EventSystem，并持有 Router。</summary>
    [DisallowMultipleComponent]
    public sealed class UIRoot : MonoBehaviour
    {
        [Header("页面")]
        [SerializeField] private UIPageCatalog catalog;
        [SerializeField] private string startupPageId = string.Empty;

        [Header("输入")]
        [SerializeField] private bool handleBackInput = true;

        [Header("Canvas")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private int canvasSortingOrder = 100;

        [Header("生命周期")]
        [SerializeField] private bool dontDestroyOnLoad;

        private readonly Dictionary<UIPageLayer, RectTransform> layers =
            new Dictionary<UIPageLayer, RectTransform>();

        private Canvas canvas;
        private GameObject createdEventSystem;
        private bool initialized;

        public static UIRoot Instance { get; private set; }
        public UIPageCatalog Catalog => catalog;
        public UIPageRouter Router { get; private set; }
        public int LayerCount => layers.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void Start()
        {
            if (Router != null && !string.IsNullOrWhiteSpace(startupPageId))
                Router.ShowScreen(startupPageId);
        }

        private void Update()
        {
            if (!handleBackInput || Router == null || !Router.CanGoBack) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Router.Back();
        }

        /// <summary>场景/测试配置入口；可重复调用刷新 catalog。</summary>
        public void Configure(UIPageCatalog pageCatalog, string startupPage = null)
        {
            catalog = pageCatalog;
            if (startupPage != null) startupPageId = startupPage;
            EnsureInitialized();
        }

        public RectTransform GetLayer(UIPageLayer layer)
        {
            return layers.TryGetValue(layer, out RectTransform root) ? root : null;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                initialized = true;
                BuildCanvas();
                EnsureEventSystem();
                Router = GetComponent<UIPageRouter>();
                if (Router == null) Router = gameObject.AddComponent<UIPageRouter>();
            }

            Router.Configure(catalog, layers);
        }

        private void BuildCanvas()
        {
            canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                        typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                go.layer = LayerMask.NameToLayer("UI");
                canvas = go.GetComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            foreach (UIPageLayer layer in System.Enum.GetValues(typeof(UIPageLayer)))
                EnsureLayer(layer);
        }

        private void EnsureLayer(UIPageLayer layer)
        {
            if (layers.TryGetValue(layer, out RectTransform existing) && existing != null) return;

            var go = new GameObject(layer + "Layer", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.layer = LayerMask.NameToLayer("UI");

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            layers[layer] = rect;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            createdEventSystem = new GameObject("EventSystem", typeof(EventSystem),
                                                 typeof(InputSystemUIInputModule));
            createdEventSystem.transform.SetParent(transform, false);
            var module = createdEventSystem.GetComponent<InputSystemUIInputModule>();
            if (module != null) module.AssignDefaultActions();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (createdEventSystem != null) Destroy(createdEventSystem);
        }
    }
}
