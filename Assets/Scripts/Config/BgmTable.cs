using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>BGM 轨道标识。Start / Battle 按场景切，Result 在战斗结束时切。</summary>
    public enum BgmTrack
    {
        None = 0,
        Start = 1,
        Battle = 2,
        Result = 3,
    }

    /// <summary>
    /// BGM 配置表：一行一条循环音乐（哪条轨道、放哪个 clip、多大音量、淡入淡出多久）。
    /// 场景里挂一个 <see cref="BgmService"/>，在 Inspector 上选轨道即可，不用改代码。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/BGM 表", fileName = "BgmTable")]
    public class BgmTable : ScriptableObject
    {
        [Serializable]
        public class Row
        {
            [Tooltip("这条音乐用在哪个场景（Start = 开始界面，Battle = 战斗）")]
            public BgmTrack track = BgmTrack.Start;

            [Tooltip("循环音乐文件；留空则这条轨道静音")]
            public AudioClip clip;

            [Range(0f, 1f)]
            [Tooltip("这条音乐的基准音量；总音量还会再乘 AudioSource / AudioListener")]
            public float volume = 0.55f;

            [Tooltip("是否循环。BGM 一般是勾上的")]
            public bool loop = true;

            [Min(0f)]
            [Tooltip("进场淡入时长（秒）")]
            public float fadeIn = 1.0f;

            [Min(0f)]
            [Tooltip("切走 / 停止时的淡出时长（秒）")]
            public float fadeOut = 0.6f;
        }

        [SerializeField] private List<Row> rows = new List<Row>();

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>取一条轨道对应的行；没有就新建一条（编辑器里生成表和运行期扩表都用它）。</summary>
        public Row GetOrAddRow(BgmTrack track)
        {
            Row row = Find(track);
            if (row != null) return row;

            row = new Row { track = track };
            rows.Add(row);
            return row;
        }

        /// <summary>按轨道查行；没有对应行或 clip 为空都返回 null（调用方按静音处理）。</summary>
        public Row Find(BgmTrack track)
        {
            if (rows == null) return null;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && rows[i].track == track) return rows[i];
            }

            return null;
        }
    }
}
