using UnityEngine;

namespace NineDays
{
    /// <summary>编辑场景入口：人工摆层，Bake 成 ArenaConfig；运行时不使用。</summary>
    public class ArenaAuthoring : MonoBehaviour
    {
        [Header("目标数据")]
        public ArenaConfig config;

        [Header("层")]
        public Transform layersRoot;

        [Header("场地")]
        public Rect worldRect = new Rect(-8f, -5f, 16f, 10f);

        [Min(1)] public int pixelsPerUnit = 16;
    }
}
