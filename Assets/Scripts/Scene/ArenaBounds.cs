using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 场地边界（文档「场景」）。逻辑边界用矩形描述，不依赖任何墙物体：
    /// 可自己生成空气墙，测试时在 Scene 视图画出边界，以后场景重做只需调 Rect。
    /// </summary>
    public class ArenaBounds : MonoBehaviour
    {
        [Header("可玩区域（世界坐标）")]
        [SerializeField] private Rect playArea = new Rect(-8f, -5f, 16f, 10f);

        [Header("空气墙")]
        [SerializeField] private bool createInvisibleWalls = true;
        [SerializeField] private string obstacleLayerName = "Obstacle";
        [SerializeField] private float wallThickness = 1f;
        [SerializeField] private float wallExtend = 2f;

        [Header("出界兜底")]
        [SerializeField] private bool recoverOutOfBounds = true;
        [SerializeField] private float checkInterval = 0.2f;

        [Header("Gizmos")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 1f, 0.6f, 0.8f);

        private Transform wallsRoot;
        private float checkTimer;

        public Rect PlayArea => playArea;
        public bool Contains(Vector2 worldPosition) => playArea.Contains(worldPosition);

        private void Awake()
        {
            if (createInvisibleWalls) BuildWalls();
        }

        private void Update()
        {
            if (!recoverOutOfBounds) return;

            checkTimer += Time.deltaTime;
            if (checkTimer < checkInterval) return;
            checkTimer = 0f;

            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character c = characters[i];
                if (c == null || Contains(c.transform.position)) continue;

                GameEvents.RaiseCharacterOutOfBounds(c);
                c.ResetToSpawn();
            }
        }

        private void BuildWalls()
        {
            if (wallsRoot != null) return;

            var root = new GameObject("ArenaWalls");
            root.transform.SetParent(transform, false);
            wallsRoot = root.transform;

            int layer = LayerMask.NameToLayer(obstacleLayerName);
            if (layer < 0) layer = 0;

            float halfW = playArea.width * 0.5f;
            float halfH = playArea.height * 0.5f;
            Vector2 center = playArea.center;

            CreateWall("WallLeft", center + Vector2.left * (halfW + wallThickness * 0.5f),
                       new Vector2(wallThickness, playArea.height + wallExtend * 2f), layer);
            CreateWall("WallRight", center + Vector2.right * (halfW + wallThickness * 0.5f),
                       new Vector2(wallThickness, playArea.height + wallExtend * 2f), layer);
            CreateWall("WallTop", center + Vector2.up * (halfH + wallThickness * 0.5f),
                       new Vector2(playArea.width + wallExtend * 2f, wallThickness), layer);
            CreateWall("WallBottom", center + Vector2.down * (halfH + wallThickness * 0.5f),
                       new Vector2(playArea.width + wallExtend * 2f, wallThickness), layer);
        }

        private void CreateWall(string wallName, Vector2 position, Vector2 size, int layer)
        {
            var go = new GameObject(wallName);
            go.layer = layer;
            go.transform.SetParent(wallsRoot, false);
            go.transform.position = position;

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = size;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(playArea.center, new Vector3(playArea.width, playArea.height, 0f));
        }
    }
}
