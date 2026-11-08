using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 结算画面：战斗结束时显示胜负图案（手绘 WINNER / DRAW 逐帧抖动），下方给出「重新开始 / 退出」。
    ///
    /// 面板默认隐藏，只订阅 GameEvents，不参与任何战斗逻辑。
    /// 两个按钮直接复用暂停面板的 PauseMenu 方法，保证行为完全一致
    /// （重新开始会先恢复 timeScale，退出会先恢复时间再切场景）。
    ///
    /// 图案来自 Assets/Art/End 的两张精灵表（各 6 帧），按 framesPerSecond 循环播放；
    /// 结算时 timeScale 通常是 0，所以用 WaitForSecondsRealtime 计时，不受暂停影响。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleResultView : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("整块结算内容；默认隐藏，战斗结束时显示")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("显示胜负图案的 Image")]
        [SerializeField] private Image resultImage;

        [SerializeField] private Button restartButton;
        [SerializeField] private Button exitButton;

        [Tooltip("留空则自动查找场景里的 PauseMenu")]
        [SerializeField] private PauseMenu pauseMenu;

        [Header("图案")]
        [Tooltip("获胜时循环播放的帧，按数组顺序播放（win_0 → win_5）")]
        [SerializeField] private Sprite[] winSprites;

        [Tooltip("平局时循环播放的帧（draw_0 → draw_5）")]
        [SerializeField] private Sprite[] drawSprites;

        [Tooltip("每秒切换几帧；10 左右比较像手绘线条在抖动")]
        [Min(1f)]
        [SerializeField] private float framesPerSecond = 10f;

        private Coroutine cycleRoutine;

        private void Awake()
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();

            if (restartButton != null) restartButton.onClick.AddListener(RestartBattle);
            if (exitButton != null) exitButton.onClick.AddListener(ExitToStart);

            Hide();
        }

        private void OnEnable()
        {
            GameEvents.BattleEnded += OnBattleEnded;
            GameEvents.BattleStarted += OnBattleStarted;
        }

        private void OnDisable()
        {
            GameEvents.BattleEnded -= OnBattleEnded;
            GameEvents.BattleStarted -= OnBattleStarted;
            StopCycling();
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartBattle);
            if (exitButton != null) exitButton.onClick.RemoveListener(ExitToStart);
        }

        private void OnBattleEnded(BattleResult result)
        {
            if (panelRoot != null) panelRoot.SetActive(true);

            bool draw = result.Winner == Team.None;
            ShowFrames(draw ? drawSprites : winSprites, draw ? "DRAW" : "WINNER");
        }

        /// <summary>重开一局时收起来，避免盖住新一局的画面。</summary>
        private void OnBattleStarted(int rounds) => Hide();

        /// <summary>切到第一帧并开始循环播放。</summary>
        private void ShowFrames(Sprite[] frames, string label)
        {
            StopCycling();

            if (resultImage == null)
            {
                Debug.LogWarning("[BattleResultView] 没有挂 resultImage，结算图案无法显示", this);
                return;
            }

            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning($"[BattleResultView] {label} 的帧数组是空的，检查 Prefab 里的 winSprites / drawSprites", this);
                return;
            }

            resultImage.sprite = frames[0];
            if (frames.Length > 1 && isActiveAndEnabled) cycleRoutine = StartCoroutine(CycleFrames(frames));
        }

        /// <summary>按 framesPerSecond 在帧数组里循环。</summary>
        private IEnumerator CycleFrames(Sprite[] frames)
        {
            float interval = 1f / Mathf.Max(1f, framesPerSecond);
            int index = 0;

            while (true)
            {
                if (frames[index] != null) resultImage.sprite = frames[index];
                index = (index + 1) % frames.Length;
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        private void StopCycling()
        {
            if (cycleRoutine == null) return;

            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }

        private void Hide()
        {
            StopCycling();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void RestartBattle()
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (pauseMenu == null)
            {
                Debug.LogError("[BattleResultView] 找不到 PauseMenu，无法重新开始", this);
                return;
            }

            Hide();
            pauseMenu.RestartBattle();
        }

        private void ExitToStart()
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (pauseMenu == null)
            {
                Debug.LogError("[BattleResultView] 找不到 PauseMenu，无法退出", this);
                return;
            }

            pauseMenu.ReturnToStartScene();
        }
    }
}
