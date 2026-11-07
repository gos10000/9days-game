using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 生成音效配置表 + AudioRoot 预制体，并把实例放进当前战斗场景。
    ///
    /// 菜单：9Days/Audio/Build Audio Root
    /// 幂等：已存在的资产 / 预制体 / 场景实例只会被更新，不会重复创建。
    /// </summary>
    internal static class AudioSetupBuilder
    {
        private const string AudioRoot = "Assets/Audio";
        private const string TablePath = "Assets/Data/Audio/AudioTable_Default.asset";
        private const string PrefabPath = "Assets/Prefabs/Audio/AudioRoot.prefab";

        [MenuItem("9Days/Audio/Build Audio Root")]
        internal static void BuildMenu()
        {
            Build();
        }

        internal static bool Build()
        {
            AudioTable table = BuildTable();
            if (table == null) return false;

            GameObject prefab = BuildPrefab(table);
            if (prefab == null) return false;

            bool placed = EnsureSceneInstance(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AudioSetup] 完成：配置表 {TablePath}，预制体 {PrefabPath}，场景实例 {(placed ? "已放入" : "已存在")}");
            return true;
        }

        // ---------------- 配置表 ----------------

        private static AudioTable BuildTable()
        {
            AudioClip click = LoadClip("Click.wav");
            AudioClip throwClip = LoadClip("Throw.wav");
            AudioClip boom = LoadClip("Boom.wav");
            AudioClip clear = LoadClip("Clear.wav");
            AudioClip traction = LoadClip("Traction.wav");
            AudioClip gravity = LoadClip("Gravity.wav");
            AudioClip blackHole = LoadClip("BlackHole.wav");
            AudioClip whiteHole = LoadClip("WhiteHole.wav");

            if (click == null || throwClip == null || boom == null || clear == null || traction == null ||
                gravity == null || blackHole == null || whiteHole == null)
            {
                Debug.LogError($"[AudioSetup] {AudioRoot} 里的音效没找齐（需要 Click / Throw / Boom / Clear / Traction / Gravity / BlackHole / WhiteHole 八个 wav）");
                return null;
            }

            AudioTable table = AssetDatabase.LoadAssetAtPath<AudioTable>(TablePath);
            if (table == null)
            {
                EditorAssets.EnsureFolderOfFile(TablePath);
                table = ScriptableObject.CreateInstance<AudioTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.hoverClick = click;
            table.throwClip = throwClip;
            table.burstClip = boom;
            table.clearClip = clear;
            table.tractionClip = traction;

            // 道具行：一个预制体一行，留空才填，避免覆盖手调过的音量
            EnsurePropRow(table, "BlackHole", blackHole, 0.9f);
            EnsurePropRow(table, "BlackHolePro", blackHole, 0.9f);
            EnsurePropRow(table, "WhiteHole", whiteHole, 0.9f);
            EnsurePropRow(table, "WhiteHolePro", whiteHole, 0.9f);
            EnsurePropRow(table, "GravityField", gravity, 0.8f);
            EnsurePropRow(table, "AntigravityField", gravity, 0.8f);

            EditorUtility.SetDirty(table);
            return table;
        }

        private static void EnsurePropRow(AudioTable table, string prefabName, AudioClip clip, float volume)
        {
            string path = $"Assets/Prefabs/Projectiles/Prop/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[AudioSetup] 找不到道具预制体 {path}，这一行跳过");
                return;
            }

            AudioTable.PropSound existing = table.FindProp(prefab);
            if (existing != null)
            {
                // 音效文件被替换过（guid 会变），这里始终按路径重新指一次，避免留下空引用
                existing.clip = clip;
                return;
            }

            table.props.Add(new AudioTable.PropSound
            {
                prefab = prefab,
                clip = clip,
                volume = volume,
            });
        }

        // ---------------- 预制体 ----------------

        private static GameObject BuildPrefab(AudioTable table)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                var service = existing.GetComponent<AudioService>();
                if (service != null)
                {
                    var so = new SerializedObject(service);
                    so.FindProperty("table").objectReferenceValue = table;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            var root = new GameObject("AudioRoot");
            AudioService created = root.AddComponent<AudioService>();

            var sfx = root.AddComponent<AudioSource>();
            ConfigureSource(sfx);

            var ui = root.AddComponent<AudioSource>();
            ConfigureSource(ui);

            var createdSo = new SerializedObject(created);
            createdSo.FindProperty("table").objectReferenceValue = table;
            createdSo.FindProperty("sfxSource").objectReferenceValue = sfx;
            createdSo.FindProperty("uiSource").objectReferenceValue = ui;
            createdSo.ApplyModifiedPropertiesWithoutUndo();

            EditorAssets.EnsureFolderOfFile(PrefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[AudioSetup] 生成预制体 {PrefabPath}");
            return prefab;
        }

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.ignoreListenerPause = true;
        }

        // ---------------- 场景实例 ----------------

        private static bool EnsureSceneInstance(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return false;

            AudioService[] found = Object.FindObjectsByType<AudioService>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject.scene == scene) return false;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) return false;

            Undo.RegisterCreatedObjectUndo(instance, "Create AudioRoot");
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static AudioClip LoadClip(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/{fileName}");
        }
    }

    /// <summary>
    /// 一次性自动执行：存在 Temp/audio_builder.flag 时，编辑器空闲后自动跑一遍 Build()，成功才删标记。
    /// </summary>
    [InitializeOnLoad]
    internal static class AudioSetupAutoRun
    {
        private const string FlagPath = "Temp/audio_builder.flag";
        private static bool hooked;

        static AudioSetupAutoRun()
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
                AudioSetupBuilder.Build();
                System.IO.File.Delete(FlagPath);
                Unhook();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[AudioSetup] 自动执行失败：" + exception);
                Unhook();
            }
        }
    }
}
