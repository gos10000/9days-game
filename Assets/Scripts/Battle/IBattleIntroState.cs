namespace NineDays
{
    /// <summary>
    /// 开场镜头状态。流程层只依赖这个接口，不引用相机程序集：
    /// 镜头还没演完时，抽选流程先等着，避免 UI 压在特写上。
    /// </summary>
    public interface IBattleIntroState
    {
        bool IntroPlaying { get; }
    }
}
