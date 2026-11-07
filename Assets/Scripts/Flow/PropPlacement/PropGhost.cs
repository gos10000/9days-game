using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 放置预览体：把真实道具 prefab 克隆一份，剥掉所有玩法逻辑，只留外形。
    /// 这样「预览看到的」和「放下后的」必然是同一个东西，不会出现图标与实体不一致。
    /// </summary>
    public sealed class PropGhost : MonoBehaviour
    {
        private static readonly Color ValidTint = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color InvalidTint = new Color(1f, 0.32f, 0.32f, 0.45f);

        private SpriteRenderer[] renderers;
        private Color[] baseColors;

        /// <summary>克隆并冻结成幽灵；返回 null 表示 prefab 为空。</summary>
        public static PropGhost Create(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;

            GameObject go = Object.Instantiate(prefab, parent);
            go.name = "PropGhost_" + prefab.name;

            // 先停用再剥逻辑：直接 Instantiate 的话 ProjectileField.OnEnable 会先跑一次，
            // 环境音会被塞进播放队列，出现「还没放下就响了」。
            go.SetActive(false);

            Collider2D[] colliders = go.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Rigidbody2D[] bodies = go.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
                bodies[i].simulated = false;

            ProjectileField[] fields = go.GetComponentsInChildren<ProjectileField>(true);
            for (int i = 0; i < fields.Length; i++)
                fields[i].enabled = false;

            PropGhost ghost = go.AddComponent<PropGhost>();
            ghost.renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            ghost.baseColors = new Color[ghost.renderers.Length];
            for (int i = 0; i < ghost.renderers.Length; i++)
            {
                ghost.baseColors[i] = ghost.renderers[i].color;
                ghost.renderers[i].sortingOrder += 100;
            }

            go.SetActive(true);
            ghost.SetValid(true);
            return ghost;
        }

        public void SetPose(Vector2 position, bool valid)
        {
            transform.position = position;
            SetValid(valid);
        }

        public void SetValid(bool valid)
        {
            Color tint = valid ? ValidTint : InvalidTint;

            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color baseColor = baseColors[i];
                renderers[i].color = new Color(
                    baseColor.r * tint.r,
                    baseColor.g * tint.g,
                    baseColor.b * tint.b,
                    tint.a);
            }
        }
    }
}
