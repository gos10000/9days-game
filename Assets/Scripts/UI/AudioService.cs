using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 全局音效播放器：按钮悬停、子弹脱手、子弹爆炸、放置道具。
    ///
    /// 按钮悬停不靠给每个按钮挂脚本——那样运行时动态生成的卡片、以及以后再加的按钮都得手动补一遍。
    /// 这里每帧（只在指针移动时）用 EventSystem 射线找出指针下的 Selectable，目标变了就播一次，
    /// 所以暂停面板、结算面板、停止键、选卡卡片全部自动覆盖。
    ///
    /// 音效内容全部来自 <see cref="AudioTable"/>，代码里不写死任何 clip。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("音效配置表；为空则完全不发声")]
        [SerializeField] private AudioTable table;

        [Tooltip("战斗音效（发射 / 爆炸 / 放置道具）；为空自动补一个 2D AudioSource")]
        [SerializeField] private AudioSource sfxSource;

        [Tooltip("界面音效（按钮悬停）；为空自动补一个 2D AudioSource")]
        [SerializeField] private AudioSource uiSource;

        [Header("开关")]
        [Tooltip("关掉后不再检测按钮悬停，其它音效不受影响")]
        [SerializeField] private bool hoverSounds = true;

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);

        private PointerEventData pointerData;
        private Vector2 lastPointer = new Vector2(float.NaN, float.NaN);
        private GameObject lastHovered;

        private void Awake()
        {
            if (sfxSource == null) sfxSource = EnsureSource("SFX");
            if (uiSource == null) uiSource = EnsureSource("UI");
        }

        private void OnEnable()
        {
            GameEvents.ShotFired += HandleShotFired;
            GameEvents.PropPlaced += HandlePropPlaced;
            GameEvents.PropCleared += HandlePropCleared;
            GameEvents.TractionPulled += HandleTractionPulled;
            GameEvents.BurstTriggered += HandleBurstTriggered;
        }

        private void OnDisable()
        {
            GameEvents.ShotFired -= HandleShotFired;
            GameEvents.PropPlaced -= HandlePropPlaced;
            GameEvents.PropCleared -= HandlePropCleared;
            GameEvents.TractionPulled -= HandleTractionPulled;
            GameEvents.BurstTriggered -= HandleBurstTriggered;

            lastHovered = null;
            lastPointer = new Vector2(float.NaN, float.NaN);
        }

        private void Update()
        {
            UpdateHoverSound();
        }

        // ---------------- 按钮悬停 ----------------

        private void UpdateHoverSound()
        {
            if (!hoverSounds || table == null || table.hoverClick == null) return;

            EventSystem system = EventSystem.current;
            if (system == null) return;

            Vector2 pointer = GameInput.Current.PointerScreen;
            if (pointer == lastPointer) return;     // 指针没动，不重复判定
            lastPointer = pointer;

            GameObject hovered = FindSelectableUnder(system, pointer);
            if (hovered == lastHovered) return;

            lastHovered = hovered;
            if (hovered != null) Play(table.hoverClick, table.hoverVolume, uiSource);
        }

        /// <summary>返回指针正下方第一个「可交互的按钮」；命中不可交互的按钮则视为没有悬停。</summary>
        private GameObject FindSelectableUnder(EventSystem system, Vector2 pointer)
        {
            if (pointerData == null) pointerData = new PointerEventData(system);
            pointerData.position = pointer;

            raycastResults.Clear();
            system.RaycastAll(pointerData, raycastResults);

            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject hit = raycastResults[i].gameObject;
                if (hit == null) continue;

                Selectable selectable = hit.GetComponentInParent<Selectable>();
                if (selectable == null) continue;
                if (!selectable.IsInteractable()) return null;

                return selectable.gameObject;
            }

            return null;
        }

        // ---------------- 战斗音效 ----------------

        private void HandleShotFired(Character shooter, Projectile projectile)
        {
            if (table == null) return;
            Play(table.throwClip, table.throwVolume, sfxSource);
        }

        private void HandlePropPlaced(PropSpec spec, GameObject instance)
        {
            if (table == null || !spec.IsValid) return;

            AudioTable.PropSound row = table.FindProp(spec.Prefab);
            if (row == null) return;

            Play(row.clip, row.volume, sfxSource);
        }

        /// <summary>消除类子弹清掉一个道具：清几个就响几次。</summary>
        private void HandlePropCleared(GameObject instance)
        {
            if (table == null) return;
            Play(table.clearClip, table.clearVolume, sfxSource);
        }

        /// <summary>牵引弹每牵引一次响一次。</summary>
        private void HandleTractionPulled(GameObject zone)
        {
            if (table == null) return;
            Play(table.tractionClip, table.tractionVolume, sfxSource);
        }

        private void HandleBurstTriggered(Projectile projectile, BurstDefinition definition, BurstContext context)
        {
            if (table == null) return;

            // 两种爆炸要出声：
            // 1. 子弹自己撞出来的（projectile 不为空 + OnImpact）；
            // 2. 牵引区整段结束时的收尾爆炸（projectile 为空 + OnExpire）。
            bool bulletImpact = projectile != null && context.Trigger == BurstTriggerType.OnImpact;
            bool zoneFinalBurst = projectile == null && context.Trigger == BurstTriggerType.OnExpire;
            if (!bulletImpact && !zoneFinalBurst) return;

            // 牵引弹命中的那一下只是「架起牵引区」，不是真爆炸：
            // 它的爆炸声留到牵引结束时再播，否则一次牵引会响两声。
            if (bulletImpact && SpawnsTractionZone(definition)) return;

            Play(table.burstClip, table.burstVolume, sfxSource);
        }

        /// <summary>这次爆发是否只是用来生成牵引区（效果列表里有 TractionEffect）。</summary>
        private static bool SpawnsTractionZone(BurstDefinition definition)
        {
            if (definition == null || definition.effects == null) return false;

            for (int i = 0; i < definition.effects.Count; i++)
            {
                if (definition.effects[i] is TractionEffect) return true;
            }

            return false;
        }

        // ---------------- 工具 ----------------

        private void Play(AudioClip clip, float volume, AudioSource source)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private AudioSource EnsureSource(string sourceName)
        {
            var go = new GameObject("AudioSource_" + sourceName);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;       // 纯 2D，不吃相机位置
            source.volume = 1f;
            source.ignoreListenerPause = true;

            return source;
        }
    }
}
