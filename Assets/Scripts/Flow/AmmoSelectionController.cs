using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NineDays
{
    /// <summary>
    /// 行动开始前的选弹门：随机抽取、显示选弹页、把结果写入 CharacterLauncher。
    /// 选弹期间关闭战斗输入；选弹完成/取消后恢复。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmmoSelectionController : MonoBehaviour, ITurnStartGate
    {
        [Header("数据")]
        [Tooltip("全部可选子弹")]
        [SerializeField] private AmmoCatalog catalog;

        [Tooltip("每次随机展示数量；大于目录数量时显示全部")]
        [SerializeField, Min(1)] private int drawCount = 3;

        [Tooltip("测试开关：展示目录里的全部子弹")]
        [SerializeField] private bool showAllBulletsForTesting;

        [Tooltip("0 = 每次随机；非 0 = 固定序列，便于复现")]
        [SerializeField] private int randomSeed;

        [Header("引用")]
        [Tooltip("选弹页桥接；为空自动查找")]
        [SerializeField] private UIAmmoSelectPresenter presenterSource;

        [Tooltip("取行动角色；为空自动查找")]
        [SerializeField] private TurnController turnController;

        [Tooltip("选弹期间关闭战斗输入；为空自动查找")]
        [SerializeField] private BattleInputController inputController;

        private IAmmoSelectPresenter presenter;
        private Random random;
        private bool selecting;
        private Team actor;
        private Action onReady;
        private IReadOnlyList<AmmoDefinition> picked;
        private bool previousInput;
        private bool inputLocked;

        private void Awake()
        {
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
            if (inputController == null) inputController = FindAnyObjectByType<BattleInputController>();
            presenter = presenterSource != null
                ? presenterSource
                : FindAnyObjectByType<UIAmmoSelectPresenter>();
            CreateRandom();
        }

        private void OnDisable()
        {
            Cancel();
        }

        /// <summary>测试/流程注入；正式场景用 Inspector。</summary>
        public void Configure(AmmoCatalog ammoCatalog, int count, bool showAll, int seed,
                              IAmmoSelectPresenter selectPresenter, TurnController turns,
                              BattleInputController input)
        {
            catalog = ammoCatalog;
            drawCount = Mathf.Max(1, count);
            showAllBulletsForTesting = showAll;
            randomSeed = seed;
            presenter = selectPresenter;
            turnController = turns;
            inputController = input;
            CreateRandom();
        }

        /// <summary>回合开始门：选弹完成（或走回退）后调用 onReady。</summary>
        public void BeginTurn(Team actor, Action onReady)
        {
            if (selecting) Cancel();

            this.actor = actor;
            this.onReady = onReady;
            selecting = true;

            picked = AmmoDraw.Pick(
                catalog != null ? catalog.Ammo : null,
                drawCount,
                showAllBulletsForTesting,
                RandomForDraw());

            if (picked.Count == 0)
            {
                selecting = false;
                ResetActorPrefab();
                InvokeReady();
                return;
            }

            previousInput = inputController != null && inputController.InputEnabled;
            inputLocked = inputController != null;
            if (inputLocked) inputController.InputEnabled = false;

            if (presenter == null || !presenter.Show(BuildOptions(picked), OnPicked))
                CompleteSelection(picked[0]);
        }

        /// <summary>关闭页面并丢弃当前选弹；不触发 onReady。</summary>
        public void Cancel()
        {
            if (!selecting) return;

            selecting = false;
            onReady = null;
            picked = null;
            presenter?.Hide();
            RestoreInput();
        }

        private void OnPicked(int index)
        {
            if (!selecting || picked == null || index < 0 || index >= picked.Count) return;
            CompleteSelection(picked[index]);
        }

        private void CompleteSelection(AmmoDefinition definition)
        {
            if (!selecting) return;
            selecting = false;

            if (definition != null && definition.prefab != null)
                ApplyToActor(definition);
            else
                ResetActorPrefab();

            presenter?.Hide();
            RestoreInput();
            InvokeReady();
        }

        private void ApplyToActor(AmmoDefinition definition)
        {
            CharacterLauncher launcher = FindLauncher();
            if (launcher != null) launcher.SetProjectilePrefab(definition.prefab);
        }

        private void ResetActorPrefab()
        {
            CharacterLauncher launcher = FindLauncher();
            if (launcher != null) launcher.ResetProjectilePrefab();
        }

        private CharacterLauncher FindLauncher()
        {
            Character character = turnController != null ? turnController.GetCharacter(actor) : null;
            return character != null ? character.GetComponent<CharacterLauncher>() : null;
        }

        private List<AmmoOption> BuildOptions(IReadOnlyList<AmmoDefinition> definitions)
        {
            var options = new List<AmmoOption>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                AmmoDefinition definition = definitions[i];
                options.Add(new AmmoOption(definition.ResolvedName, definition.description, definition.icon));
            }

            return options;
        }

        private void InvokeReady()
        {
            Action callback = onReady;
            onReady = null;
            callback?.Invoke();
        }

        private void RestoreInput()
        {
            if (!inputLocked) return;
            inputLocked = false;
            if (inputController != null) inputController.InputEnabled = previousInput;
        }

        private void CreateRandom()
        {
            random = randomSeed == 0 ? null : new Random(randomSeed);
        }

        private Random RandomForDraw()
        {
            if (randomSeed == 0) return new Random();
            return random ??= new Random(randomSeed);
        }

        private void OnValidate()
        {
            drawCount = Mathf.Max(1, drawCount);
        }
    }
}

