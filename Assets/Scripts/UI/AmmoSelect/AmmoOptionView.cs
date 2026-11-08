using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>
    /// 选卡页的单个选项：卡片底图、图标、名称、介绍、按钮。
    ///
    /// 卡片美术分两层：根节点是透明的点击区，真正的图片挂在子物体 Art 上。
    /// 原因是 item2（选中态）比 item1 宽——多出来的部分只有右下角那块贴纸，
    /// 卡片本体宽度是一样的。如果把 item2 硬塞进 item1 的矩形，本体就会被压窄，
    /// 里面的文字就超出卡面。这里改成「左边不动、右边向右延展」。
    /// </summary>
    public sealed class AmmoOptionView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("内容")]
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button button;

        [Header("卡片美术")]
        [Tooltip("真正显示卡片图片的子物体；为空则不参与换图")]
        [SerializeField] private Image artImage;

        [Tooltip("常态图（item1）")]
        [SerializeField] private Sprite normalArt;

        [Tooltip("悬停图（item2）")]
        [SerializeField] private Sprite selectedArt;

        [Tooltip("悬停时右侧额外延展的宽度；左边和上下边不动。约等于「图2 比图1 宽出的比例 × 卡宽」")]
        [SerializeField] private float selectedExtraWidth = 22f;

        private Action onClick;
        private RectTransform artRect;
        private Vector2 artSize;
        private Vector2 artPosition;
        private bool artBaseCaptured;
        private bool hovered;

        private void Awake()
        {
            ResolveRefs();
            CaptureArtBase();
            ApplyArt(false);
        }

        private void OnDisable()
        {
            // 模板会被反复克隆 / 池化，隐藏时复位，避免留下选中态
            hovered = false;
            ApplyArt(false);
        }

        /// <summary>绑定一条纯 UI 数据与点击回调。</summary>
        public void Bind(AmmoOption option, Action callback)
        {
            ResolveRefs();
            onClick = callback;

            if (icon != null)
            {
                icon.sprite = option != null ? option.Icon : null;
                icon.enabled = icon.sprite != null;
            }

            if (nameText != null) nameText.text = option != null ? option.Name : string.Empty;
            if (descriptionText != null) descriptionText.text = option != null ? option.Description : string.Empty;

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }

            CaptureArtBase();
            ApplyArt(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            ApplyArt(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            ApplyArt(false);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick() => onClick?.Invoke();

        /// <summary>选中态：换图 + 右边缘向外延展，左边缘保持不动。</summary>
        private void ApplyArt(bool selected)
        {
            if (artImage == null) return;
            if (!artBaseCaptured) CaptureArtBase();

            artImage.sprite = selected && selectedArt != null ? selectedArt : normalArt;

            if (artRect == null) return;

            float extra = selected ? Mathf.Max(0f, selectedExtraWidth) : 0f;
            artRect.sizeDelta = new Vector2(artSize.x + extra, artSize.y);
            // 中心右移一半 → 左边固定，右边多出 extra
            artRect.anchoredPosition = new Vector2(artPosition.x + extra * 0.5f, artPosition.y);
        }

        /// <summary>记录 item1 状态下的尺寸与位置作为基准（只在非悬停时采）。</summary>
        private void CaptureArtBase()
        {
            if (artBaseCaptured || hovered) return;
            if (artImage == null) artImage = transform.Find("Art")?.GetComponent<Image>();
            if (artImage == null) return;

            artRect = artImage.rectTransform;
            if (artRect == null) return;

            artSize = artRect.sizeDelta;
            artPosition = artRect.anchoredPosition;
            artBaseCaptured = true;
        }

        /// <summary>模板可能是 inactive，不能只依赖 Awake。</summary>
        private void ResolveRefs()
        {
            if (button == null) button = GetComponent<Button>();
            if (button == null) button = GetComponentInChildren<Button>(true);

            // 图标必须按名字找：卡片新增了 Art 子图，GetComponentInChildren 会先拿到它
            if (icon == null)
            {
                Transform iconChild = transform.Find("Icon");
                if (iconChild != null) icon = iconChild.GetComponent<Image>();
            }

            if (artImage == null)
            {
                Transform artChild = transform.Find("Art");
                if (artChild != null) artImage = artChild.GetComponent<Image>();
            }

            if (nameText == null) nameText = FindText("Name");
            if (descriptionText == null) descriptionText = FindText("Description");
        }

        private Text FindText(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }
    }
}
