using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成默认背景/场地/关卡 prefab，并把默认关卡接到 Battle Scene。
    /// 不重建角色、子弹或 TestArena，避免覆盖已有美术资源。
    /// </summary>
    internal static class BattleSceneSetup
    {
        private const string BackgroundFolder = "Assets/Prefabs/Backgrounds";
        private const string ArenaFolder = "Assets/Prefabs/Arenas";
        private const string LevelFolder = "Assets/Prefabs/Levels";

        private const string BackgroundPrefabPath = BackgroundFolder + "/Background_Default.prefab";
        private const string ArenaPrefabPath = ArenaFolder + "/Arena_Default.prefab";
        private const string LevelPrefabPath = LevelFolder + "/Level_Default.prefab";
        private const string BattleScenePath = "Assets/Scenes/Battle Scene.unity";

        [MenuItem("9Days/Build/Battle Scene Setup (Default Level)")]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Setup();
        }

        /// <summary>batchmode 入口：生成默认 prefab 并接线 Battle Scene。</summary>
        public static void SetupBatch()
        {
            try
            {
                Setup();
                Debug.Log("BATTLE_SETUP_OK");
            }
            catch (Exception e)
            {
                Debug.LogError("BATTLE_SETUP_FAILED\n" + e);
                EditorApplication.Exit(1);
            }
        }

        internal static void Setup()
        {
            EditorAssets.EnsureFolder(BackgroundFolder);
            EditorAssets.EnsureFolder(ArenaFolder);
            EditorAssets.EnsureFolder(LevelFolder);

            GameObject background = EnsureBackgroundPrefab();
            GameObject arena = EnsureArenaPrefab();
            EnsureLevelPrefab(background, arena);

            AmmoFactory.EnsureDefaults();
            UIFactory.BuildAll();

            UIPageConfig ammoPageConfig = AssetDatabase.LoadAssetAtPath<UIPageConfig>(EditorAssets.AmmoSelectConfigPath);
            if (ammoPageConfig != null) ammoPageConfig.Validate();

            UIPageCatalog pageCatalog = AssetDatabase.LoadAssetAtPath<UIPageCatalog>(EditorAssets.UIPageCatalogPath);
            if (pageCatalog != null) pageCatalog.Validate();

            // 先让新 prefab 完成导入，否则场景里保存的组件引用会在刷新后失效
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ApplyToBattleScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BattleSceneSetup] 默认关卡与战斗场景已就绪");
        }

        /// <summary>创建只含表现层的默认背景 prefab；已存在则直接复用。</summary>
        private static GameObject EnsureBackgroundPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPrefabPath);
            if (existing != null) return existing;

            Sprite sprite = LoadSprite("Assets/Art/Background/background  sky.png");
            var root = new GameObject("Background_Default");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -100;
            EditorAssets.ScaleToWorldSize(root.transform, sprite, new Vector2(18f, 11f));

            PrefabUtility.SaveAsPrefabAsset(root, BackgroundPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPrefabPath);
        }

        /// <summary>创建带地面与两侧空气墙的默认场地 prefab。</summary>
        private static GameObject EnsureArenaPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            if (existing != null) return existing;

            Sprite sprite = LoadSprite("Assets/Art/Background/ground.png");
            var root = new GameObject("Arena_Default");

            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -4.85f, 0f);

            var renderer = ground.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -10;
            EditorAssets.ScaleToWorldSize(ground.transform, sprite, new Vector2(16f, 1f));

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer < 0) obstacleLayer = 0;

            // 碰撞体单独放，避免被视觉 Sprite 的缩放放大
            var groundCollider = new GameObject("GroundCollider");
            groundCollider.transform.SetParent(root.transform, false);
            groundCollider.transform.localPosition = new Vector3(0f, -4.85f, 0f);
            groundCollider.layer = obstacleLayer;
            var box = groundCollider.AddComponent<BoxCollider2D>();
            box.size = new Vector2(16f, 1f);

            CreateWall(root.transform, "WallLeft", new Vector3(-8.5f, -0.5f, 0f),
                       new Vector2(1f, 10f), obstacleLayer);
            CreateWall(root.transform, "WallRight", new Vector3(8.5f, -0.5f, 0f),
                       new Vector2(1f, 10f), obstacleLayer);

            PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
        }

        /// <summary>创建关卡数据 prefab；已存在时只补空引用。</summary>
        private static BattleLevel EnsureLevelPrefab(GameObject background, GameObject arena)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LevelPrefabPath);
            if (existing != null)
            {
                BattleLevel current = LoadLevelComponent();
                if (current == null) current = existing.AddComponent<BattleLevel>();
                if (current.backgroundPrefab == null) current.backgroundPrefab = background;
                if (current.arenaPrefab == null) current.arenaPrefab = arena;
                EditorUtility.SetDirty(current);
                AssetDatabase.SaveAssets();
                return current;
            }

            var root = new GameObject("Level_Default");
            var level = root.AddComponent<BattleLevel>();
            level.backgroundPrefab = background;
            level.arenaPrefab = arena;
            level.player1Spawn = new Vector2(-5f, -3.7f);
            level.player2Spawn = new Vector2(5f, -3.7f);

            PrefabUtility.SaveAsPrefabAsset(root, LevelPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return LoadLevelComponent();
        }

        private static BattleLevel LoadLevelComponent()
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(LevelPrefabPath))
                if (asset is BattleLevel level) return level;
            return null;
        }

        /// <summary>把默认关卡、UIRoot 与选弹门接到 Battle Scene。</summary>
        private static void ApplyToBattleScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            BattleLevel level = LoadLevelComponent();
            AmmoCatalog ammoCatalog = AssetDatabase.LoadAssetAtPath<AmmoCatalog>(EditorAssets.AmmoCatalogPath);
            if (level == null) throw new InvalidOperationException("Level_Default.prefab 缺 BattleLevel 组件");
            if (ammoCatalog == null) throw new InvalidOperationException("AmmoCatalog_Default.asset 不存在");

            BattleController battle = FindFirst<BattleController>();
            TurnController turn = FindFirst<TurnController>();
            BattleInputController input = FindFirst<BattleInputController>();
            if (battle == null) throw new InvalidOperationException("Battle Scene 里没有 BattleController");
            if (turn == null) throw new InvalidOperationException("Battle Scene 里没有 TurnController");

            BattleSceneInitializer initializer = FindFirst<BattleSceneInitializer>();
            if (initializer == null)
            {
                var go = new GameObject("BattleSceneInitializer");
                go.transform.SetParent(battle.transform.parent, false);
                initializer = go.AddComponent<BattleSceneInitializer>();
            }

            if (EditorAssets.GetRef(initializer, "level") == null)
                EditorAssets.SetRef(initializer, "level", level);
            if (EditorAssets.GetRef(initializer, "turnController") == null)
                EditorAssets.SetRef(initializer, "turnController", turn);
            if (EditorAssets.GetRef(battle, "sceneInitializer") == null)
                EditorAssets.SetRef(battle, "sceneInitializer", initializer);

            EnsureUIRoot();
            EnsureAmmoSelection(ammoCatalog, battle, turn, input);

            EditorUtility.SetDirty(initializer);
            EditorUtility.SetDirty(battle);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>确保战斗场景有 UIRoot，并指向包含选弹页的默认 catalog。</summary>
        private static void EnsureUIRoot()
        {
            UIRoot uiRoot = FindFirst<UIRoot>();
            if (uiRoot == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIRootPrefabPath);
                if (prefab == null) throw new InvalidOperationException("找不到 UIRoot.prefab");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "UIRoot";
                uiRoot = instance.GetComponent<UIRoot>();
            }

            UIPageCatalog catalog = AssetDatabase.LoadAssetAtPath<UIPageCatalog>(EditorAssets.UIPageCatalogPath);
            if (uiRoot != null && catalog != null)
                EditorAssets.SetRef(uiRoot, "catalog", catalog);
        }

        /// <summary>创建/补全选弹门与 UI 桥接；已有字段不覆盖用户设置。</summary>
        private static void EnsureAmmoSelection(AmmoCatalog ammoCatalog, BattleController battle,
                                                TurnController turn, BattleInputController input)
        {
            AmmoSelectionController ammo = FindFirst<AmmoSelectionController>();
            bool created = false;
            if (ammo == null)
            {
                var go = new GameObject("AmmoSelection");
                ammo = go.AddComponent<AmmoSelectionController>();
                created = true;
            }

            UIAmmoSelectPresenter presenter = ammo.GetComponent<UIAmmoSelectPresenter>();
            if (presenter == null) presenter = ammo.gameObject.AddComponent<UIAmmoSelectPresenter>();
            EditorAssets.SetString(presenter, "pageId", "AmmoSelect");

            if (created)
            {
                EditorAssets.SetInt(ammo, "drawCount", 3);
                EditorAssets.SetBool(ammo, "showAllBulletsForTesting", false);
                EditorAssets.SetInt(ammo, "randomSeed", 0);
            }

            if (EditorAssets.GetRef(ammo, "catalog") == null) EditorAssets.SetRef(ammo, "catalog", ammoCatalog);
            if (EditorAssets.GetRef(ammo, "presenterSource") == null) EditorAssets.SetRef(ammo, "presenterSource", presenter);
            if (EditorAssets.GetRef(ammo, "turnController") == null) EditorAssets.SetRef(ammo, "turnController", turn);
            if (input != null && EditorAssets.GetRef(ammo, "inputController") == null)
                EditorAssets.SetRef(ammo, "inputController", input);

            if (EditorAssets.GetRef(battle, "turnStartGateSource") == null)
                EditorAssets.SetRef(battle, "turnStartGateSource", ammo);

            EditorUtility.SetDirty(ammo);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(battle);
        }

        /// <summary>在场景里找第一个指定组件，包含未激活对象。</summary>
        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>创建只有碰撞体的空气墙。</summary>
        private static void CreateWall(Transform parent, string wallName, Vector3 position,
                                       Vector2 size, int layer)
        {
            var wall = new GameObject(wallName);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.layer = layer;

            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        /// <summary>加载项目 Sprite；失败时回退内置方形精灵。</summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : EditorAssets.LoadSquareSprite();
        }
    }
}
