using UnityEngine;

namespace NineDays
{
    /// <summary>挂在编辑场景的层根节点上，Bake 时读取；运行时不会参与。</summary>
    public class TerrainLayerAuthoring : MonoBehaviour
    {
        [Header("层")]
        public string layerId = "Layer";

        [Header("破坏")]
        public bool destructible;
        [Min(0f)] public float requiredBreakPower;

        [Header("物理")]
        public bool collisionEnabled = true;

        [Header("遮罩")]
        [Range(0.01f, 0.99f)] public float alphaThreshold = 0.5f;
    }
}
