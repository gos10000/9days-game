using UnityEngine;

namespace NineDays
{
    /// <summary>全局输入门面：缓存当帧快照，并提供唯一的取消键消费入口。</summary>
    public static class GameInput
    {
        private static IInputSource source;
        private static InputSnapshot cachedSnapshot;
        private static int cachedFrame = -1;
        private static int cancelConsumedFrame = -1;

        /// <summary>当前帧快照；多个消费者在同一帧拿到同一份数据。</summary>
        public static InputSnapshot Current
        {
            get
            {
                int frame = Time.frameCount;
                if (cachedFrame != frame)
                {
                    cachedSnapshot = EnsureSource().Read();
                    cachedFrame = frame;
                }

                return cachedSnapshot;
            }
        }

        /// <summary>取消键一帧只能被一个消费者拿到；UI 应优先于角色消费。</summary>
        public static bool ConsumeCancel()
        {
            InputSnapshot snapshot = Current;
            if (!snapshot.CancelPressed) return false;
            if (cancelConsumedFrame == cachedFrame) return false;

            cancelConsumedFrame = cachedFrame;
            return true;
        }

        /// <summary>查询语义化调试动作；映射规则由输入源负责。</summary>
        public static bool WasPressed(GameInputAction action)
        {
            return EnsureSource().WasPressed(action);
        }

        /// <summary>屏幕坐标转玩法平面（z = 0）世界坐标；保持原有镜头换算行为。</summary>
        public static Vector2 PointerWorld(Camera camera)
        {
            if (camera == null) camera = Camera.main;
            if (camera == null) return Vector2.zero;

            Vector3 screen = Current.PointerScreen;
            screen.z = Mathf.Abs(camera.transform.position.z);
            return camera.ScreenToWorldPoint(screen);
        }

        /// <summary>替换输入源；测试注入 Fake，后续可替换为 Input Actions 实现。</summary>
        public static void SetSource(IInputSource value)
        {
            source = value;
            cachedSnapshot = InputSnapshot.Empty;
            cachedFrame = -1;
            cancelConsumedFrame = -1;
        }

        /// <summary>清理静态状态，供测试和场景重载使用。</summary>
        public static void Reset()
        {
            source = null;
            cachedSnapshot = InputSnapshot.Empty;
            cachedFrame = -1;
            cancelConsumedFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Reset();
        }

        private static IInputSource EnsureSource()
        {
            if (source == null) source = new UnityInputSource();
            return source;
        }
    }
}
