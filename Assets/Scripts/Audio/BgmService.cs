using System.Collections;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 背景音乐播放器：读 <see cref="BgmTable"/>，按轨道播放循环音乐并做淡入淡出。
    ///
    /// 用法：每个场景放一个（或做成预制体拖进去），Inspector 里选 Track = Start / Battle。
    /// 淡入淡出用 unscaledTime，暂停（timeScale = 0）时音乐音量也能正常过渡。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BgmService : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("BGM 配置表；为空则什么都不播")]
        [SerializeField] private BgmTable table;

        [Tooltip("本场景播放哪条轨道")]
        [SerializeField] private BgmTrack track = BgmTrack.Start;

        [Tooltip("进入场景后自动播放")]
        [SerializeField] private bool autoPlay = true;

        [Header("战斗结束")]
        [Tooltip("战斗结束时自动切到结算曲（表里 Result 行有 clip 才生效）")]
        [SerializeField] private bool switchToResultOnBattleEnd = true;

        [SerializeField] private BgmTrack resultTrack = BgmTrack.Result;

        [Header("调试")]
        [SerializeField] private bool logPlayback = true;

        [SerializeField] private AudioSource source;

        private Coroutine fadeRoutine;
        private BgmTable.Row current;
        public BgmTrack CurrentTrack => current != null ? current.track : BgmTrack.None;
        public bool IsPlaying => source != null && source.isPlaying;

        private void Awake()
        {
            EnsureSource();
        }

        private void Start()
        {
            if (autoPlay) Play(track);
        }

        private void OnEnable()
        {
            GameEvents.BattleEnded += HandleBattleEnded;
        }

        private void OnDisable()
        {
            GameEvents.BattleEnded -= HandleBattleEnded;
        }

        private void HandleBattleEnded(BattleResult result)
        {
            if (!switchToResultOnBattleEnd) return;
            if (table == null) return;

            BgmTable.Row row = table.Find(resultTrack);
            if (row == null || row.clip == null) return;   // 没配结算曲就保持原样

            Play(resultTrack);
        }

        /// <summary>切换到指定轨道；同一轨道重复调用不会重启。</summary>
        public void Play(BgmTrack next)
        {
            BgmTable.Row row = table != null ? table.Find(next) : null;

            if (row == null || row.clip == null)
            {
                // 这条轨道没配音乐：把当前音乐淡出掉，别留着上一个场景的
                Stop(current != null ? current.fadeOut : 0.4f);
                return;
            }

            if (current == row && source != null && source.isPlaying) return;

            EnsureSource();

            float fadeOut = current != null ? current.fadeOut : 0.3f;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);

            current = row;
            fadeRoutine = StartCoroutine(SwitchRoutine(row, fadeOut));

            if (logPlayback)
                Debug.Log($"[Bgm] {next} -> {row.clip.name}（音量 {row.volume:0.##}，循环 {row.loop}）", this);
        }

        /// <summary>淡出并停止。</summary>
        public void Stop(float fadeOut = -1f)
        {
            if (source == null || !source.isPlaying) return;

            if (fadeOut < 0f) fadeOut = current != null ? current.fadeOut : 0.4f;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(StopRoutine(fadeOut));
        }

        private IEnumerator SwitchRoutine(BgmTable.Row row, float fadeOut)
        {
            // 先淡出旧曲（如果有）
            if (source.isPlaying && source.volume > 0.001f)
            {
                float from = source.volume;
                float t = 0f;
                while (t < fadeOut)
                {
                    t += Time.unscaledDeltaTime;
                    source.volume = Mathf.Lerp(from, 0f, fadeOut <= 0f ? 1f : t / fadeOut);
                    yield return null;
                }
            }

            source.Stop();
            source.clip = row.clip;
            source.loop = row.loop;
            source.volume = 0f;

            source.Play();

            float elapsed = 0f;
            while (elapsed < row.fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, row.volume, row.fadeIn <= 0f ? 1f : elapsed / row.fadeIn);
                yield return null;
            }

            source.volume = row.volume;
            fadeRoutine = null;
        }

        private IEnumerator StopRoutine(float fadeOut)
        {
            float from = source.volume;
            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, 0f, fadeOut <= 0f ? 1f : t / fadeOut);
                yield return null;
            }

            source.volume = 0f;
            source.Stop();
            current = null;
            fadeRoutine = null;
        }

        private void EnsureSource()
        {
            if (source != null) return;

            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;          // 纯 2D
            source.volume = 0f;
            source.ignoreListenerPause = true; // 暂停时不要被 AudioListener 掐掉
        }
    }
}
