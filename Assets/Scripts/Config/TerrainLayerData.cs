using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>一个美术 Sprite 在层里的摆放；运行时据此重建视觉。</summary>
    [Serializable]
    public struct TerrainArtPiece
    {
        public Sprite sprite;
        public Vector3 position;
        public Vector3 eulerAngles;
        public Vector3 scale;
        public Color color;
        public string sortingLayerName;
        public int sortingOrder;
        public bool flipX;
        public bool flipY;
        public SpriteDrawMode drawMode;
        public Vector2 size;
        public SpriteTileMode tileMode;

        /// <summary>生成默认占位层时使用的简单 Sprite。</summary>
        public static TerrainArtPiece Create(Sprite sprite, Vector3 position, Vector3 scale, Color color,
                                             int sortingOrder = -10)
        {
            return new TerrainArtPiece
            {
                sprite = sprite,
                position = position,
                eulerAngles = Vector3.zero,
                scale = scale,
                color = color,
                sortingLayerName = "Default",
                sortingOrder = sortingOrder,
                drawMode = SpriteDrawMode.Simple,
                size = Vector2.one,
                tileMode = SpriteTileMode.Continuous
            };
        }
    }

    /// <summary>一个地形层：视觉、初始遮罩、可破坏性与是否参与物理。</summary>
    [Serializable]
    public class TerrainLayerData
    {
        public string layerId = "Layer";
        public bool destructible;
        public float requiredBreakPower;
        public bool collisionEnabled = true;
        public float alphaThreshold = 0.5f;
        public Texture2D initialMask;
        public List<TerrainArtPiece> art = new List<TerrainArtPiece>();

        public bool HasArt => art != null && art.Count > 0;

        /// <summary>把非法数值夹到可用范围。</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(layerId)) layerId = "Layer";
            requiredBreakPower = Mathf.Max(0f, requiredBreakPower);
            alphaThreshold = Mathf.Clamp01(alphaThreshold);
            if (art == null) art = new List<TerrainArtPiece>();
        }
    }
}
