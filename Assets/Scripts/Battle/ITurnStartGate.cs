using System;

namespace NineDays
{
    /// <summary>
    /// 回合开始门：BattleController 在真正给角色操作权之前调用。
    /// 选弹、抽卡等前置流程通过实现该接口插入。
    /// </summary>
    public interface ITurnStartGate
    {
        void BeginTurn(Team actor, Action onReady);
        void Cancel();
    }
}
