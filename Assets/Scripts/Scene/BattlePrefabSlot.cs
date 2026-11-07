using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 通用 prefab 槽位：背景和场地共用，只负责实例化与销毁。
    /// 槽位不修改 prefab 的排序、颜色和碰撞。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattlePrefabSlot : MonoBehaviour
    {
        [Tooltip("实例化父节点；留空时挂在自身下")]
        [SerializeField] private Transform root;

        public Transform Root => root != null ? root : transform;
        public GameObject CurrentPrefab { get; private set; }
        public GameObject Instance { get; private set; }

        /// <summary>应用 prefab；相同 prefab 且实例有效时直接复用。</summary>
        public void Apply(GameObject prefab)
        {
            if (prefab == CurrentPrefab && Instance != null) return;

            Clear();
            CurrentPrefab = prefab;
            if (prefab == null) return;

            Instance = Instantiate(prefab, Root, false);
            Instance.name = prefab.name;
        }

        /// <summary>销毁当前实例；先失活，避免同帧新旧对象重叠。</summary>
        public void Clear()
        {
            if (Instance != null)
            {
                Instance.SetActive(false);
                Destroy(Instance);
            }

            Instance = null;
            CurrentPrefab = null;
        }
    }
}
