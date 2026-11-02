using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成自建的测试场景（不引用 demo 的 MainScene / Background / 空气墙）。
    /// 地面、障碍、空气墙全部由代码创建，场景可随时重建。
    /// </summary>
    internal static class ArenaBuilder
    {
        internal static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.13f, 0.19f);
            cameraGo.AddComponent<AudioListener>();

            var arenaGo = new GameObject("ArenaBounds");
            var arena = arenaGo.AddComponent<ArenaBounds>();
            EditorAssets.SetRect(arena, "playArea", new Rect(-8f, -5f, 16f, 10f));
            EditorAssets.SetBool(arena, "createInvisibleWalls", true);
            EditorAssets.SetString(arena, "obstacleLayerName", "Obstacle");
            EditorAssets.SetBool(arena, "recoverOutOfBounds", true);
            EditorAssets.SetFloat(arena, "checkInterval", 0.2f);

            Sprite square = EditorAssets.LoadSquareSprite();
            CreateStaticBlock("Ground", new Vector2(0f, -5f), new Vector2(16f, 0.6f),
                              new Color(0.25f, 0.28f, 0.34f), square);
            CreateStaticBlock("Obstacle_Block", new Vector2(0f, -4f), new Vector2(1f, 1.4f),
                              new Color(0.9f, 0.6f, 0.2f), square);

            GameObject p1 = InstantiateCharacter(EditorAssets.CharacterP1Path, new Vector3(-5f, -3.7f, 0f), Team.Player1);
            GameObject p2 = InstantiateCharacter(EditorAssets.CharacterP2Path, new Vector3(5f, -3.7f, 0f), Team.Player2);

            var turnGo = new GameObject("TurnController");
            var turn = turnGo.AddComponent<TurnController>();
            EditorAssets.SetObjectArray(turn, "characters", new UnityEngine.Object[]
            {
                p1.GetComponent<Character>(),
                p2.GetComponent<Character>(),
            });
            EditorAssets.SetInt(turn, "firstTeam", (int)Team.Player1);
            EditorAssets.SetBool(turn, "autoStart", false);
            EditorAssets.SetBool(turn, "autoAdvance", false);
            EditorAssets.SetFloat(turn, "resolveTimeout", 3f);

            BattleConfig battleConfig = AssetDatabase.LoadAssetAtPath<BattleConfig>(EditorAssets.BattleConfigPath);
            if (battleConfig == null) throw new System.InvalidOperationException("找不到 BattleConfig，请先跑 Build/Prefabs");

            var battleGo = new GameObject("BattleController");
            var battle = battleGo.AddComponent<BattleController>();
            EditorAssets.SetRef(battle, "config", battleConfig);
            EditorAssets.SetRef(battle, "turnController", turn);
            EditorAssets.SetObjectArray(battle, "characters", new UnityEngine.Object[]
            {
                p1.GetComponent<Character>(),
                p2.GetComponent<Character>(),
            });
            EditorAssets.SetBool(battle, "autoStart", true);

            var hudGo = new GameObject("DebugHUD");
            var hud = hudGo.AddComponent<CombatDebugHud>();
            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.TestBallPath);
            if (ballPrefab == null) throw new System.InvalidOperationException("找不到 TestBall prefab");

            EditorAssets.SetRef(hud, "turnController", turn);
            EditorAssets.SetRef(hud, "battleController", battle);
            EditorAssets.SetRef(hud, "testProjectilePrefab", ballPrefab.GetComponent<Projectile>());
            EditorAssets.SetBool(hud, "showHud", true);
            EditorAssets.SetBool(hud, "verboseImpactLog", false);

            var banner = hudGo.AddComponent<TurnBanner>();
            EditorAssets.SetBool(banner, "showBanner", true);
            EditorAssets.SetFloat(banner, "holdSeconds", 1.2f);
            EditorAssets.SetFloat(banner, "fadeSeconds", 0.25f);

            var poolGo = new GameObject("ProjectilePool");
            var pool = poolGo.AddComponent<ProjectilePool>();
            EditorAssets.SetRef(pool, "prewarmPrefab", ballPrefab.GetComponent<Projectile>());
            EditorAssets.SetInt(pool, "prewarmCount", 8);
            EditorAssets.SetInt(pool, "defaultCapacity", 8);
            EditorAssets.SetInt(pool, "maxSizePerPrefab", 64);
            EditorAssets.SetBool(pool, "dontDestroyOnLoad", false);

            EditorAssets.EnsureFolder(EditorAssets.SceneRoot);
            EditorSceneManager.SaveScene(scene, EditorAssets.TestArenaScenePath);
            EditorSceneManager.OpenScene(EditorAssets.TestArenaScenePath);

            Debug.Log($"[ArenaBuilder] 测试场景: {EditorAssets.TestArenaScenePath}");
        }

        private static GameObject InstantiateCharacter(string path, Vector3 position, Team expectedTeam)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.InvalidOperationException($"找不到角色 prefab: {path}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            instance.name = $"Character_{expectedTeam}";
            return instance;
        }

        private static void CreateStaticBlock(string name, Vector2 center, Vector2 size, Color color, Sprite sprite)
        {
            int layer = LayerMask.NameToLayer("Obstacle");
            if (layer < 0) layer = 0;

            var root = new GameObject(name);
            root.layer = layer;
            root.transform.position = center;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);

            var renderer = spriteGo.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 0;
            EditorAssets.ScaleToWorldSize(spriteGo.transform, sprite, size);

            var box = root.AddComponent<BoxCollider2D>();
            box.size = size;
        }
    }
}
