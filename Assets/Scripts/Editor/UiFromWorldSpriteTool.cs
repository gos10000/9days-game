using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 把场景里的世界精灵（SpriteRenderer）转成 Canvas 上的 UI Image，让它跟着分辨率自适应。
    ///
    /// 菜单：9Days/UI/把选中的世界精灵转成 UI
    ///
    /// 做法：
    /// 1. 找一个可用的 Screen Space Overlay Canvas；没有就新建一个（CanvasScaler = 1920×1080 / Match 0.5）
    /// 2. 用场景摄像机把精灵的世界包围盒换算成屏幕矩形
    /// 3. 在那个相对屏幕位置上生成 Image，锚点用「屏幕比例」，所以换分辨率不会跑位
    /// 4. 停用原来的 SpriteRenderer（保留对象，方便随时改回来）
    ///
    /// 整步可以用 Ctrl+Z 撤销。
    /// </summary>
    internal static class UiFromWorldSpriteTool
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [MenuItem("9Days/UI/把选中的世界精灵转成 UI")]
        private static void ConvertSelection()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[UiFromWorld] 请先选中要转成 UI 的精灵对象（比如开始界面的 logo2）");
                return;
            }

            var renderers = new System.Collections.Generic.List<SpriteRenderer>();
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null) continue;

                SpriteRenderer[] found = selected[i].GetComponentsInChildren<SpriteRenderer>(true);
                for (int j = 0; j < found.Length; j++)
                {
                    if (found[j] != null && found[j].sprite != null && !renderers.Contains(found[j]))
                        renderers.Add(found[j]);
                }
            }

            if (renderers.Count == 0)
            {
                Debug.LogWarning("[UiFromWorld] 选中的对象里没有带 Sprite 的 SpriteRenderer");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("[UiFromWorld] 场景里没有摄像机，无法把世界坐标换成屏幕坐标");
                return;
            }

            Canvas canvas = FindOrCreateCanvas();
            if (canvas == null)
            {
                Debug.LogError("[UiFromWorld] 创建 Canvas 失败");
                return;
            }

            var canvasRect = (RectTransform)canvas.transform;
            int converted = 0;

            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                Sprite sprite = renderer.sprite;

                Bounds bounds = renderer.bounds;
                Vector3 center = bounds.center;
                Vector3 right = center + Vector3.right * bounds.extents.x;
                Vector3 up = center + Vector3.up * bounds.extents.y;

                Vector3 centerScreen = camera.WorldToScreenPoint(center);
                Vector3 rightScreen = camera.WorldToScreenPoint(right);
                Vector3 upScreen = camera.WorldToScreenPoint(up);

                float pixelWidth = Mathf.Abs(rightScreen.x - centerScreen.x) * 2f;
                float pixelHeight = Mathf.Abs(upScreen.y - centerScreen.y) * 2f;
                if (pixelWidth <= 0f || pixelHeight <= 0f)
                {
                    Debug.LogWarning($"[UiFromWorld] {renderer.name} 在屏幕上尺寸为 0，已跳过");
                    continue;
                }

                // 屏幕比例即锚点：换分辨率时保持相对位置
                Vector2 anchor = new Vector2(
                    Mathf.Clamp01(centerScreen.x / Mathf.Max(1f, Screen.width)),
                    Mathf.Clamp01(centerScreen.y / Mathf.Max(1f, Screen.height)));

                float scaleFactor = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
                Vector2 size = new Vector2(pixelWidth, pixelHeight) / scaleFactor;

                var go = new GameObject(renderer.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(canvasRect, false);
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = size;
                rect.SetAsLastSibling();

                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.color = renderer.color;
                image.preserveAspect = true;
                image.raycastTarget = false;

                Undo.RegisterCreatedObjectUndo(go, "World Sprite To UI");

                // 停用原世界精灵，不要删：方便对照 / 随时改回来
                Undo.RecordObject(renderer, "Disable World Sprite");
                renderer.enabled = false;

                converted++;
                Debug.Log($"[UiFromWorld] {renderer.name} → Canvas/{go.name}\n" +
                          $"  锚点 {anchor}（屏幕比例），尺寸 {size.x:F0}×{size.y:F0}（参考分辨率单位），" +
                          $"原世界尺寸 {bounds.size.x:F2}×{bounds.size.y:F2}");
            }

            EditorSceneManager.MarkSceneDirty(Selection.activeGameObject != null
                ? Selection.activeGameObject.scene
                : UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[UiFromWorld] 完成：{converted} 个精灵已转成 UI（原 SpriteRenderer 已停用，Ctrl+Z 可全部撤销）");
        }

        /// <summary>优先复用已启用的 Overlay Canvas，否则新建一个带 CanvasScaler 的。</summary>
        private static Canvas FindOrCreateCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                if (candidate.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (candidate.transform.parent != null && candidate.transform.parent.GetComponentInParent<Canvas>() != null)
                    continue;

                return candidate;
            }

            var go = new GameObject("StartUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Start UI Canvas");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Debug.Log($"[UiFromWorld] 已新建 {go.name}（Screen Space Overlay，Scale With Screen Size " +
                      $"{ReferenceResolution.x}×{ReferenceResolution.y}，Match 0.5）");
            return canvas;
        }
    }
}
