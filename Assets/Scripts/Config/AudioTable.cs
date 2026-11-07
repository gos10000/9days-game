using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 音效配置表：界面悬停、发射、爆炸三档固定音效，外加「放置某个道具时播哪个音」的对照表。
    /// 一张表就是全部音效来源，<see cref="AudioService"/> 只读它，不在代码里写死任何 clip。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/音效表", fileName = "AudioTable")]
    public class AudioTable : ScriptableObject
    {
        [Serializable]
        public class PropSound
        {
            [Tooltip("道具预制体（和 PropTable 里拖的是同一个资源）")]
            public GameObject prefab;

            [Tooltip("放下这个道具时播放的音效")]
            public AudioClip clip;

            [Range(0f, 1f)]
            public float volume = 0.9f;
        }

        [Header("界面")]
        [Tooltip("鼠标移进任意按钮时播放（暂停、结算、停止键、选卡卡片都算）")]
        public AudioClip hoverClick;

        [Range(0f, 1f)]
        public float hoverVolume = 0.45f;

        [Header("战斗")]
        [Tooltip("玩家松开鼠标把子弹丢出去时播放")]
        public AudioClip throwClip;

        [Range(0f, 1f)]
        public float throwVolume = 0.85f;

        [Tooltip("子弹撞击爆炸时播放")]
        public AudioClip burstClip;

        [Range(0f, 1f)]
        public float burstVolume = 0.9f;

        [Tooltip("消除类子弹清掉一个道具时播放（清几个响几次）")]
        public AudioClip clearClip;

        [Range(0f, 1f)]
        public float clearVolume = 0.8f;

        [Tooltip("牵引弹每牵引一次播放一次；整段牵引结束时再放 burstClip")]
        public AudioClip tractionClip;

        [Range(0f, 1f)]
        public float tractionVolume = 0.8f;

        [Header("道具（放置时）")]
        [Tooltip("按道具预制体逐个配置；没配的道具静音")]
        public List<PropSound> props = new List<PropSound>();

        /// <summary>按预制体找对应的放置音效；找不到返回 null（静音）。</summary>
        public PropSound FindProp(GameObject prefab)
        {
            if (prefab == null || props == null) return null;

            for (int i = 0; i < props.Count; i++)
            {
                PropSound row = props[i];
                if (row != null && row.prefab == prefab) return row;
            }

            return null;
        }
    }
}
