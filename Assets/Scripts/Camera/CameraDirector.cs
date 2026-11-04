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
            if (bounds != null) ShowOverview(true);
        }

        /// <summary>显示整个场地；snap 为 true 时立即到位。</summary>
        public void ShowOverview(bool snap = true)
        {
            targets.Clear();
            overview = true;
            RefreshPose(snap);
        }

        /// <summary>替换目标列表；列表为空时自动回全景。</summary>
        public void SetTargets(IReadOnlyList<Transform> newTargets, bool snap = false)
        {
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
            if (rig == null || rig.TargetCamera == null || bounds == null) return;

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
