using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 运行时道具单元。表格里填的「图标 + 预制体」就是它，
    /// 占地尺寸从 prefab 根碰撞体推导，不用在表里再填一遍。
    /// </summary>
    public readonly struct PropSpec
    {
        public readonly GameObject Prefab;
        public readonly Sprite Icon;
        public readonly string Name;
        public readonly int Count;

        public PropSpec(GameObject prefab, Sprite icon, string name, int count = 1)
        {
            Prefab = prefab;
            Icon = icon;
            Name = name;
            Count = Mathf.Max(1, count);
        }

        public bool IsValid => Prefab != null;

        public string ResolvedName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name)) return Name;
                return Prefab != null ? Prefab.name : "Unnamed";
            }
        }

        /// <summary>图标优先用表里填的，缺失时退回 prefab 自身精灵。</summary>
        public Sprite ResolvedIcon
        {
            get
            {
                if (Icon != null) return Icon;
                if (Prefab == null) return null;
                SpriteRenderer renderer = Prefab.GetComponentInChildren<SpriteRenderer>(true);
                return renderer != null ? renderer.sprite : null;
            }
        }

        public Vector2 Footprint
        {
            get
            {
                if (Prefab == null) return Vector2.one;

                Vector3 scale = Prefab.transform.localScale;
                float scaleX = Mathf.Abs(scale.x);
                float scaleY = Mathf.Abs(scale.y);

                Collider2D collider = Prefab.GetComponent<Collider2D>();
                if (collider is BoxCollider2D box)
                    return new Vector2(box.size.x * scaleX, box.size.y * scaleY);

                if (collider is CircleCollider2D circle)
                {
                    float diameter = circle.radius * 2f * Mathf.Max(scaleX, scaleY);
                    return new Vector2(diameter, diameter);
                }

                if (collider is CapsuleCollider2D capsule)
                    return new Vector2(capsule.size.x * scaleX, capsule.size.y * scaleY);

                return Vector2.one;
            }
        }
    }
}
