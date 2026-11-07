using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 道具放置：幽灵跟着鼠标走，左键放下、右键跳过这一个、Esc 放弃剩余直接开打。
    /// 只负责「放」；抽什么、选哪个由 TurnOpeningFlow 决定。
    /// 规则：中心夹在场内（允许边缘外探），只拦「压在角色或其他道具上」。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PropPlacementController : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("放置期间关闭战斗输入；为空自动查找")]
        [SerializeField] private BattleInputController inputController;

        [Tooltip("世界相机；为空用 Camera.main")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("屏幕跟随图标；为空且开启时自动创建")]
        [SerializeField] private PropCursorView cursorView;

        [Header("放置规则")]
        [Tooltip("落点压到角色时禁止放置")]
        [SerializeField] private bool blockOnCharacters = true;

        [Tooltip("落点压到其他道具时禁止放置；默认关——道具之间互不影响，洞的范围内可以再放一个洞")]
        [SerializeField] private bool blockOnProps;

        [Tooltip("显示跟随鼠标的屏幕图标")]
        [SerializeField] private bool showCursorIcon = true;

        [Tooltip("道具中心离场地边缘至少留这么多，避免贴死在边界上")]
        [SerializeField] private float arenaMargin = 0.3f;

        [Tooltip("防止道具/角色被叠在一起的最小间隔；大道具按占地取，但不超过这个上限")]
        [SerializeField] private float maxClearance = 2f;

        [Tooltip("放置时用红色虚线圈标出不可放置的区域")]
        [SerializeField] private bool showBlockedZones = true;

        [SerializeField] private bool logPlacement = true;

        private readonly List<PropSpec> queue = new List<PropSpec>();
        private readonly List<GameObject> placedInstances = new List<GameObject>();
        private readonly List<Character> blockerCharacters = new List<Character>();
        private readonly List<ProjectileField> blockerProps = new List<ProjectileField>();
        private readonly List<PropBlockedZoneVisual.Zone> zoneBuffer = new List<PropBlockedZoneVisual.Zone>();
        private PropBlockedZoneVisual zoneVisual;
        private Transform ghostRoot;
        private PropGhost ghost;
        private PropSpec current;
        private Action sessionDone;
        private bool active;
        private bool inputLocked;
        private bool previousInput;
        private int cursor;
        private int armedFrame = -1;

        /// <summary>每次真正放下一个道具时触发；外部 UI / 统计可以订阅。</summary>
        public event Action<PropSpec, GameObject> Placed;

        public bool IsPlacing => active && current.IsValid;
        public IReadOnlyList<PropSpec> Queue => queue;

        private void Awake()
        {
            if (inputController == null) inputController = FindAnyObjectByType<BattleInputController>();
            if (worldCamera == null) worldCamera = Camera.main;
            if (cursorView == null && showCursorIcon) cursorView = PropCursorView.Create();
        }

        private void OnEnable()
        {
            GameEvents.BattleStarted -= HandleBattleStarted;
            GameEvents.BattleStarted += HandleBattleStarted;
        }

        private void OnDisable()
        {
            Cancel();
            GameEvents.BattleStarted -= HandleBattleStarted;
        }

        /// <summary>新一场战斗开始时清掉上一场放下的道具，避免跨局累积。</summary>
        private void HandleBattleStarted(int rounds)
        {
            for (int i = 0; i < placedInstances.Count; i++)
                if (placedInstances[i] != null) Destroy(placedInstances[i]);

            placedInstances.Clear();
        }

        /// <summary>给一批道具开始放置；全部放完（或跳过/按 Esc）后回调。</summary>
        public void BeginPlacement(IReadOnlyList<PropSpec> props, Action onDone = null)
        {
            CloseSession(false);
            StartSession(onDone);
            LoadQueue(props);

            if (queue.Count == 0)
            {
                CloseSession(true);
                return;
            }

            BeginCarry();
        }

        /// <summary>取消本次放置，不触发回调。</summary>
        public void Cancel()
        {
            CloseSession(false);
        }

        // ---------------- 主循环 ----------------

        private void Update()
        {
            if (!active || !current.IsValid) return;

            InputSnapshot input = GameInput.Current;

            if (input.CancelPressed && GameInput.ConsumeCancel())
            {
                // Esc = 放弃剩余道具、直接进入投掷；不能只关界面，否则回合永远不开始
                CloseSession(true);
                return;
            }

            bool overUI = PropCursorView.IsPointerOverUI();
            Vector2 point = ClampToArena(PointerWorld());
            bool valid = !overUI && IsValid(point, current);

            ghost?.SetPose(point, valid);
            if (cursorView != null) cursorView.SetPointer(input.PointerScreen, valid);

            RefreshBlockedZones(current);

            // 选道具页上的那一下点击不该顺手放下一个道具
            if (Time.frameCount <= armedFrame) return;

            if (input.SecondaryPressed)
            {
                Advance();
                return;
            }

            if (input.PrimaryPressed && valid) Place(point);
        }

        private void Place(Vector2 point)
        {
            PropSpec spec = current;
            if (!spec.IsValid)
            {
                Advance();
                return;
            }

            GameObject instance = Instantiate(spec.Prefab, point, Quaternion.identity);
            instance.name = spec.Prefab.name;
            placedInstances.Add(instance);

            if (logPlacement)
                Debug.Log($"[PropPlacement] 放下 {spec.ResolvedName} @ {point}", instance);

            Placed?.Invoke(spec, instance);
            GameEvents.RaisePropPlaced(spec, instance);

            Advance();
        }

        private void Advance()
        {
            cursor++;

            if (cursor >= queue.Count)
            {
                CloseSession(true);
                return;
            }

            BeginCarry();
        }

        private void BeginCarry()
        {
            DestroyGhost();

            if (cursor >= queue.Count)
            {
                CloseSession(true);
                return;
            }

            // 每个新道具开始前重收一次：刚放下的道具也要算进「不可放置」
            CollectBlockers();

            current = queue[cursor];
            Vector2 point = ClampToArena(PointerWorld());

            ghost = PropGhost.Create(current.Prefab, EnsureGhostRoot());
            ghost?.SetPose(point, IsValid(point, current));

            if (cursorView != null) cursorView.Show(current.ResolvedIcon);
        }

        private void LoadQueue(IReadOnlyList<PropSpec> props)
        {
            queue.Clear();
            cursor = 0;

            if (props == null) return;

            for (int i = 0; i < props.Count; i++)
            {
                PropSpec spec = props[i];
                if (!spec.IsValid) continue;

                int copies = Mathf.Max(1, spec.Count);
                for (int c = 0; c < copies; c++) queue.Add(spec);
            }
        }

        // ---------------- 落点判定 ----------------

        /// <summary>
        /// 只要求「中心」落在场地内：大道具（增强黑洞直径 9）可以伸出场地边界，
        /// 否则 24×10 的场地里只有中间一条 1 单位高的带子能放，等于放不下来。
        /// 重叠只按核心间距判断，不拿整个视觉包围盒去卡，避免大气场把整片场地判死。
        /// </summary>
        private bool IsValid(Vector2 point, PropSpec spec)
        {
            if (!spec.IsValid) return false;
            return !Overlaps(point, spec);
        }

        private bool Overlaps(Vector2 point, PropSpec spec)
        {
            if (!blockOnCharacters && !blockOnProps) return false;

            // 角色：按「手持道具的核心间距」判定
            if (blockOnCharacters && OverlapsCharacter(point, ClearanceFor(spec))) return true;

            // 道具：按各自的「不可放置半径」判定，圆心就是道具中心
            return blockOnProps && IsInsideBlockedField(point);
        }

        private static bool OverlapsCharacter(Vector2 point, float clearance)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, clearance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit != null && hit.GetComponentInParent<Character>() != null) return true;
            }

            return false;
        }

        private bool IsInsideBlockedField(Vector2 point)
        {
            for (int i = 0; i < blockerProps.Count; i++)
            {
                ProjectileField field = blockerProps[i];
                if (field == null) continue;

                float radius = field.BlockRadius;
                if (radius <= 0f) continue;

                Vector2 center = field.transform.position;
                if ((point - center).sqrMagnitude < radius * radius) return true;
            }

            return false;
        }

        /// <summary>当前道具要求的核心间距（只用于角色）；红圈半径也用它，保证看到和判定一致。</summary>
        private float ClearanceFor(PropSpec spec)
        {
            Vector2 size = spec.Footprint;
            float raw = Mathf.Max(size.x, size.y) * 0.25f;
            return Mathf.Clamp(raw, 0.5f, Mathf.Max(0.5f, maxClearance));
        }

        // ---------------- 不可放置区域可视化 ----------------

        /// <summary>把角色和已有道具收进缓存；每个新道具开始前刷新一次即可。</summary>
        private void CollectBlockers()
        {
            blockerCharacters.Clear();
            blockerProps.Clear();

            if (blockOnCharacters)
                blockerCharacters.AddRange(FindObjectsByType<Character>(FindObjectsSortMode.None));

            if (blockOnProps)
                blockerProps.AddRange(FindObjectsByType<ProjectileField>(FindObjectsSortMode.None));
        }

        /// <summary>用红虚线标出每个占位者周围「中心不能进入」的范围。</summary>
        private void RefreshBlockedZones(PropSpec spec)
        {
            if (!showBlockedZones) return;

            // 懒创建：只有真的进入放置才生成贴图，平时不占开销
            if (zoneVisual == null) zoneVisual = PropBlockedZoneVisual.Create(transform);

            zoneBuffer.Clear();
            float clearance = ClearanceFor(spec);

            for (int i = 0; i < blockerCharacters.Count; i++)
            {
                Character character = blockerCharacters[i];
                if (character == null) continue;

                AddZone(character.GetComponent<Collider2D>(), clearance);
            }

            for (int i = 0; i < blockerProps.Count; i++)
            {
                ProjectileField field = blockerProps[i];
                if (field == null) continue;

                // 道具用自己配的半径，圆心 = 道具中心
                float radius = field.BlockRadius;
                if (radius <= 0f) continue;

                Vector2 center = field.transform.position;
                zoneBuffer.Add(new PropBlockedZoneVisual.Zone(center, radius));
            }

            if (zoneBuffer.Count == 0) zoneVisual.Hide();
            else zoneVisual.Refresh(zoneBuffer);
        }

        private void AddZone(Collider2D collider, float clearance)
        {
            if (collider == null || !collider.enabled) return;

            Bounds bounds = collider.bounds;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y);
            zoneBuffer.Add(new PropBlockedZoneVisual.Zone(bounds.center, clearance + extent));
        }

        /// <summary>把落点夹进场内；返回的是夹过之后的点，幽灵会贴着边界走而不是变红。</summary>
        private Vector2 ClampToArena(Vector2 point)
        {
            ArenaBounds bounds = ArenaController.Instance != null ? ArenaController.Instance.Bounds : null;
            if (bounds == null) return point;

            Rect area = bounds.PlayArea;
            float margin = Mathf.Max(0f, arenaMargin);

            return new Vector2(
                Mathf.Clamp(point.x, area.xMin + margin, area.xMax - margin),
                Mathf.Clamp(point.y, area.yMin + margin, area.yMax - margin));
        }

        private Vector2 PointerWorld()
        {
            Camera camera = worldCamera != null ? worldCamera : Camera.main;
            return GameInput.PointerWorld(camera);
        }

        // ---------------- 会话与输入锁 ----------------

        private void StartSession(Action onDone)
        {
            sessionDone = onDone;
            active = true;
            queue.Clear();
            cursor = 0;
            current = default;

            LockInput();

            // 选择页弹出时的点击不能被当成放置
            armedFrame = Time.frameCount + 1;
        }

        private void CloseSession(bool notify)
        {
            if (!active) return;

            active = false;
            DestroyGhost();

            if (cursorView != null) cursorView.Hide();
            if (zoneVisual != null) zoneVisual.Hide();

            current = default;
            queue.Clear();
            cursor = 0;
            UnlockInput();

            Action done = sessionDone;
            sessionDone = null;
            if (notify) done?.Invoke();
        }

        private void LockInput()
        {
            if (inputController == null)
            {
                Debug.LogWarning("[PropPlacement] 找不到 BattleInputController，放置期间无法屏蔽战斗输入", this);
                return;
            }

            previousInput = inputController.InputEnabled;
            inputLocked = true;
            inputController.InputEnabled = false;
        }

        private void UnlockInput()
        {
            if (!inputLocked) return;

            inputLocked = false;
            if (inputController != null) inputController.InputEnabled = previousInput;
        }

        private Transform EnsureGhostRoot()
        {
            if (ghostRoot != null) return ghostRoot;

            var go = new GameObject("PropGhosts");
            go.transform.SetParent(transform, false);
            ghostRoot = go.transform;
            return ghostRoot;
        }

        private void DestroyGhost()
        {
            if (ghost == null) return;

            Destroy(ghost.gameObject);
            ghost = null;
        }
    }
}
