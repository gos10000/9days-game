using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NineDays
{
    /// <summary>
    /// 行动前的开桌流程：从「子弹 + 道具」的混合卡池抽一次，玩家选一张。
    /// 选子弹 → 本回合换弹；选道具 → 子弹用默认的普通弹，先放置道具再投掷。
    /// 作为 ITurnStartGate 挂在回合开始，每回合自动走一遍。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnOpeningFlow : MonoBehaviour, ITurnStartGate
    {
        [Header("抽取表")]
        [Tooltip("子弹表；留空则只抽道具")]
        [SerializeField] private AmmoTable ammoTable;

        [Tooltip("道具表；留空则只抽子弹")]
        [SerializeField] private PropTable propTable;

        [Tooltip("每次展示几张候选（子弹与道具混在一起）")]
        [SerializeField, Min(1)] private int drawCount = 3;

        [Tooltip("0 = 每次随机；非 0 = 固定序列，便于复现")]
        [SerializeField] private int randomSeed;

        [Header("引用")]
        [Tooltip("选择页桥接；为空自动查找")]
        [SerializeField] private UIAmmoSelectPresenter presenterSource;

        [Tooltip("选桌前关闭战斗输入；为空自动查找")]
        [SerializeField] private BattleInputController inputController;

        [Tooltip("取行动角色；为空自动查找")]
        [SerializeField] private TurnController turnController;

        [Tooltip("道具放置器；为空自动查找")]
        [SerializeField] private PropPlacementController placement;

        [Header("UI 兜底")]
        [Tooltip("场景里没有 UIRoot 时用它补一个；留空则只找现成的")]
        [SerializeField] private GameObject uiRootPrefab;

        [Tooltip("补 UIRoot 时挂的页面目录；必须包含 AmmoSelect 页")]
        [SerializeField] private UIPageCatalog uiCatalog;

        [Tooltip("开场镜头状态；为空自动查找。开场演完才开始抽")]
        [SerializeField] private MonoBehaviour introSource;

        [SerializeField] private bool logFlow = true;

        private enum CardKind
        {
            Ammo,
            Prop,
        }

        /// <summary>一张候选卡：要么是子弹，要么是道具。</summary>
        private readonly struct Card
        {
            public readonly CardKind Kind;
            public readonly AmmoTable.Row Ammo;
            public readonly PropTable.Row Prop;

            private Card(CardKind kind, AmmoTable.Row ammo, PropTable.Row prop)
            {
                Kind = kind;
                Ammo = ammo;
                Prop = prop;
            }

            public static Card FromAmmo(AmmoTable.Row row) => new Card(CardKind.Ammo, row, null);
            public static Card FromProp(PropTable.Row row) => new Card(CardKind.Prop, null, row);
        }

        private IChoicePresenter presenter;
        private IBattleIntroState intro;
        private Coroutine waitRoutine;
        private Random random;
        private Team actor;
        private Action onReady;
        private bool running;
        private bool inputLocked;
        private bool previousInput;
        private List<Card> drawn;

        public bool IsRunning => running;

        private void Awake()
        {
            if (inputController == null) inputController = FindAnyObjectByType<BattleInputController>();
            if (turnController == null) turnController = FindAnyObjectByType<TurnController>();
            if (placement == null) placement = FindAnyObjectByType<PropPlacementController>();
            if (presenterSource == null) presenterSource = FindAnyObjectByType<UIAmmoSelectPresenter>();
            presenter = presenterSource;
        }

        private void OnDisable()
        {
            Cancel();
        }

        /// <summary>测试/流程注入。</summary>
        public void Configure(AmmoTable ammo, PropTable props, int count, int seed,
                              IChoicePresenter choicePresenter, TurnController turns,
                              BattleInputController input, PropPlacementController placementController)
        {
            ammoTable = ammo;
            propTable = props;
            drawCount = Mathf.Max(1, count);
            randomSeed = seed;
            presenter = choicePresenter;
            turnController = turns;
            inputController = input;
            placement = placementController;
        }

        // ---------------- 回合门 ----------------

        public void BeginTurn(Team actor, Action onReady)
        {
            Cancel();

            this.actor = actor;
            this.onReady = onReady;
            running = true;

            // 每回合先回到默认子弹：没选子弹的回合就用它
            ResetToDefaultAmmo();

            LockInput();

            // 开场特写还在演，就先不弹选择页；镜头交回常规后再抽
            if (ResolveIntro() != null && intro.IntroPlaying)
            {
                if (logFlow) Debug.Log("[TurnOpeningFlow] 等待开场镜头结束…");
                waitRoutine = StartCoroutine(WaitIntroThenDraw());
                return;
            }

            DrawCards();
        }

        private IEnumerator WaitIntroThenDraw()
        {
            // 上限只是兜底：镜头状态异常时不至于把整个回合卡死
            float waited = 0f;
            while (running && intro != null && intro.IntroPlaying && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            waitRoutine = null;
            if (!running) yield break;

            if (logFlow) Debug.Log($"[TurnOpeningFlow] 开场镜头结束（等待 {waited:0.0}s），开始抽取");
            DrawCards();
        }

        /// <summary>不直接依赖相机程序集，按接口在场景里找。</summary>
        private IBattleIntroState ResolveIntro()
        {
            if (intro != null) return intro;
            if (introSource is IBattleIntroState direct) return intro = direct;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IBattleIntroState state) return intro = state;

            return null;
        }

        /// <summary>
        /// 场景里没接 UIRoot / 选弹页时，现场补一套，避免流程退化成「自动选第一个」。
        /// Battle Scene 本来就有，这一步是空操作。
        /// </summary>
        private void EnsurePresenter()
        {
            if (presenter != null) return;

            if (UIRoot.Instance == null && uiRootPrefab != null)
            {
                GameObject go = Instantiate(uiRootPrefab);
                UIRoot root = go.GetComponent<UIRoot>();
                if (root != null) root.Configure(uiCatalog);
                if (logFlow) Debug.Log("[TurnOpeningFlow] 场景缺 UIRoot，已实例化并挂上页面目录", this);
            }

            presenterSource = FindAnyObjectByType<UIAmmoSelectPresenter>();
            if (presenterSource == null && UIRoot.Instance != null)
                presenterSource = UIRoot.Instance.gameObject.AddComponent<UIAmmoSelectPresenter>();

            presenter = presenterSource;
        }

        public void Cancel()
        {
            if (!running) return;

            running = false;
            onReady = null;

            if (waitRoutine != null)
            {
                StopCoroutine(waitRoutine);
                waitRoutine = null;
            }

            presenter?.Hide();
            placement?.Cancel();
            UnlockInput();
        }

        private void Finish()
        {
            if (!running) return;

            running = false;

            Action done = onReady;
            onReady = null;

            UnlockInput();
            done?.Invoke();
        }

        // ---------------- 混合抽取 ----------------

        private void DrawCards()
        {
            EnsurePresenter();

            var pool = new List<Card>();

            if (ammoTable != null)
            {
                List<AmmoTable.Row> ammoRows = ammoTable.ValidRows();
                for (int i = 0; i < ammoRows.Count; i++) pool.Add(Card.FromAmmo(ammoRows[i]));
            }

            if (propTable != null)
            {
                List<PropTable.Row> propRows = propTable.ValidRows();
                for (int i = 0; i < propRows.Count; i++) pool.Add(Card.FromProp(propRows[i]));
            }

            if (pool.Count == 0)
            {
                Debug.LogWarning("[TurnOpeningFlow] 子弹表与道具表都没有可用条目，本回合只能用默认子弹", this);
                Finish();
                return;
            }

            drawn = DraftPick.Pick(pool, drawCount, Rng());

            var options = new List<AmmoOption>(drawn.Count);
            for (int i = 0; i < drawn.Count; i++) options.Add(ToOption(drawn[i]));

            Show("选择子弹或道具", options, OnCardPicked);
        }

        private void OnCardPicked(int index)
        {
            if (!running) return;

            presenter?.Hide();

            if (!TryPick(drawn, index, out Card card))
            {
                Finish();
                return;
            }

            if (card.Kind == CardKind.Ammo)
            {
                ApplyAmmo(card.Ammo);
                if (logFlow && card.Ammo != null)
                    Debug.Log($"[TurnOpeningFlow] {actor} 选择子弹 {card.Ammo.ResolvedName}");
                Finish();
                return;
            }

            if (logFlow && card.Prop != null)
                Debug.Log($"[TurnOpeningFlow] {actor} 选择道具 {card.Prop.ResolvedName}（子弹用普通弹）");

            if (placement == null || card.Prop == null || !card.Prop.IsValid)
            {
                Finish();
                return;
            }

            placement.BeginPlacement(new[] { card.Prop.ToSpec() }, Finish);
        }

        private static AmmoOption ToOption(in Card card)
        {
            if (card.Kind == CardKind.Ammo)
            {
                AmmoTable.Row row = card.Ammo;
                return new AmmoOption(row.ResolvedName, row.description, row.ResolvedIcon);
            }

            PropTable.Row prop = card.Prop;
            Sprite icon = prop.icon != null ? prop.icon : PreviewIcon(prop.prefab);
            return new AmmoOption(prop.ResolvedName, prop.description, icon);
        }

        private void ApplyAmmo(AmmoTable.Row row)
        {
            if (row == null) return;

            CharacterLauncher launcher = ResolveLauncher();
            Projectile prefab = row.ResolvedProjectile;
            if (launcher != null && prefab != null) launcher.SetProjectilePrefab(prefab);
        }

        /// <summary>回到角色预制体上的默认子弹（普通弹）。</summary>
        private void ResetToDefaultAmmo()
        {
            ResolveLauncher()?.ResetProjectilePrefab();
        }

        private CharacterLauncher ResolveLauncher()
        {
            Character character = turnController != null ? turnController.GetCharacter(actor) : null;
            return character != null ? character.GetComponent<CharacterLauncher>() : null;
        }

        // ---------------- 工具 ----------------

        private void Show(string title, IReadOnlyList<AmmoOption> options, Action<int> onPicked)
        {
            if (presenter != null && presenter.Show(title, options, onPicked)) return;

            // 没接 UI（缺 UIRoot / 页面）时不能让回合卡死，直接取第一个并说清楚原因
            Debug.LogWarning($"[TurnOpeningFlow] 找不到可用的选择页（UIRoot / AmmoSelectPage 未接），自动选择第一个：{title}", this);
            onPicked?.Invoke(0);
        }

        private Random Rng()
        {
            return random ??= randomSeed == 0 ? new Random() : new Random(randomSeed);
        }

        private static bool TryPick<T>(IReadOnlyList<T> list, int index, out T value)
        {
            if (list == null || index < 0 || index >= list.Count)
            {
                value = default;
                return false;
            }

            value = list[index];
            return true;
        }

        /// <summary>表里没填图标时，用 prefab 上的精灵兜底。</summary>
        private static Sprite PreviewIcon(GameObject prefab)
        {
            if (prefab == null) return null;
            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private void LockInput()
        {
            if (inputController == null)
            {
                Debug.LogWarning("[TurnOpeningFlow] 找不到 BattleInputController，抽选期间无法屏蔽战斗输入", this);
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
    }
}
