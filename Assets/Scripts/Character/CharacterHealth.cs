using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 角色血量（血量制战斗）：受击扣血，归零进入 Defeated 并广播事件。
    /// 与 Character 挂在同一个根节点。
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class CharacterHealth : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Character character;
        [SerializeField] private CharacterConfig config;

        [Header("运行时")]
        [SerializeField] private float current;

        public float Max => config != null ? Mathf.Max(1f, config.maxHealth) : 100f;
        public float Current => current;
        public bool IsDefeated => current <= 0f;

        private void Awake()
        {
            if (character == null) character = GetComponent<Character>();
            if (config == null && character != null) config = character.Config;

            current = Max;
        }

        /// <summary>战斗开始时回满血。</summary>
        public void ResetHealth()
        {
            current = Max;
            GameEvents.RaiseCharacterHealthChanged(character, current, Max);
        }

        /// <summary>扣血；归零时切换到 Defeated 并广播。</summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDefeated) return;

            current = Mathf.Max(0f, current - amount);
            GameEvents.RaiseCharacterHealthChanged(character, current, Max);

            if (current > 0f) return;

            if (character != null) character.EnterDefeated();
            GameEvents.RaiseCharacterDefeated(character);
        }
    }
}
