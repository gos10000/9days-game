using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>生成资源时用到的通用小工具（路径、内置精灵、材质、序列化字段赋值）。</summary>
    internal static class EditorAssets
    {
        internal const string PrefabRoot = "Assets/Prefabs";
        internal const string DataRoot = "Assets/Data";
        internal const string MaterialRoot = "Assets/Materials";
        internal const string SceneRoot = "Assets/Scenes";

        internal const string CharacterPrefabPath = PrefabRoot + "/Characters/Character_Base.prefab";
        internal const string CharacterP1Path = PrefabRoot + "/Characters/Character_P1.prefab";
        internal const string CharacterP2Path = PrefabRoot + "/Characters/Character_P2.prefab";
        internal const string SkinDefaultPath = PrefabRoot + "/Skins/Skin_Default.prefab";
        internal const string SkinAltPath = PrefabRoot + "/Skins/Skin_Alt.prefab";
        internal const string TestBallPath = PrefabRoot + "/Projectiles/TestBall.prefab";

        internal const string CharacterConfigPath = DataRoot + "/Characters/CharacterConfig_Default.asset";
        internal const string ProjectileConfigPath = DataRoot + "/Projectiles/TestBallConfig.asset";

        internal const string TestArenaScenePath = SceneRoot + "/TestArena.unity";

        internal const string UIPrefabRoot = PrefabRoot + "/UI";
        internal const string UIScreenPath = UIPrefabRoot + "/UI_Screen.prefab";
        internal const string UIHudPath = UIPrefabRoot + "/UI_HUD.prefab";
        internal const string UIButtonPath = UIPrefabRoot + "/UI_Button.prefab";
        internal const string UIHealthBarPath = UIPrefabRoot + "/UI_HealthBar.prefab";
        internal const string UITurnBannerPath = UIPrefabRoot + "/UI_TurnBanner.prefab";
        internal const string UIBulletTypePath = UIPrefabRoot + "/UI_BulletType.prefab";
        internal const string UIPausePanelPath = UIPrefabRoot + "/UI_PausePanel.prefab";
        internal const string UIItemSelectPanelPath = UIPrefabRoot + "/UI_ItemSelectPanel.prefab";
        internal const string UIPlacementPanelPath = UIPrefabRoot + "/UI_PlacementPanel.prefab";

        internal const string UIScenePath = SceneRoot + "/UIScene.unity";

        internal const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        internal static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "Assets") return;
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent)) parent = parent.Replace('\\', '/');

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        internal static void EnsureFolderOfFile(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) EnsureFolder(dir.Replace('\\', '/'));
        }

        /// <summary>白色圆角方块（引擎内置资源，不属于项目里的 demo 资产）。</summary>
        internal static Sprite LoadSquareSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite == null) throw new InvalidOperationException("找不到内置精灵 UI/Skin/UISprite.psd");
            return sprite;
        }

        /// <summary>白色圆形（引擎内置资源）。</summary>
        internal static Sprite LoadCircleSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (sprite == null) throw new InvalidOperationException("找不到内置精灵 UI/Skin/Knob.psd");
            return sprite;
        }

        internal static Material CreateOrLoadMaterial(string path, params string[] shaderNames)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = null;
            foreach (string shaderName in shaderNames)
            {
                shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    Debug.Log($"[Setup] 使用 shader: {shaderName}");
                    break;
                }
            }

            if (shader == null)
            {
                Debug.LogWarning($"[Setup] 找不到可用 shader，跳过材质: {path}");
                return null;
            }

            EnsureFolderOfFile(path);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>按世界尺寸缩放物体（自动适配精灵 PPU）。</summary>
        internal static void ScaleToWorldSize(Transform target, Sprite sprite, Vector2 worldSize)
        {
            Vector3 bounds = sprite != null ? sprite.bounds.size : Vector3.one;
            float sx = bounds.x > 0.0001f ? worldSize.x / bounds.x : 1f;
            float sy = bounds.y > 0.0001f ? worldSize.y / bounds.y : 1f;
            target.localScale = new Vector3(sx, sy, 1f);
        }

        internal static SerializedProperty Find(UnityEngine.Object target, string property)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) throw new InvalidOperationException($"{target.GetType().Name}.{property} 不存在");
            return p;
        }

        internal static void SetRef(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetObjectArray(UnityEngine.Object target, string property, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetInt(UnityEngine.Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetFloat(UnityEngine.Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetBool(UnityEngine.Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetString(UnityEngine.Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetVector2(UnityEngine.Object target, string property, Vector2 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetRect(UnityEngine.Object target, string property, Rect value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).rectValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static UnityEngine.Object GetRef(UnityEngine.Object target, string property)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            return p != null ? p.objectReferenceValue : null;
        }

        internal static int GetInt(UnityEngine.Object target, string property)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            return p != null ? p.intValue : 0;
        }
    }
}
