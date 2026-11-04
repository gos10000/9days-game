using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 玩家设置的默认值资产。只读，玩家实际值由 ISettingsStore 保存到本地文件。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/设置/玩家默认设置", fileName = "PlayerSettingsDefaults")]
    public sealed class PlayerSettingsDefaults : ScriptableObject
    {
        [SerializeField] private PlayerSettingsData values = new PlayerSettingsData();

        public PlayerSettingsData Values => values;

        /// <summary>返回默认值副本，调用方修改它不会影响资产。</summary>
        public PlayerSettingsData CreateDefault()
        {
            return values != null ? values.Clone() : new PlayerSettingsData();
        }

        /// <summary>供编辑器生成器与测试写入默认值；运行时应只读。</summary>
        public void SetValues(PlayerSettingsData data)
        {
            values = data != null ? data.Clone() : new PlayerSettingsData();
        }

        private void OnValidate()
        {
            if (values == null) values = new PlayerSettingsData();
        }
    }
}
