namespace NineDays
{
    /// <summary>
    /// 全局暂停开关。UI 层（PauseMenu）负责写入，逻辑层（CharacterLauncher 等）只读取。
    /// 放在 Game 程序集里，是为了让逻辑层判断暂停时不必反向依赖 NineDays.UI。
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; set; }
    }
}
