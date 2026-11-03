using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 地形物理根：把并集遮罩生成的轮廓变成多个 EdgeCollider2D。
    /// 每次抹除/追加后整体重建，保证边界与遮罩一致。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TerrainPhysics : MonoBehaviour
    {
        private Rigidbody2D body;
        private readonly List<GameObject> contourObjects = new List<GameObject>();

        public int ContourCount => contourObjects.Count;

        private void Awake()
        {
            EnsureComponents();
        }

        /// <summary>初始化静态刚体。</summary>
        public void Initialize(int layer)
        {
            EnsureComponents();

            gameObject.layer = layer;
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
        }

        /// <summary>用新的轮廓集合重建碰撞；空集合会清掉全部边界。</summary>
        public void Rebuild(IReadOnlyList<Vector2[]> contours, int layer, float radius)
        {
            EnsureComponents();
            ClearContours();

            if (contours != null)
            {
                for (int i = 0; i < contours.Count; i++)
                {
                    Vector2[] points = contours[i];
                    if (points == null || points.Length < 4) continue;

                    var go = new GameObject($"Contour_{i}");
                    go.layer = layer;
                    go.transform.SetParent(transform, false);

                    var edge = go.AddComponent<EdgeCollider2D>();
                    edge.points = points;
                    edge.edgeRadius = Mathf.Max(0f, radius);

                    contourObjects.Add(go);
                }
            }
        }

        /// <summary>清掉全部轮廓子对象，等待下一次重建。</summary>
        public void ClearContours()
        {
            for (int i = 0; i < contourObjects.Count; i++)
            {
                if (contourObjects[i] == null) continue;
                contourObjects[i].SetActive(false);
                Destroy(contourObjects[i]);
            }
            contourObjects.Clear();
        }

        private void EnsureComponents()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Static;
        }
    }
}
