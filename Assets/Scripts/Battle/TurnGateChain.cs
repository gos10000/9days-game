using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 把多个回合开始步骤串成一条链：选弹 → 放道具 → 才开始行动。
    /// 存在的理由：ITurnStartGate 在 BattleController 里是单槽，两个实现会互相抢；
    /// 有了链，各步骤独立开发，合并时不用改对方代码。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnGateChain : MonoBehaviour, ITurnStartGate
    {
        [Tooltip("按顺序执行的步骤；留空则在 Awake 时自动收集场景里的门实现（按类型名排序）")]
        [SerializeField] private List<MonoBehaviour> steps = new List<MonoBehaviour>();

        private int index;
        private bool running;
        private Team actor;
        private Action onReady;

        public bool HasSteps
        {
            get
            {
                EnsureSteps();
                return steps != null && steps.Count > 0;
            }
        }

        public IReadOnlyList<MonoBehaviour> Steps => steps;

        private void Awake()
        {
            EnsureSteps();
        }

        /// <summary>BattleController 可能在链的 Awake 之前就来问，这里兜底收集。</summary>
        private void EnsureSteps()
        {
            if (steps == null) steps = new List<MonoBehaviour>();
            if (steps.Count == 0) AutoCollect();
        }

        public void BeginTurn(Team actor, Action onReady)
        {
            if (running) Cancel();

            this.actor = actor;
            this.onReady = onReady;
            running = true;
            index = 0;

            Advance();
        }

        public void Cancel()
        {
            if (!running) return;

            running = false;
            onReady = null;
            index = HasSteps ? steps.Count : 0;

            if (steps == null) return;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i] is ITurnStartGate gate) gate.Cancel();
        }

        /// <summary>当前步骤放行后才推进下一个；任一步骤同步回调也不会卡住。</summary>
        private void Advance()
        {
            while (running && index < steps.Count)
            {
                MonoBehaviour step = steps[index++];
                if (step == null || !step.isActiveAndEnabled) continue;
                if (step is not ITurnStartGate gate) continue;

                gate.BeginTurn(actor, Advance);
                return;
            }

            if (!running) return;

            running = false;
            Action done = onReady;
            onReady = null;
            done?.Invoke();
        }

        /// <summary>没有手填时自动收集，保证「拖进场景就能跑」。</summary>
        private void AutoCollect()
        {
            var found = new List<MonoBehaviour>();
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                if (ReferenceEquals(behaviours[i], this)) continue;
                if (behaviours[i] is ITurnStartGate) found.Add(behaviours[i]);
            }

            // 按类型名排序：AmmoSelectionController 在 PropPlacementController 之前，
            // 正好是「先选弹、再放道具」。顺序需要变时，手动填 steps 覆盖即可。
            found.Sort((a, b) => string.CompareOrdinal(a.GetType().Name, b.GetType().Name));

            steps = found;

            if (found.Count > 2)
                Debug.LogWarning($"[TurnGateChain] 自动收集到 {found.Count} 个回合门，执行顺序可能不是你想要；请在 Inspector 手动填写 Steps。", this);
        }
    }
}
