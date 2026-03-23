using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NullProtocol.Editor
{
    public static class PrimitiveColliderBatchProcessor
    {
        private const float MinColliderThickness = 0.05f;
        private const string AutoRunKeyPrefix = "NullProtocol.PrimitiveColliderBatchProcessor.AutoRun";
        private static readonly string[] ExcludedPrefabPaths =
        {
            "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab",
            "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerCapsule.prefab",
            "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab",
        };

        private static readonly string[] ExcludedRootNames =
        {
            "PlayerArmature",
            "PlayerCapsule",
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

        [MenuItem("Tools/Null Protocol/Add Missing Primitive Colliders")]
        public static void RunFromMenu()
        {
            Run(false);
        }

        public static void RunFromCommandLine()
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
            var stats = new ProcessingStats();

            ProcessPrefabAssets(stats);
            ProcessScenes(stats);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = stats.ToSummary();
            WriteSummaryLog(summary, triggeredAutomatically);
            Debug.Log(summary);
        }

        private static void ProcessPrefabAssets(ProcessingStats stats)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || IsExcludedPrefabPath(path))
                {
                    stats.SkippedPrefabs++;
                    continue;
                }

                var prefabType = PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadMainAssetAtPath(path));
                if (prefabType == PrefabAssetType.Model)
                {
                    stats.SkippedModelPrefabs++;
                    continue;
                }

                stats.ScannedPrefabs++;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (IsExcludedRoot(root))
                    {
                        stats.SkippedPrefabs++;
                        continue;
                    }

                    if (AddCollidersToHierarchy(root, stats))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        stats.ModifiedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ProcessScenes(ProcessingStats stats)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                stats.ScannedScenes++;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var changed = false;

                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    if (IsExcludedRoot(rootObject))
                    {
                        continue;
                    }

                    changed |= AddCollidersToHierarchy(rootObject, stats);
                }

                if (changed)
                {
                    EditorSceneManager.SaveScene(scene);
                    stats.ModifiedScenes++;
                }
            }
        }

        private static bool AddCollidersToHierarchy(GameObject root, ProcessingStats stats)
        {
            var changed = false;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var current = transform.gameObject;
                if (ShouldSkipObject(current, root.transform))
                {
                    continue;
                }

                if (!TryGetLocalBounds(current, out var localBounds))
                {
                    continue;
                }

                var shape = ChooseShape(localBounds.size, current.name);
                AddCollider(current, localBounds, shape);
                changed = true;
                stats.CollidersAdded++;

                switch (shape)
                {
                    case ColliderShape.Box:
                        stats.BoxCollidersAdded++;
                        break;
                    case ColliderShape.Capsule:
                        stats.CapsuleCollidersAdded++;
                        break;
                    case ColliderShape.Sphere:
                        stats.SphereCollidersAdded++;
                        break;
                }
            }

            return changed;
        }

        private static bool ShouldSkipObject(GameObject gameObject, Transform root)
        {
            if (!gameObject.activeInHierarchy && PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                // Disabled prefab children still need colliders if they're later enabled.
            }

            if (IsExcludedRoot(gameObject))
            {
                return true;
            }

            if (gameObject.GetComponent<Collider>() != null || gameObject.GetComponent<CharacterController>() != null)
            {
                return true;
            }

            var current = gameObject.transform.parent;
            while (current != null)
            {
                if (current.GetComponent<Collider>() != null || current.GetComponent<CharacterController>() != null)
                {
                    return true;
                }

                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool TryGetLocalBounds(GameObject gameObject, out Bounds localBounds)
        {
            if (gameObject.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                localBounds = meshFilter.sharedMesh.bounds;
                return IsUsable(localBounds.size);
            }

            if (gameObject.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer) && skinnedMeshRenderer.sharedMesh != null)
            {
                localBounds = skinnedMeshRenderer.localBounds;
                return IsUsable(localBounds.size);
            }

            localBounds = default;
            return false;
        }

        private static bool IsUsable(Vector3 size)
        {
            return size.sqrMagnitude > 0.0001f;
        }

        private static ColliderShape ChooseShape(Vector3 size, string objectName)
        {
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            var axis = LargestAxis(size);
            var largest = AxisValue(size, axis);
            var middle = MiddleValue(size);
            var smallest = SmallestValue(size);
            var aspect = smallest <= 0.0001f ? float.MaxValue : largest / smallest;

            var loweredName = objectName.ToLowerInvariant();
            if (loweredName.Contains("barrel") || loweredName.Contains("cylinder") || loweredName.Contains("bollard") ||
                loweredName.Contains("pole") || loweredName.Contains("post") || loweredName.Contains("cone"))
            {
                return ColliderShape.Capsule;
            }

            if (loweredName.Contains("ball") || loweredName.Contains("sphere"))
            {
                return ColliderShape.Sphere;
            }

            if (aspect <= 1.25f)
            {
                return ColliderShape.Sphere;
            }

            var secondarySimilarity = middle <= 0.0001f ? 0f : Mathf.Abs(middle - smallest) / middle;
            if (largest >= middle * 1.5f && secondarySimilarity <= 0.35f)
            {
                return ColliderShape.Capsule;
            }

            return ColliderShape.Box;
        }

        private static void AddCollider(GameObject gameObject, Bounds bounds, ColliderShape shape)
        {
            switch (shape)
            {
                case ColliderShape.Sphere:
                {
                    var collider = gameObject.AddComponent<SphereCollider>();
                    collider.center = bounds.center;
                    collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, MinColliderThickness * 0.5f);
                    break;
                }
                case ColliderShape.Capsule:
                {
                    var collider = gameObject.AddComponent<CapsuleCollider>();
                    var axis = LargestAxis(bounds.size);
                    var size = bounds.size;
                    var radius = 0.5f * Mathf.Max(
                        axis == 0 ? size.y : size.x,
                        axis == 2 ? size.y : size.z,
                        MinColliderThickness);

                    collider.center = bounds.center;
                    collider.direction = axis;
                    collider.radius = radius;
                    collider.height = Mathf.Max(AxisValue(size, axis), radius * 2f, MinColliderThickness);
                    break;
                }
                default:
                {
                    var collider = gameObject.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = new Vector3(
                        Mathf.Max(Mathf.Abs(bounds.size.x), MinColliderThickness),
                        Mathf.Max(Mathf.Abs(bounds.size.y), MinColliderThickness),
                        Mathf.Max(Mathf.Abs(bounds.size.z), MinColliderThickness));
                    break;
                }
            }
        }

        private static int LargestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static float AxisValue(Vector3 size, int axis)
        {
            return axis switch
            {
                0 => size.x,
                1 => size.y,
                _ => size.z,
            };
        }

        private static float SmallestValue(Vector3 size)
        {
            return Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        }

        private static float MiddleValue(Vector3 size)
        {
            var values = new List<float> { size.x, size.y, size.z };
            values.Sort();
            return values[1];
        }

        private static bool IsExcludedPrefabPath(string path)
        {
            foreach (var excludedPath in ExcludedPrefabPaths)
            {
                if (path.Equals(excludedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExcludedRoot(GameObject gameObject)
        {
            foreach (var excludedName in ExcludedRootNames)
            {
                if (gameObject.name.Equals(excludedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (gameObject.GetComponent<CharacterController>() != null && gameObject.name.Contains("Player", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (source == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(source);
            return IsExcludedPrefabPath(path);
        }

        private static string GetAutoRunKey()
        {
            return $"{AutoRunKeyPrefix}.{Application.dataPath}";
        }

        private static void WriteSummaryLog(string summary, bool triggeredAutomatically)
        {
            var header = triggeredAutomatically ? "Auto-run" : "Manual run";
            var content =
                $"{header} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"{summary}\n";

            var logPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "PrimitiveColliderBatchProcessor.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, content);
        }

        private enum ColliderShape
        {
            Box,
            Capsule,
            Sphere,
        }

        private sealed class ProcessingStats
        {
            public int ScannedPrefabs;
            public int ModifiedPrefabs;
            public int SkippedPrefabs;
            public int SkippedModelPrefabs;
            public int ScannedScenes;
            public int ModifiedScenes;
            public int CollidersAdded;
            public int BoxCollidersAdded;
            public int CapsuleCollidersAdded;
            public int SphereCollidersAdded;

            public string ToSummary()
            {
                return
                    "Primitive collider batch complete.\n" +
                    $"Prefabs scanned: {ScannedPrefabs}\n" +
                    $"Prefabs modified: {ModifiedPrefabs}\n" +
                    $"Prefabs skipped: {SkippedPrefabs}\n" +
                    $"Model prefabs skipped: {SkippedModelPrefabs}\n" +
                    $"Scenes scanned: {ScannedScenes}\n" +
                    $"Scenes modified: {ModifiedScenes}\n" +
                    $"Colliders added: {CollidersAdded}\n" +
                    $"Box colliders: {BoxCollidersAdded}\n" +
                    $"Capsule colliders: {CapsuleCollidersAdded}\n" +
                    $"Sphere colliders: {SphereCollidersAdded}";
            }
        }
    }
}
