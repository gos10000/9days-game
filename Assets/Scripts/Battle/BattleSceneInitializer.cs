using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 战斗场景初始化：读取关卡/测试覆盖，应用背景与场地 prefab，并写入双方出生点。
    /// 没有关卡选择 UI 时，level 就是默认关卡，可在 Inspector 直接替换。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleSceneInitializer : MonoBehaviour
    {
        [Header("关卡")]
        [Tooltip("默认关卡预制体；没有关卡选择 UI 时使用")]
        [SerializeField] private BattleLevel level;

        [Tooltip("测试覆盖：非空时优先于默认关卡")]
        [SerializeField] private BattleLevel levelOverride;

        [Header("测试覆盖（属性板）")]
        [Tooltip("非空时覆盖关卡背景")]
        [SerializeField] private GameObject backgroundOverride;

        [Tooltip("非空时覆盖关卡场地")]
        [SerializeField] private GameObject arenaOverride;

        [Header("场景槽位")]
        [Tooltip("背景实例化根；留空自动创建")]
        [SerializeField] private BattlePrefabSlot backgroundSlot;

        [Tooltip("场地实例化根；留空自动创建")]
        [SerializeField] private BattlePrefabSlot arenaSlot;

        [Header("角色")]
        [Tooltip("用于取双方 Character；留空自动查找")]
        [SerializeField] private TurnController turnController;

        public BattleLevel Level => level;
        public BattleLevel ActiveLevel { get; private set; }
        public GameObject ActiveBackground { get; private set; }
        public GameObject ActiveArena { get; private set; }
        public bool HasArenaPrefab => ActiveArena != null;

        public BattlePrefabSlot BackgroundSlot
        {
            get
            {
                EnsureSlots();
                return backgroundSlot;
            }
        }

        public BattlePrefabSlot ArenaSlot
        {
            get
            {
                EnsureSlots();
                return arenaSlot;
            }
        }

        private void Awake()
        {
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
            EnsureSlots();
        }

        /// <summary>应用当前关卡与测试覆盖；返回是否应用了任何关卡/覆盖。</summary>
        public bool Apply()
        {
            EnsureSlots();
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();

            BattleLevel resolved = levelOverride != null ? levelOverride : level;
            ActiveLevel = resolved;
            ActiveBackground = backgroundOverride != null
                ? backgroundOverride
                : (resolved != null ? resolved.backgroundPrefab : null);
            ActiveArena = arenaOverride != null
                ? arenaOverride
                : (resolved != null ? resolved.arenaPrefab : null);

            backgroundSlot.Apply(ActiveBackground);
            arenaSlot.Apply(ActiveArena);

            if (resolved != null)
            {
                ApplySpawnPoint(Team.Player1, resolved.player1Spawn);
                ApplySpawnPoint(Team.Player2, resolved.player2Spawn);
            }

            return resolved != null || ActiveBackground != null || ActiveArena != null;
        }

        /// <summary>设置默认关卡并立即应用；供未来选关流程使用。</summary>
        public bool ApplyLevel(BattleLevel value)
        {
            level = value;
            return Apply();
        }

        /// <summary>设置测试用关卡覆盖；下次 Apply 生效。</summary>
        public void SetLevelOverride(BattleLevel value)
        {
            levelOverride = value;
        }

        /// <summary>设置背景/场地覆盖；下次 Apply 生效。</summary>
        public void SetOverrides(GameObject background, GameObject arena)
        {
            backgroundOverride = background;
            arenaOverride = arena;
        }

        /// <summary>把关卡出生点写给角色；真正移动由 ResetForBattle 完成。</summary>
        private void ApplySpawnPoint(Team team, Vector2 position)
        {
            Character character = turnController != null ? turnController.GetCharacter(team) : null;
            if (character == null)
            {
                Debug.LogWarning($"[BattleScene] 缺少 {team} 角色，无法设置出生点");
                return;
            }

            character.SetSpawnPoint(new Vector3(position.x, position.y, 0f));
        }

        /// <summary>确保背景/场地槽位存在；未手动配置时自动创建。</summary>
        private void EnsureSlots()
        {
            if (backgroundSlot == null) backgroundSlot = CreateSlot("BackgroundRoot");
            if (arenaSlot == null) arenaSlot = CreateSlot("ArenaRoot");
        }

        /// <summary>创建子物体并挂 BattlePrefabSlot。</summary>
        private BattlePrefabSlot CreateSlot(string slotName)
        {
            var go = new GameObject(slotName);
            go.transform.SetParent(transform, false);
            // 背景/场地 prefab 统一按世界原点实例化，不受父节点偏移/旋转影响
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.AddComponent<BattlePrefabSlot>();
        }
    }
}
