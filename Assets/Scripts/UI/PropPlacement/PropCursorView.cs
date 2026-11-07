using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 放置阶段的屏幕图标：跟随鼠标、按落点合法性变色。
    /// 只做表现，不参与任何命中判定（raycastTarget = false）。
    /// </summary>
    public sealed class PropCursorView : MonoBehaviour
    {
        private Image icon;
        private RectTransform iconRect;

        /// <summary>运行时创建一块独立 Overlay 画布，不依赖场景 UI 结构。</summary>
        public static PropCursorView Create(int sortingOrder = 500)
        {
            var go = new GameObject("PropCursorCanvas");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            PropCursorView view = go.AddComponent<PropCursorView>();
            view.Build();
            go.SetActive(false);
            return view;
        }

        private void Build()
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(transform, false);

            iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
        }

        public void Show(Sprite sprite, float maxSize = 96f)
        {
            if (icon == null) return;

            icon.sprite = sprite;
            icon.enabled = sprite != null;

            if (sprite != null)
            {
                Vector2 size = sprite.rect.size;
                float longest = Mathf.Max(size.x, size.y, 1f);
                float scale = Mathf.Min(1f, maxSize / longest);
                iconRect.sizeDelta = size * scale;
            }
            else
            {
                iconRect.sizeDelta = new Vector2(maxSize, maxSize);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void SetPointer(Vector2 screenPoint, bool valid)
        {
            if (iconRect != null)
                iconRect.position = new Vector3(screenPoint.x, screenPoint.y, 0f);

            if (icon != null)
                icon.color = valid
                    ? new Color(1f, 1f, 1f, 0.95f)
                    : new Color(1f, 0.35f, 0.35f, 0.95f);
        }

        /// <summary>指针是否压在 UI 上；放在 UI 程序集里，避免 Flow 直接依赖 UnityEngine.UI。</summary>
        public static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
