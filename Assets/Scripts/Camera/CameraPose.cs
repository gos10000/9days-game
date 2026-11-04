using UnityEngine;

namespace NineDays
{
    /// <summary>2D 正交相机的目标姿态：世界中心 + orthographicSize。</summary>
    public readonly struct CameraPose
    {
        public readonly Vector2 Center;
        public readonly float OrthographicSize;

        public CameraPose(Vector2 center, float orthographicSize)
        {
            Center = center;
            OrthographicSize = Mathf.Max(0.01f, orthographicSize);
        }

        public Vector3 ToWorldPosition(float z) => new Vector3(Center.x, Center.y, z);
    }
}
