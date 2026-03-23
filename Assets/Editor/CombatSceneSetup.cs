using System;
using System.IO;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NullProtocol.Editor
{
    public static class CombatSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string EnemyPrefabPath = "Assets/Scenes/Character prefabs/Enemy Dummy.prefab";
        private const string AutoRunKeyPrefix = "NullProtocol.CombatSceneSetup.AutoRun";

        private static readonly Vector3[] EnemyPositions =
        {
            new Vector3(18f, 1.604f, -6f),
            new Vector3(27f, 1.604f, 7f),
            new Vector3(11f, 1.604f, 20f),
            new Vector3(24f, 1.604f, 31f),
            new Vector3(39f, 1.604f, 18f),
        };

        [InitializeOnLoadMethod]
        private static void AutoRunOnceInOpenEditor()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(GetAutoRunKey(), false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoRunInEditor;
        }

        [MenuItem("Tools/Null Protocol/Setup Street Combat")]
        public static void RunFromMenu()
        {
            Run(false);
        }

        private static void TryAutoRunInEditor()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutoRunInEditor;
                return;
            }

            EditorPrefs.SetBool(GetAutoRunKey(), true);
            Run(true);
        }

        private static void Run(bool triggeredAutomatically)
        {
            ConfigureEnemyPrefab();

            var wasLoaded = SceneManager.GetSceneByPath(ScenePath).isLoaded;
            var scene = wasLoaded
                ? SceneManager.GetSceneByPath(ScenePath)
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var stats = new SetupStats();
            ConfigureScenePlayer(scene, stats);
            EnsureStreetEnemies(scene, stats);

            EditorSceneManager.SaveScene(scene);
            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            var summary = stats.ToSummary();
            WriteSummaryLog(summary, triggeredAutomatically);
            Debug.Log(summary);
        }

        private static void ConfigureEnemyPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                var health = GetOrAddComponent<Health>(root);
                health.Team = CombatTeam.Enemy;
                health.MaxHealth = 40f;
                health.DestroyOnDeath = true;

                var weapon = GetOrAddComponent<HitscanWeapon>(root);
                weapon.Team = CombatTeam.Enemy;
                weapon.FirePoint = root.transform.Find("FirePoint");
                weapon.FireRate = 1.1f;
                weapon.Damage = 5f;
                weapon.HitRadius = 0.18f;
                weapon.Range = 55f;
                weapon.DrawDebugShots = false;

                var ai = GetOrAddComponent<EnemyShooterAI>(root);

                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureScenePlayer(Scene scene, SetupStats stats)
        {
            var playerController = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ThirdPersonController>(true))
                .FirstOrDefault();

            if (playerController == null)
            {
                throw new InvalidOperationException("Could not find the ThirdPersonController in SampleScene.");
            }

            var player = playerController.gameObject;
            var health = GetOrAddComponent<Health>(player);
            health.Team = CombatTeam.Player;
            health.MaxHealth = 100f;
            health.DestroyOnDeath = false;

            var weapon = GetOrAddComponent<HitscanWeapon>(player);
            weapon.Team = CombatTeam.Player;
            weapon.FirePoint = null;
            weapon.FireRate = 5f;
            weapon.Damage = 20f;
            weapon.HitRadius = 0f;
            weapon.Range = 90f;
            weapon.DrawDebugShots = false;

            var shooter = GetOrAddComponent<PlayerShooter>(player);
            var deathHandler = GetOrAddComponent<DisableOnDeath>(player);
            deathHandler.Health = health;
            deathHandler.BehavioursToDisable = new Behaviour[]
            {
                playerController,
                shooter,
            };
            deathHandler.CharacterControllerToDisable = player.GetComponent<CharacterController>();
            deathHandler.CollidersToDisable = Array.Empty<Collider>();

            stats.PlayerConfigured = true;
        }

        private static void EnsureStreetEnemies(Scene scene, SetupStats stats)
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemyPrefab == null)
            {
                throw new InvalidOperationException("Could not load Enemy Dummy prefab.");
            }

            var rootObjects = scene.GetRootGameObjects();
            var container = rootObjects.FirstOrDefault(root => root.name == "Street Enemies");
            if (container == null)
            {
                container = new GameObject("Street Enemies");
                SceneManager.MoveGameObjectToScene(container, scene);
            }

            for (var i = 0; i < EnemyPositions.Length; i++)
            {
                var enemyName = $"Street Enemy {i + 1:00}";
                var existing = container.transform.Find(enemyName);
                if (existing == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                    instance.name = enemyName;
                    instance.transform.SetParent(container.transform);
                    instance.transform.position = EnemyPositions[i];
                    instance.transform.rotation = Quaternion.identity;
                    stats.EnemiesSpawned++;
                }
                else
                {
                    existing.position = EnemyPositions[i];
                }
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }

        private static string GetAutoRunKey()
        {
            return $"{AutoRunKeyPrefix}.{Application.dataPath}";
        }

        private static void WriteSummaryLog(string summary, bool triggeredAutomatically)
        {
            var header = triggeredAutomatically ? "Auto-run" : "Manual run";
            var content = $"{header} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{summary}\n";
            var logPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "CombatSceneSetup.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, content);
        }

        private sealed class SetupStats
        {
            public bool PlayerConfigured;
            public int EnemiesSpawned;

            public string ToSummary()
            {
                return
                    "Street combat setup complete.\n" +
                    $"Player configured: {PlayerConfigured}\n" +
                    $"Street enemies spawned: {EnemiesSpawned}";
            }
        }
    }
}
