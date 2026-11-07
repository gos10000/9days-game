using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 开始页控制器：负责页面状态（待机 → 进入 / 退出）、淡出与场景切换。
    /// 只处理页面表现，不参与投球与回合；投球由 CharacterLauncher / TurnController 负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartScene : MonoBehaviour
    {
        public enum StartPageState
        {
            Idle = 0,
            Starting = 1,
            Exiting = 2
        }

        [Header("页面")]
        [Tooltip("整页淡出用的 CanvasGroup；留空自动取同节点")]
        [SerializeField] private CanvasGroup pageGroup;

        [Tooltip("可选：显示当前页面状态")]
        [SerializeField] private Text statusText;
        [SerializeField] private string idleText = "拖动并松开发射";
        [SerializeField] private string startingText = "正在进入战斗…";
        [SerializeField] private string exitingText = "正在退出…";

        [Tooltip("页面淡出时长（秒）")]
        [Min(0f)]
        [SerializeField] private float fadeSeconds = 0.35f;

        [Header("进入")]
        [Tooltip("要加载的战斗场景名；需在 Build Settings 中")]
        [SerializeField] private string battleSceneName = "Test_BattleScene";

        [Tooltip("命中开始盒后通过 UIPageRouter 打开的页面 id；为空则回退到加载场景")]
        [SerializeField] private string nextPageId = "Placeholder";

        [Tooltip("true = ReplaceScreen（替换当前页）；false = ShowScreen（压栈）")]
        [SerializeField] private bool replaceCurrentPage = true;

        [Tooltip("页面不可用时是否回退到加载战斗场景")]
        [SerializeField] private bool loadSceneWhenPageMissing = true;

        [Header("退出")]
        [Tooltip("淡出后延迟多久退出（秒）")]
        [Min(0f)]
        [SerializeField] private float quitDelay = 0.1f;

        public static StartScene Instance { get; private set; }

        /// <summary>页面状态变化；表现层可订阅。</summary>
        public event Action<StartPageState> PageUpdated;

        public StartPageState State { get; private set; } = StartPageState.Idle;
        public bool IsLeaving => State != StartPageState.Idle;

        private void Awake()
        {
            Instance = this;
            if (pageGroup == null) pageGroup = GetComponent<CanvasGroup>();
            UpdatePage(StartPageState.Idle);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>开始盒命中：优先用 UIPageRouter 切换页面；不可用时回退到加载战斗场景。</summary>
        public void RequestStart()
        {
            if (IsLeaving) return;
            if (TrySwitchPage()) return;

            UpdatePage(StartPageState.Starting);

            if (!loadSceneWhenPageMissing)
            {
                Debug.LogError($"[StartScene] 页面 {nextPageId} 不可用，且未开启场景回退");
                UpdatePage(StartPageState.Idle);
                return;
            }

            StartCoroutine(StartRoutine());
        }

        /// <summary>按 nextPageId 切换 UI 页面；成功返回 true。</summary>
        private bool TrySwitchPage()
        {
            if (string.IsNullOrWhiteSpace(nextPageId)) return false;

            UIRoot root = UIRoot.Instance;
            if (root == null || root.Router == null)
            {
                Debug.LogWarning("[StartScene] 场景里没有 UIRoot，无法切换页面");
                return false;
            }

            if (root.Catalog == null || !root.Catalog.TryGet(nextPageId, out UIPageConfig config) || config == null)
            {
                Debug.LogWarning($"[StartScene] 页面 {nextPageId} 未注册");
                return false;
            }

            if (config.Prefab == null)
            {
                Debug.LogWarning($"[StartScene] 页面 {nextPageId} 没有绑定 prefab");
                return false;
            }

            UIPageHandle handle = replaceCurrentPage
                ? root.Router.ReplaceScreen(nextPageId)
                : root.Router.ShowScreen(nextPageId);

            if (handle == null)
            {
                Debug.LogWarning($"[StartScene] 页面切换失败：{nextPageId}");
                return false;
            }

            Debug.Log($"[StartScene] 已切换页面：{nextPageId}");
            return true;
        }

        /// <summary>退出盒命中：页面切到「退出中」并淡出，随后退出游戏。</summary>
        public void RequestExit()
        {
            if (IsLeaving) return;
            UpdatePage(StartPageState.Exiting);
            StartCoroutine(ExitRoutine());
        }

        /// <summary>编辑器内停止 Play，构建后退出应用。</summary>
        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdatePage(StartPageState state)
        {
            State = state;

            if (pageGroup != null)
            {
                pageGroup.interactable = state == StartPageState.Idle;
                pageGroup.blocksRaycasts = state == StartPageState.Idle;
            }

            if (statusText != null)
            {
                if (state == StartPageState.Starting) statusText.text = startingText;
                else if (state == StartPageState.Exiting) statusText.text = exitingText;
                else statusText.text = idleText;
            }

            PageUpdated?.Invoke(state);
        }

        private IEnumerator StartRoutine()
        {
            yield return FadeOut(true);

            if (string.IsNullOrWhiteSpace(battleSceneName))
            {
                Debug.LogError("[StartScene] 未配置战斗场景名，无法开始游戏");
                yield break;
            }

            SceneManager.LoadScene(battleSceneName);
        }

        private IEnumerator ExitRoutine()
        {
            yield return FadeOut(false);
            if (quitDelay > 0f) yield return new WaitForSecondsRealtime(quitDelay);
            QuitApplication();
        }

        /// <summary>按 unscaled 时间把页面淡到全透明；同时锁住页面交互。</summary>
        private IEnumerator FadeOut(bool blockInput)
        {
            if (pageGroup != null)
            {
                pageGroup.interactable = false;
                pageGroup.blocksRaycasts = blockInput;

                float startAlpha = pageGroup.alpha;
                float elapsed = 0f;

                while (elapsed < fadeSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    pageGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                    yield return null;
                }

                pageGroup.alpha = 0f;
            }
            else if (fadeSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(fadeSeconds);
            }
        }
    }
}
