namespace NineDays
{
    /// <summary>战斗中的一方（角色或测试替身）；放在角色层，避免 Character 反向依赖 Battle。</summary>
    public interface IBattleParticipant
    {
        Team Team { get; }
        float CurrentHealth { get; }
        bool IsDefeated { get; }
    }
}
