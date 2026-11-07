using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹视觉自转：把预制体下所有挂了 SpriteRenderer 的子物体绕着自身 Z 轴持续旋转。
    /// 只动表现层，不影响 Rigidbody2D 的受力和碰撞方向。
    /// 默认只在「已发射（飞行中）」时旋转，回到对象池后自动停住并复位。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletSpinVisual : MonoBehaviour
    {
        [Header("旋转")]
        [Tooltip("每秒旋转角度，360 = 1 秒一圈")]
        [SerializeField] private float spinSpeed = 540f;

        [Tooltip("勾选为顺时针（屏幕上看是逆时针方向取决于 2D 坐标系，反过来试一下即可）")]
        [SerializeField] private bool clockwise = true;

        [Header("变化")]
        [Tooltip("每个子物体的转速略有差异，避免所有部件整齐划一显得僵硬")]
        [SerializeField] private bool perChildVariance = false;
        [SerializeField, Range(0f, 0.9f)] private float variance = 0.3f;

        [Header("时机")]
        [Tooltip("勾选：只在子弹发射后旋转；取消：激活即一直转")]
        [SerializeField] private bool onlyWhileFlying = true;

        [Tooltip("停止旋转时把子物体的旋转复位到初始值（对象池复用时不残留角度）")]
        [SerializeField] private bool resetOnStop = true;

        private Projectile projectile;
        private Transform[] targets = new Transform[0];
        private Quaternion[] initialRotations = new Quaternion[0];
        private float[] speeds = new float[0];

        private void Awake()
        {
            projectile = GetComponent<Projectile>();
            if (projectile == null) projectile = GetComponentInParent<Projectile>();

            CacheTargets();
        }

        private void OnEnable()
        {
            if (projectile == null) projectile = GetComponent<Projectile>();
            if (targets.Length == 0) CacheTargets();

            if (onlyWhileFlying && projectile != null && !projectile.IsLaunched) ResetRotations();
        }

        private void Update()
        {
            if (targets.Length == 0) return;

            if (onlyWhileFlying && projectile != null && !projectile.IsLaunched)
            {
                if (resetOnStop) ResetRotations();
                return;
            }

            float direction = clockwise ? -1f : 1f;
            float dt = Time.deltaTime;

            for (int i = 0; i < targets.Length; i++)
            {
                Transform t = targets[i];
                if (t == null) continue;

                t.Rotate(0f, 0f, direction * speeds[i] * dt, Space.Self);
            }
        }

        /// <summary>收集所有挂了 SpriteRenderer 的子物体（含自身）。</summary>
        private void CacheTargets()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            var list = new List<Transform>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Transform t = renderers[i].transform;
                if (!list.Contains(t)) list.Add(t);
            }

            targets = list.ToArray();
            initialRotations = new Quaternion[targets.Length];
            speeds = new float[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                initialRotations[i] = targets[i].localRotation;

                if (!perChildVariance)
                {
                    speeds[i] = spinSpeed;
                    continue;
                }

                // 用下标做伪随机，保证同一个预制体每次运行的表现一致
                float noise = Mathf.Sin((i + 1) * 12.9898f) * 43758.5453f;
                noise -= Mathf.Floor(noise);
                float factor = 1f + (noise * 2f - 1f) * variance;
                speeds[i] = spinSpeed * factor;
            }
        }

        private void ResetRotations()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null) targets[i].localRotation = initialRotations[i];
            }
        }
    }
}
