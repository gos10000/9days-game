using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

            var battleConfig = AssetDatabase.LoadAssetAtPath<BattleConfig>(EditorAssets.BattleConfigPath);
            Require(battleConfig != null, "战斗配置不存在");
            Require(battleConfig.roundsToPlay > 0, "战斗配置轮数必须大于 0");
            Require(battleConfig.maxTurnsPerRound >= 2, "战斗配置每轮行动上限至少为 2");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new UnityException("[BuildCheck] " + message);
        }
    }
}
