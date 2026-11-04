using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成角色 / 皮肤 / 子弹预制体与配置资源。
    /// 全部代码生成，避免手拖 prefab 产生冲突或漏引用。
    /// </summary>
    internal static class PrefabFactory
    {
        internal static void BuildAll()
        {
            EditorAssets.EnsureFolder(EditorAssets.PrefabRoot + "/Characters");
            EditorAssets.EnsureFolder(EditorAssets.PrefabRoot + "/Skins");
            EditorAssets.EnsureFolder(EditorAssets.PrefabRoot + "/Projectiles");
            EditorAssets.EnsureFolder(EditorAssets.DataRoot + "/Characters");
            EditorAssets.EnsureFolder(EditorAssets.DataRoot + "/Projectiles");
            EditorAssets.EnsureFolder(EditorAssets.MaterialRoot);

            Sprite square = EditorAssets.LoadSquareSprite();
            Sprite circle = EditorAssets.LoadCircleSprite();

            BuildSkin(EditorAssets.SkinDefaultPath, "Skin_Default", new Color(0.95f, 0.95f, 0.95f), square, new Vector2(1f, 2f), 10);
            BuildSkin(EditorAssets.SkinAltPath, "Skin_Alt", new Color(0.45f, 0.78f, 1f), square, new Vector2(1f, 2f), 10);

            CharacterConfig characterConfig = CreateCharacterConfig(
                EditorAssets.CharacterConfigPath,
                AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.SkinDefaultPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.SkinAltPath));

            CreateBattleConfig(EditorAssets.BattleConfigPath);
            ProjectileConfig projectileConfig = CreateProjectileConfig(EditorAssets.ProjectileConfigPath);

            GameObject ball = BuildTestBall(EditorAssets.TestBallPath, projectileConfig, circle);
            if (ball == null) throw new InvalidOperationException("TestBall 生成失败");

            Material lineMaterial = EditorAssets.CreateOrLoadMaterial(
                EditorAssets.MaterialRoot + "/AimLine.mat",
                "Universal Render Pipeline/2D/Sprite-Unlit-Default",
                "Sprites/Default");

            BuildCharacterBase(EditorAssets.CharacterPrefabPath, characterConfig, ball.GetComponent<Projectile>(), lineMaterial);
            BuildVariant(EditorAssets.CharacterPrefabPath, EditorAssets.CharacterP1Path, Team.Player1, 0);
            BuildVariant(EditorAssets.CharacterPrefabPath, EditorAssets.CharacterP2Path, Team.Player2, 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UIFactory.BuildAll();
            Debug.Log("[PrefabFactory] 预制体与配置生成完成");
        }

        private static void BuildSkin(string path, string name, Color color, Sprite sprite, Vector2 worldSize, int order)
        {
            var root = new GameObject(name);
            var group = root.AddComponent<SortingGroup>();
            group.sortingLayerName = "Character";
            group.sortingOrder = order;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);

            var renderer = spriteGo.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Character";
            renderer.sortingOrder = order;

            EditorAssets.ScaleToWorldSize(spriteGo.transform, sprite, worldSize);

            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[PrefabFactory] 皮肤: {path}");
        }

        private static CharacterConfig CreateCharacterConfig(string path, GameObject skinDefault, GameObject skinAlt)
        {
            if (skinDefault == null || skinAlt == null)
                throw new InvalidOperationException("皮肤 prefab 生成失败，无法创建角色配置");

            CharacterConfig config = AssetDatabase.LoadAssetAtPath<CharacterConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CharacterConfig>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(config, path);
            }

            config.mass = 1f;
            config.colliderSize = new Vector2(1f, 2f);
            config.freezeRotation = true;

            config.hitThreshold = 0.5f;
            config.knockbackScale = 1f;
            config.minKnockback = 2f;
            config.maxKnockback = 20f;
            config.controlLockTime = 0.35f;

            config.impactThreshold = 1f;
            config.obstacleMask = LayerMask.GetMask("Obstacle");
            config.impactCooldown = 0.05f;

            config.landedSpeed = 1f;
            config.settleSpeed = 0.15f;
            config.settleTime = 0.3f;
            config.getUpDelay = 0.5f;
            config.getUpDuration = 0.4f;

            config.groundCheckRadius = 0.12f;
            config.spawnNudgeAttempts = 8;
            config.spawnNudgeStep = 0.3f;
            config.defaultSkinIndex = 0;

            config.skins = new List<CharacterConfig.Skin>
            {
                new CharacterConfig.Skin
                {
                    skinId = "Default",
                    visualPrefab = skinDefault,
                    visualOffset = Vector2.zero,
                    sortingOrder = 10,
                },
                new CharacterConfig.Skin
                {
                    skinId = "Alt",
                    visualPrefab = skinAlt,
                    visualOffset = Vector2.zero,
                    sortingOrder = 10,
                },
            };

            EditorUtility.SetDirty(config);
            Debug.Log($"[PrefabFactory] 角色配置: {path}");
            return config;
        }

        private static BattleConfig CreateBattleConfig(string path)
        {
            BattleConfig config = AssetDatabase.LoadAssetAtPath<BattleConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BattleConfig>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(config, path);
            }

            config.roundsToPlay = 9;
            config.randomSeed = 0;
            config.maxTurnsPerRound = 6;
            config.turnResolveDelay = 0.6f;
            config.roundResolveDelay = 1f;
            config.turnResolveTimeout = 3f;

            EditorUtility.SetDirty(config);
            Debug.Log($"[PrefabFactory] 战斗配置: {path}");
            return config;
        }

        private static ProjectileConfig CreateProjectileConfig(string path)
        {
            ProjectileConfig config = AssetDatabase.LoadAssetAtPath<ProjectileConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ProjectileConfig>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(config, path);
            }

            config.mass = 0.5f;
            config.radius = 0.25f;
            config.initialSpeed = 15f;
            config.lifeTime = 5f;
            config.ignoreOwnerTime = 0.15f;
            config.damage = 10f;
            config.destroyOnHit = true;
            config.friendlyFire = false;
            config.gravityScale = 1f;
            config.linearDamping = 0f;
            if (config.traits == null) config.traits = new List<ProjectileTrait>();
            if (config.bursts == null) config.bursts = new List<BurstDefinition>();

            EditorUtility.SetDirty(config);
            Debug.Log($"[PrefabFactory] 子弹配置: {path}");
            return config;
        }

        private static GameObject BuildTestBall(string path, ProjectileConfig config, Sprite sprite)
        {
            int layer = LayerMask.NameToLayer("Projectile");
            if (layer < 0) layer = 0;

            var root = new GameObject("TestBall");
            root.layer = layer;

            var body = root.AddComponent<Rigidbody2D>();
            body.mass = config.mass;
            body.gravityScale = config.gravityScale;
            body.linearDamping = config.linearDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = root.AddComponent<CircleCollider2D>();
            circle.radius = config.radius;

            var projectile = root.AddComponent<Projectile>();

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);

            var renderer = visualGo.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = 20;
            EditorAssets.ScaleToWorldSize(visualGo.transform, sprite, Vector2.one * (config.radius * 2f));

            EditorAssets.SetRef(projectile, "config", config);
            EditorAssets.SetRef(projectile, "body", body);
            EditorAssets.SetRef(projectile, "bodyCollider", circle);

            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[PrefabFactory] 子弹 prefab: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildCharacterBase(string path, CharacterConfig config, Projectile ballPrefab, Material lineMaterial)
        {
            int characterLayer = LayerMask.NameToLayer("Character");
            if (characterLayer < 0) characterLayer = 0;
            int obstacleMask = LayerMask.GetMask("Obstacle");

            var root = new GameObject("Character_Base");
            root.layer = characterLayer;

            var body = root.AddComponent<Rigidbody2D>();
            body.mass = config.mass;
            body.gravityScale = 1f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = config.colliderSize;

            var character = root.AddComponent<Character>();
            var combat = root.AddComponent<CharacterCombat>();
            var launcher = root.AddComponent<CharacterLauncher>();
            var health = root.AddComponent<CharacterHealth>();
            root.AddComponent<ProjectileModifierSource>();

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            var group = visualGo.AddComponent<SortingGroup>();
            group.sortingLayerName = "Character";
            group.sortingOrder = 10;
            var visual = visualGo.AddComponent<CharacterVisual>();

            var aim = new GameObject("AimPivot");
            aim.transform.SetParent(root.transform, false);
            aim.transform.localPosition = new Vector3(0.55f, 0.1f, 0f);

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(root.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, -1f, 0f);

            var hitPoint = new GameObject("HitPoint");
            hitPoint.transform.SetParent(root.transform, false);

            var lineGo = new GameObject("AimLine");
            lineGo.transform.SetParent(root.transform, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.06f;
            line.endWidth = 0.02f;
            line.numCapVertices = 2;
            line.sharedMaterial = lineMaterial;
            line.sortingLayerName = "Character";
            line.sortingOrder = 11;
            line.startColor = Color.white;
            line.endColor = new Color(1f, 1f, 1f, 0.2f);
            line.enabled = false;

            EditorAssets.SetRef(character, "config", config);
            EditorAssets.SetRef(character, "body", body);
            EditorAssets.SetRef(character, "bodyCollider", capsule);
            EditorAssets.SetRef(character, "combat", combat);
            EditorAssets.SetRef(character, "visual", visual);
            EditorAssets.SetRef(character, "aimPivot", aim.transform);
            EditorAssets.SetRef(character, "groundCheck", groundCheck.transform);
            EditorAssets.SetRef(character, "hitPoint", hitPoint.transform);
            EditorAssets.SetInt(character, "groundMask", obstacleMask);

            EditorAssets.SetRef(combat, "character", character);
            EditorAssets.SetRef(combat, "body", body);
            EditorAssets.SetRef(combat, "health", health);

            EditorAssets.SetRef(character, "health", health);
            EditorAssets.SetRef(health, "character", character);
            EditorAssets.SetRef(health, "config", config);

            EditorAssets.SetRef(visual, "character", character);
            EditorAssets.SetRef(visual, "config", config);
            EditorAssets.SetRef(visual, "visualRoot", visualGo.transform);
            EditorAssets.SetInt(visual, "initialSkinIndex", config.defaultSkinIndex);

            EditorAssets.SetRef(launcher, "character", character);
            EditorAssets.SetRef(launcher, "projectilePrefab", ballPrefab);
            EditorAssets.SetRef(launcher, "aimLine", line);

            EditorAssets.EnsureFolderOfFile(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[PrefabFactory] 角色 prefab: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void BuildVariant(string basePath, string outPath, Team team, int skinIndex)
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (basePrefab == null) throw new InvalidOperationException($"找不到基础 prefab: {basePath}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            var character = instance.GetComponent<Character>();
            var visual = instance.GetComponentInChildren<CharacterVisual>(true);

            EditorAssets.SetInt(character, "team", (int)team);
            if (visual != null) EditorAssets.SetInt(visual, "initialSkinIndex", skinIndex);

            instance.name = System.IO.Path.GetFileNameWithoutExtension(outPath);

            EditorAssets.EnsureFolderOfFile(outPath);
            PrefabUtility.SaveAsPrefabAsset(instance, outPath);
            UnityEngine.Object.DestroyImmediate(instance);

            Debug.Log($"[PrefabFactory] 角色变体: {outPath} (team={team}, skin={skinIndex})");
        }
    }
}
