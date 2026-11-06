using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 关卡数据载体：背景 prefab、场地 prefab 与双方出生点。
    /// 只挂在关卡 prefab 根节点，不参与运行时状态。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleLevel : MonoBehaviour
    {
        [Header("背景 / 场地")]
        [Tooltip("背景预制体；只放表现层，不参与物理")]
        public GameObject backgroundPrefab;

        [Tooltip("场地预制体；地形视觉与碰撞都放在 prefab 里")]
        public GameObject arenaPrefab;

        [Header("角色初始位置（世界坐标）")]
        [Tooltip("Player1 战斗开始时的位置")]
        public Vector2 player1Spawn = new Vector2(-5f, -3.7f);

        [Tooltip("Player2 战斗开始时的位置")]
        public Vector2 player2Spawn = new Vector2(5f, -3.7f);

        public GameObject BackgroundPrefab => backgroundPrefab;
        public GameObject ArenaPrefab => arenaPrefab;
        public Vector2 Player1Spawn => player1Spawn;
        public Vector2 Player2Spawn => player2Spawn;
    }
}
