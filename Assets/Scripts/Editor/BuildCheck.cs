using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>生成后的结构自检：不满足就抛异常，让 batchmode 以失败退出。</summary>
    internal static class BuildCheck
    {
        internal static void ValidateAll()
        {
            ValidateCharacter(EditorAssets.CharacterPrefabPath);
            ValidateCharacter(EditorAssets.CharacterP1Path);
            ValidateCharacter(EditorAssets.CharacterP2Path);
            ValidateSkin(EditorAssets.SkinDefaultPath);
            ValidateSkin(EditorAssets.SkinAltPath);
            ValidateBall(EditorAssets.TestBallPath);
            ValidateConfigs();
            ValidateArenaConfig();
            ValidateUI();
            ValidateScenePool();
            ValidateSceneArena();
            ValidateSceneUI();
            Debug.Log("[BuildCheck] 结构校验全部通过");
        }

        private static void ValidateCharacter(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, $"{path} 不存在");
            Require(prefab.GetComponent<Rigidbody2D>() != null, $"{path} 缺 Rigidbody2D");
            Require(prefab.GetComponent<CapsuleCollider2D>() != null, $"{path} 缺 CapsuleCollider2D");
            Require(prefab.GetComponent<SpriteRenderer>() == null, $"{path} 根节点不应有 SpriteRenderer");

            var character = prefab.GetComponent<Character>();
            var combat = prefab.GetComponent<CharacterCombat>();
            var launcher = prefab.GetComponent<CharacterLauncher>();
            Require(character != null, $"{path} 缺 Character");
            Require(combat != null, $"{path} 缺 CharacterCombat");
            Require(launcher != null, $"{path} 缺 CharacterLauncher");

            Transform visual = prefab.transform.Find("Visual");
            Require(visual != null, $"{path} 缺 Visual");
            Require(visual.GetComponent<SortingGroup>() != null, $"{path}/Visual 缺 SortingGroup");

            var visualComp = visual.GetComponent<CharacterVisual>();
            Require(visualComp != null, $"{path}/Visual 缺 CharacterVisual");
            Require(visual.GetComponentInChildren<Collider2D>(true) == null, $"{path}/Visual 下不允许有 Collider2D");
            Require(visual.GetComponentInChildren<Rigidbody2D>(true) == null, $"{path}/Visual 下不允许有 Rigidbody2D");

            Transform aim = prefab.transform.Find("AimPivot");
            Require(aim != null, $"{path} 缺 AimPivot");
            Require(aim.GetComponent<Rigidbody2D>() == null, $"{path}/AimPivot 不应有 Rigidbody2D");
            Require(aim.GetComponent<Collider2D>() == null, $"{path}/AimPivot 不应有 Collider2D");

            Require(prefab.transform.Find("GroundCheck") != null, $"{path} 缺 GroundCheck");
            Require(prefab.transform.Find("HitPoint") != null, $"{path} 缺 HitPoint");

            Require(EditorAssets.GetRef(character, "config") != null, $"{path} Character.config 为空");
            Require(EditorAssets.GetRef(character, "body") != null, $"{path} Character.body 为空");
            Require(EditorAssets.GetRef(combat, "character") != null, $"{path} CharacterCombat.character 为空");
            Require(EditorAssets.GetRef(visualComp, "config") != null, $"{path}/Visual config 为空");
            Require(EditorAssets.GetRef(launcher, "projectilePrefab") != null, $"{path} CharacterLauncher.projectilePrefab 为空");

            var health = prefab.GetComponent<CharacterHealth>();
            Require(health != null, $"{path} 缺 CharacterHealth");
            Require(prefab.GetComponent<ProjectileModifierSource>() != null, $"{path} 缺 ProjectileModifierSource");
            Require(EditorAssets.GetRef(character, "health") != null, $"{path} Character.health 为空");
            Require(EditorAssets.GetRef(health, "config") != null, $"{path} CharacterHealth.config 为空");
        }

        private static void ValidateSkin(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, $"{path} 不存在");
            Require(prefab.GetComponent<Rigidbody2D>() == null, $"{path} 皮肤不允许有 Rigidbody2D");
            Require(prefab.GetComponentInChildren<Collider2D>(true) == null, $"{path} 皮肤不允许有 Collider2D");
            Require(prefab.transform.localPosition.sqrMagnitude < 0.0001f, $"{path} 皮肤根节点必须在原点");

            bool hasVisual = prefab.GetComponentsInChildren<SpriteRenderer>(true).Length > 0
                             || prefab.GetComponentInChildren<Animator>(true) != null;
            Require(hasVisual, $"{path} 既没有 SpriteRenderer 也没有 Animator");
        }

        private static void ValidateBall(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, $"{path} 不存在");
            Rigidbody2D ballBody = prefab.GetComponent<Rigidbody2D>();
            Require(ballBody != null, $"{path} 缺 Rigidbody2D");
            Require(prefab.GetComponent<CircleCollider2D>() != null, $"{path} 缺 CircleCollider2D");

            var projectile = prefab.GetComponent<Projectile>();
            Require(projectile != null, $"{path} 缺 Projectile");
            Require(EditorAssets.GetRef(projectile, "config") != null, $"{path} Projectile.config 为空");
            Require(projectile.Config != null, $"{path} Projectile.Config 为空");
            Require(Mathf.Approximately(ballBody.gravityScale, projectile.Config.gravityScale),
                $"{path} 刚体重力与配置不一致");
        }

        private static void ValidateConfigs()
        {
            var characterConfig = AssetDatabase.LoadAssetAtPath<CharacterConfig>(EditorAssets.CharacterConfigPath);
            Require(characterConfig != null, "角色配置不存在");
            Require(characterConfig.skins != null && characterConfig.skins.Count >= 2, "角色配置至少要 2 套皮肤");

            for (int i = 0; i < characterConfig.skins.Count; i++)
                Require(characterConfig.skins[i].visualPrefab != null, $"角色配置第 {i} 套皮肤缺少 visualPrefab");

            var projectileConfig = AssetDatabase.LoadAssetAtPath<ProjectileConfig>(EditorAssets.ProjectileConfigPath);
            Require(projectileConfig != null, "子弹配置不存在");
            Require(projectileConfig.radius > 0f && projectileConfig.initialSpeed > 0f, "子弹配置数值非法");
            Require(projectileConfig.traits != null, "子弹配置缺少特性列表");
            Require(projectileConfig.bursts != null, "子弹配置缺少爆发列表");
            for (int i = 0; i < projectileConfig.bursts.Count; i++)
            {
                BurstDefinition burst = projectileConfig.bursts[i];
                Require(burst != null, $"子弹配置第 {i} 个爆发为空");
                Require(burst.effects != null && burst.effects.Count > 0, $"爆发 {burst.burstId} 没有效果");
                Require(burst.cooldown >= 0f && burst.maxTriggers >= 0, $"爆发 {burst.burstId} 参数非法");
                for (int j = 0; j < burst.effects.Count; j++)
                    Require(burst.effects[j] != null, $"爆发 {burst.burstId} 第 {j} 个效果为空");
            }

            var battleConfig = AssetDatabase.LoadAssetAtPath<BattleConfig>(EditorAssets.BattleConfigPath);
            Require(battleConfig != null, "战斗配置不存在");
            Require(battleConfig.roundsToPlay > 0, "战斗配置轮数必须大于 0");
            Require(battleConfig.maxTurnsPerRound >= 2, "战斗配置每轮行动上限至少为 2");
        }

        /// <summary>地形配置必须有可用的层、遮罩与视觉数据。</summary>
        private static void ValidateArenaConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArenaConfig>(EditorAssets.ArenaConfigPath);
            Require(config != null, "地形配置不存在");
            Require(config.worldRect.width > 0f && config.worldRect.height > 0f, "地形配置场地尺寸非法");
            Require(config.pixelsPerUnit > 0, "地形配置 pixelsPerUnit 非法");
            Require(config.layers != null && config.layers.Count > 0, "地形配置至少需要一层");

            bool hasCollision = false;
            for (int i = 0; i < config.layers.Count; i++)
            {
                TerrainLayerData layer = config.layers[i];
                Require(layer != null, $"地形配置第 {i} 层为空");
                Require(!string.IsNullOrWhiteSpace(layer.layerId), $"地形配置第 {i} 层缺少 layerId");
                Require(layer.initialMask != null, $"地形层 {layer.layerId} 缺少遮罩");
                Require(layer.initialMask.width == config.MaskWidth && layer.initialMask.height == config.MaskHeight,
                    $"地形层 {layer.layerId} 遮罩尺寸与配置不一致");
                Require(layer.art != null && layer.art.Count > 0, $"地形层 {layer.layerId} 缺少美术 Sprite");
                Require(layer.requiredBreakPower >= 0f, $"地形层 {layer.layerId} 破坏力阈值非法");
                hasCollision |= layer.collisionEnabled;
            }

            Require(hasCollision, "地形配置至少需要一层参与碰撞");
        }

        /// <summary>页面注册表、页面 prefab 与 UI 根 prefab 的结构校验。</summary>
        private static void ValidateUI()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIPageCatalog>(EditorAssets.UIPageCatalogPath);
            Require(catalog != null, "UI 页面注册表不存在");
            Require(catalog.Count > 0, "UI 页面注册表至少需要一个页面");
            catalog.Validate();

            for (int i = 0; i < catalog.Pages.Count; i++)
            {
                UIPageConfig config = catalog.Pages[i];
                Require(config != null, $"UI 页面配置第 {i} 项为空");
                Require(config.Prefab != null, $"UI 页面 {config.PageId} 缺少 prefab");
                Require(config.Prefab.GetComponent<UIPage>() != null, $"UI 页面 {config.PageId} 的 prefab 缺 UIPage");
                Require(config.Prefab.GetComponent<CanvasGroup>() != null, $"UI 页面 {config.PageId} 的 prefab 缺 CanvasGroup");
            }

            GameObject rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.UIRootPrefabPath);
            Require(rootPrefab != null, "UIRoot prefab 不存在");
            var uiRoot = rootPrefab.GetComponent<UIRoot>();
            Require(uiRoot != null, "UIRoot prefab 缺 UIRoot");
            Require(EditorAssets.GetRef(uiRoot, "catalog") != null, "UIRoot prefab 的 catalog 为空");
        }

        /// <summary>测试场景必须挂 UIRoot 且引用页面注册表。</summary>
        private static void ValidateSceneUI()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != EditorAssets.TestArenaScenePath) return;

            UIRoot uiRoot = UnityEngine.Object.FindAnyObjectByType<UIRoot>();
            Require(uiRoot != null, "TestArena 缺 UIRoot");
            Require(uiRoot.Catalog != null, "UIRoot.catalog 为空");
        }

        /// <summary>测试场景必须在场时检查对象池；只跑 Prefab 构建时跳过。</summary>
        private static void ValidateScenePool()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != EditorAssets.TestArenaScenePath) return;

            ProjectilePool pool = UnityEngine.Object.FindAnyObjectByType<ProjectilePool>();
            Require(pool != null, "TestArena 缺 ProjectilePool");
            Require(pool.PrewarmPrefab != null, "ProjectilePool.prewarmPrefab 为空");
        }

        /// <summary>测试场景必须挂 ArenaController，且编辑期不保留静态地形。</summary>
        private static void ValidateSceneArena()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != EditorAssets.TestArenaScenePath) return;

            ArenaController arena = UnityEngine.Object.FindAnyObjectByType<ArenaController>();
            Require(arena != null, "TestArena 缺 ArenaController");
            Require(arena.Config != null, "ArenaController.config 为空");
            Require(arena.TerrainMaterial != null, "ArenaController.terrainMaterial 为空");

            TerrainLayer[] staticLayers = UnityEngine.Object.FindObjectsByType<TerrainLayer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Require(staticLayers.Length == 0, "TestArena 不应包含静态 TerrainLayer，地形必须运行时重建");

            TerrainPhysics[] staticPhysics = UnityEngine.Object.FindObjectsByType<TerrainPhysics>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Require(staticPhysics.Length == 0, "TestArena 不应包含静态 TerrainPhysics");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new UnityException("[BuildCheck] " + message);
        }
    }
}
