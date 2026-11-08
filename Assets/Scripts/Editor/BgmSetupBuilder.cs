using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成 BGM 配置表 + BgmRoot 预制体，并把实例放进当前场景（按场景名自动选轨道）。
    ///
    /// 音乐文件直接用 <c>Assets/Audio/Music</c> 里的现成资源，不做任何复制：
    /// 表里能找到就自动填上，找不到就留空，你在 Inspector 里拖。
    ///
    /// 菜单：9Days/Audio/Build BGM Table
    /// 幂等：已存在的资产 / 预制体 / 场景实例只更新，不重复创建。
    /// </summary>
    internal static class BgmSetupBuilder
    {
        private const string BgmFolder = "Assets/Audio/Music";
        private const string TablePath = "Assets/Data/Audio/BgmTable_Default.asset";
        private const string PrefabPath = "Assets/Prefabs/Audio/BgmRoot.prefab";

        [MenuItem("9Days/Audio/Build BGM Table")]
        internal static void BuildMenu()
        {
            Build();
        }

        internal static bool Build()
        {
            AudioClip startClip = FindClip("Bgm_Start.wav", "StartTheme_loop.wav");
            AudioClip battleClip = FindClip("Bgm_Battle.wav", "BattleTheme_loop.wav", "BattleTheme_loop_x2.wav");
            AudioClip resultClip = FindClip("Bgm_Result.wav", "ResultTheme_loop.wav");

            if (startClip == null) Debug.LogWarning($"[BgmSetup] {BgmFolder} 里没找到开始界面音乐，表里这一行留空，手动拖 clip 即可");
            if (battleClip == null) Debug.LogWarning($"[BgmSetup] {BgmFolder} 里没找到战斗音乐，表里这一行留空，手动拖 clip 即可");
            if (resultClip == null) Debug.Log($"[BgmSetup] {BgmFolder} 里还没放结算音乐，表里 Result 行留空");

            BgmTable table = BuildTable(startClip, battleClip, resultClip);
            if (table == null) return false;

            GameObject prefab = BuildPrefab(table);
            if (prefab == null) return false;

            bool placed = EnsureSceneInstance(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BgmSetup] 完成：表 {TablePath}，预制体 {PrefabPath}，场景实例 {(placed ? "已放入" : "已存在")}");
            return true;
        }

        // ---------------- 音乐文件 ----------------

        /// <summary>在 Assets/Audio/Music 里按文件名找 clip；找不到返回 null（不报错，你手动拖）。</summary>
        private static AudioClip FindClip(params string[] fileNames)
        {
            for (int i = 0; i < fileNames.Length; i++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{BgmFolder}/{fileNames[i]}");
                if (clip != null) return clip;
            }

            return null;
        }

        // ---------------- 配置表 ----------------

        private static BgmTable BuildTable(AudioClip startClip, AudioClip battleClip, AudioClip resultClip)
        {
            BgmTable table = AssetDatabase.LoadAssetAtPath<BgmTable>(TablePath);
            if (table == null)
            {
                EditorAssets.EnsureFolderOfFile(TablePath);
                table = ScriptableObject.CreateInstance<BgmTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            EnsureRow(table, BgmTrack.Start, startClip, 0.55f);
            EnsureRow(table, BgmTrack.Battle, battleClip, 0.50f);
            EnsureRow(table, BgmTrack.Result, resultClip, 0.55f);

            EditorUtility.SetDirty(table);
            return table;
        }

        private static void EnsureRow(BgmTable table, BgmTrack track, AudioClip clip, float volume)
        {
            BgmTable.Row row = table.GetOrAddRow(track);
            if (row.clip == null) row.clip = clip;
            if (row.volume <= 0f) row.volume = volume;
        }

        // ---------------- 预制体 ----------------

        private static GameObject BuildPrefab(BgmTable table)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                var service = existing.GetComponent<BgmService>();
                if (service != null)
                {
                    var so = new SerializedObject(service);
                    so.FindProperty("table").objectReferenceValue = table;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            var root = new GameObject("BgmRoot");
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerPause = true;

            BgmService created = root.AddComponent<BgmService>();
            var ser = new SerializedObject(created);
            ser.FindProperty("table").objectReferenceValue = table;
            ser.FindProperty("source").objectReferenceValue = source;
            ser.ApplyModifiedPropertiesWithoutUndo();

            EditorAssets.EnsureFolderOfFile(PrefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[BgmSetup] 生成预制体 {PrefabPath}");
            return prefab;
        }

        // ---------------- 场景实例 ----------------

        private static bool EnsureSceneInstance(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return false;

            BgmService[] found = Object.FindObjectsByType<BgmService>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject.scene == scene) return false;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) return false;

            // 按场景名猜轨道：名字里带 Battle 的当战斗，其余当开始界面
            BgmTrack track = scene.name.IndexOf("Battle", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? BgmTrack.Battle
                : BgmTrack.Start;

            var service = instance.GetComponent<BgmService>();
            if (service != null)
            {
                var so = new SerializedObject(service);
                so.FindProperty("track").enumValueIndex = (int)track;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(service);
            }

            Undo.RegisterCreatedObjectUndo(instance, "Create BgmRoot");
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[BgmSetup] 场景 {scene.name} 放入 BgmRoot，轨道 = {track}");
            return true;
        }
    }

    /// <summary>一次性自动执行：存在 Temp/bgm_builder.flag 时自动跑一遍 Build()，成功才删标记。</summary>
    [InitializeOnLoad]
    internal static class BgmSetupAutoRun
    {
        private const string FlagPath = "Temp/bgm_builder.flag";
        private static bool hooked;

        static BgmSetupAutoRun()
        {
            if (!System.IO.File.Exists(FlagPath)) return;
            Hook();
            EditorApplication.delayCall += Run;
        }

        private static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused) EditorApplication.delayCall += Run;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += Run;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (!System.IO.File.Exists(FlagPath))
            {
                Unhook();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            try
            {
                BgmSetupBuilder.Build();
                System.IO.File.Delete(FlagPath);
                Unhook();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[BgmSetup] 自动执行失败：" + exception);
                Unhook();
            }
        }
    }
}
