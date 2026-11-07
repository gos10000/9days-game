using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 通用镜头目标管理：全景 / 目标列表 / 场地边界。
    /// 不订阅战斗事件，战斗适配交给 BattleCameraController。
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private CameraRig rig;
        [SerializeField] private ArenaBounds bounds;

        [Header("取景")]
        [SerializeField] private float minOrthographicSize = 4.5f;
        [SerializeField] private float maxOrthographicSize = 7f;
        [SerializeField] private float framingPadding = 1f;
        [SerializeField] private float overviewPadding = 0.5f;
        [SerializeField] private float boundsPadding = 0.25f;
        [SerializeField] private int maxTargets = 4;

        private readonly List<Transform> targets = new List<Transform>();
        private readonly List<Vector2> points = new List<Vector2>();
        private bool overview;

        // 临时缩放覆盖：开场介绍等「单目标特写」用，常规逻辑下为 false
        private bool sizeOverride;
        private float overrideSize;

        public CameraRig Rig => rig;
        public ArenaBounds Bounds => bounds;
        public int TargetCount => targets.Count;
        public bool IsOverview => overview;

        private void Awake()
        {
            if (rig == null) rig = GetComponent<CameraRig>();
            if (bounds == null) bounds = FindAnyObjectByType<ArenaBounds>();
        }

        private void Start()
        {
            // 若开场介绍已经抢先设置了特写目标，就不要再用全景覆盖它
            if (bounds != null && targets.Count == 0 && !sizeOverride) ShowOverview(true);
        }

        /// <summary>显示整个场地；snap 为 true 时立即到位。</summary>
        public void ShowOverview(bool snap = true)
        {
            targets.Clear();
            overview = true;
            sizeOverride = false;
            RefreshPose(snap);
        }

        /// <summary>替换目标列表；列表为空时自动回全景。</summary>
        public void SetTargets(IReadOnlyList<Transform> newTargets, bool snap = false)
        {
            sizeOverride = false;
            targets.Clear();
            if (newTargets != null)
            {
                for (int i = 0; i < newTargets.Count && targets.Count < Mathf.Max(1, maxTargets); i++)
                {
                    Transform target = newTargets[i];
                    if (target != null) targets.Add(target);
                }
            }

            overview = targets.Count == 0;
            RefreshPose(snap);
        }

        /// <summary>追加一个目标；用于飞行中的子弹。</summary>
        public void AddTarget(Transform target, bool snap = false)
        {
            if (target == null || targets.Contains(target)) return;
            if (targets.Count >= Mathf.Max(1, maxTargets)) return;

            targets.Add(target);
            overview = false;
            RefreshPose(snap);
        }

        /// <summary>移除目标；没有目标时自动回全景。</summary>
        public void RemoveTarget(Transform target, bool snap = false)
        {
            if (target == null) return;

            targets.Remove(target);
            overview = targets.Count == 0;
            RefreshPose(snap);
        }

        /// <summary>
        /// 用指定缩放聚焦单个目标（中心精确对准目标，不受场地边界钳制）。
        /// 用于开场介绍这类特写；snap 为 false 时由 CameraRig 平滑过渡。
        /// 调用 <see cref="ClearSizeOverride"/> 或任意 SetTargets / ShowOverview 恢复常规取景。
        /// </summary>
        public void FocusSingle(Transform target, float orthographicSize, bool snap = false)
        {
            targets.Clear();
            if (target != null) targets.Add(target);

            overview = target == null;
            overrideSize = Mathf.Max(0.01f, orthographicSize);
            sizeOverride = target != null;

            RefreshPose(snap);
        }

        /// <summary>清除特写缩放覆盖，回到 min/max 常规取景。</summary>
        public void ClearSizeOverride()
        {
            if (!sizeOverride) return;
            sizeOverride = false;
            RefreshPose(false);
        }

        /// <summary>增加镜头震动，转发给 CameraRig。</summary>
        public void AddShake(float strength)
        {
            if (rig != null) rig.AddShake(strength);
        }

        private void LateUpdate()
        {
            if (rig == null || rig.TargetCamera == null) return;

            for (int i = targets.Count - 1; i >= 0; i--)
                if (targets[i] == null) targets.RemoveAt(i);

            if (targets.Count == 0) overview = true;
            RefreshPose(false);
        }

        private void RefreshPose(bool snap)
        {
            if (rig == null || rig.TargetCamera == null) return;

            // 特写：单目标 + 固定缩放，中心精确对准目标；不依赖场地边界，也不做边界钳制
            if (sizeOverride && targets.Count == 1 && targets[0] != null)
            {
                rig.SetPose(new CameraPose(targets[0].position, overrideSize), snap);
                return;
            }

            if (bounds == null) return;

            Rect area = CameraFramer.Expand(bounds.PlayArea, boundsPadding);
            float aspect = Mathf.Max(0.01f, rig.TargetCamera.aspect);

            if (overview || targets.Count == 0)
            {
                rig.SetPose(CameraFramer.FrameOverview(area, overviewPadding, minOrthographicSize,
                                                       maxOrthographicSize, aspect), snap);
                return;
            }

            points.Clear();
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null) points.Add(targets[i].position);

            rig.SetPose(CameraFramer.Frame(points, framingPadding, minOrthographicSize,
                                           maxOrthographicSize, aspect, area), snap);
        }
    }
}
