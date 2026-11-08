using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 给关卡/战斗场景制作者用的快捷入口。
    ///
    /// 9Days/道具/放置木板障碍       —— 在 Scene 视图中心摆一块可破坏木板（可用 Ctrl+Z 撤销）
    /// 9Days/道具/给选中对象加可破坏 —— 给任意已有碰撞体的物件套上「子弹一碰就碎」的规则
    /// </summary>
    internal static class DestructiblePropMenu
    {
        private const string PlankPath = "Assets/Prefabs/Projectiles/Prop/PlankObstacle.prefab";

        [MenuItem("9Days/道具/放置木板障碍")]
        private static void PlacePlank()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankPath);
            if (prefab == null)
            {
                Debug.LogError($"[DestructibleProp] 找不到预制体 {PlankPath}");
                return;
            }

            Vector3 position = SceneViewPivot();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) return;

            instance.transform.position = position;
            Undo.RegisterCreatedObjectUndo(instance, "Place Plank Obstacle");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Debug.Log($"[DestructibleProp] 已放置木板障碍 @ {position}（点击 Scene 视图后按 Ctrl+Z 可撤销）");
        }

        /// <summary>
        /// 把选中的场景物件原地换成木板障碍：沿用它的世界位置/旋转/缩放和父节点，
        /// 这样美术尺寸和层级关系都不用重调。整步可 Ctrl+Z 撤销。
        /// </summary>
        [MenuItem("9Days/道具/用木板障碍替换选中对象")]
        private static void ReplaceWithPlank()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankPath);
            if (prefab == null)
            {
                Debug.LogError($"[DestructibleProp] 找不到预制体 {PlankPath}");
                return;
            }

            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[DestructibleProp] 请先选中要替换的对象（比如 Test_StartScene 里的 PlatForm）");
                return;
            }

            int replaced = 0;

            for (int i = 0; i < selected.Length; i++)
            {
                GameObject source = selected[i];
                if (source == null) continue;

                Transform parent = source.transform.parent;
                Vector3 position = source.transform.position;
                Quaternion rotation = source.transform.rotation;
                Vector3 sourceScale = source.transform.localScale;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (instance == null) continue;

                instance.transform.position = position;
                instance.transform.rotation = rotation;
                // 缩放用预制体自己的：原对象常被非等比缩放（比如 PlatForm 的 0.6×0.2），
                // 直接沿用会把木板压成 0.096 厚的薄片，子弹容易穿过去、看着也不对
                Vector3 prefabScale = instance.transform.localScale;
                instance.transform.localScale = prefabScale;
                instance.name = source.name;

                if ((sourceScale - prefabScale).sqrMagnitude > 0.0001f)
                {
                    Debug.Log($"[DestructibleProp] {source.name} 的缩放 {sourceScale} 已重置为预制体的 {prefabScale}" +
                              "（要自定义尺寸请直接改这个实例的 Transform）");
                }

                Undo.RegisterCreatedObjectUndo(instance, "Replace With Plank");
                Undo.DestroyObjectImmediate(source);
                EditorSceneManager.MarkSceneDirty(instance.scene);
                replaced++;
            }

            Debug.Log($"[DestructibleProp] 已用木板障碍替换 {replaced} 个对象（Ctrl+Z 可撤销）");
        }

        [MenuItem("9Days/道具/给选中对象加可破坏")]
        private static void MakeDestructible()        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[DestructibleProp] 请先选中要变成可破坏物的对象");
                return;
            }

            int added = 0;
            int skipped = 0;

            for (int i = 0; i < selected.Length; i++)
            {
                GameObject go = selected[i];
                if (go == null) continue;

                if (go.GetComponent<DestructibleProp>() != null)
                {
                    skipped++;
                    continue;
                }

                if (go.GetComponent<Collider2D>() == null)
                {
                    // 没有碰撞体就没有「被子弹撞到」这件事，auto 加一个盒碰撞体更省事
                    var collider = Undo.AddComponent<BoxCollider2D>(go);
                    if (collider == null)
                    {
                        skipped++;
                        continue;
                    }
                }

                Undo.AddComponent<DestructibleProp>(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                added++;
            }

            Debug.Log($"[DestructibleProp] 已添加组件 {added} 个，跳过 {skipped} 个");
        }

        /// <summary>Scene 视图中心（没有打开 Scene 视图时退回世界原点）。</summary>
        private static Vector3 SceneViewPivot()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return Vector3.zero;

            Vector3 pivot = view.pivot;
            pivot.z = 0f;
            return pivot;
        }
    }
}
