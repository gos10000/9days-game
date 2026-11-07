using System;
using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>生成默认子弹定义与目录；已存在时只补空字段，不覆盖用户文案。</summary>
    internal static class AmmoFactory
    {
        private const string SimpleIconPath = "Assets/Art/Bullets/normal ammo.png";
        private const string TestBallIconPath = "Assets/Art/Bullets/guiding bolt.png";

        internal static AmmoCatalog EnsureDefaults()
        {
            EditorAssets.EnsureFolder(EditorAssets.AmmoDataRoot);

            Projectile simplePrefab = LoadProjectile(EditorAssets.SimpleBulletPath);
            Projectile testBallPrefab = LoadProjectile(EditorAssets.TestBallBulletPath);

            AmmoDefinition simple = EnsureDefinition(
                EditorAssets.AmmoSimplePath, simplePrefab,
                "普通子弹", "稳定、通用的基础子弹。", SimpleIconPath);

            AmmoDefinition testBall = EnsureDefinition(
                EditorAssets.AmmoTestBallPath, testBallPrefab,
                "测试球", "用于验证选弹与发射流程的测试子弹。", TestBallIconPath);

            AmmoCatalog catalog = AssetDatabase.LoadAssetAtPath<AmmoCatalog>(EditorAssets.AmmoCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AmmoCatalog>();
                EditorAssets.EnsureFolderOfFile(EditorAssets.AmmoCatalogPath);
                AssetDatabase.CreateAsset(catalog, EditorAssets.AmmoCatalogPath);
            }

            catalog.EnsureAmmo(new[] { simple, testBall });
            catalog.Validate();
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AmmoFactory] 默认弹药定义与目录已就绪");
            return catalog;
        }

        private static AmmoDefinition EnsureDefinition(string path, Projectile prefab,
                                                       string displayName, string description,
                                                       string iconPath)
        {
            AmmoDefinition definition = AssetDatabase.LoadAssetAtPath<AmmoDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<AmmoDefinition>();
                EditorAssets.EnsureFolderOfFile(path);
                AssetDatabase.CreateAsset(definition, path);
            }

            if (definition.prefab == null) definition.prefab = prefab;
            if (string.IsNullOrWhiteSpace(definition.displayName)) definition.displayName = displayName;
            if (string.IsNullOrWhiteSpace(definition.description)) definition.description = description;
            if (definition.icon == null)
                definition.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Projectile LoadProjectile(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException($"找不到子弹 prefab: {path}");
            Projectile projectile = prefab.GetComponent<Projectile>();
            if (projectile == null) throw new InvalidOperationException($"子弹 prefab 缺 Projectile: {path}");
            return projectile;
        }
    }
}
