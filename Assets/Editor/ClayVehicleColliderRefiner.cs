using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NullProtocol.Editor
{
    public static class ClayVehicleColliderRefiner
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AutoRunKeyPrefix = "NullProtocol.ClayVehicleColliderRefiner.AutoRun";
        private const float MinSize = 0.001f;

        [InitializeOnLoadMethod]
        private static void AutoRunOnceInOpenEditor()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(GetAutoRunKey(), false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoRunInEditor;
        }

        [MenuItem("Tools/Null Protocol/Refine Clay Vehicle Colliders")]
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
            var wasLoaded = SceneManager.GetSceneByPath(ScenePath).isLoaded;
            var scene = wasLoaded
                ? SceneManager.GetSceneByPath(ScenePath)
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var stats = new ProcessingStats();

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                foreach (var transform in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsTargetVehicle(transform.gameObject))
                    {
                        continue;
                    }

                    if (!TryGetHierarchyBounds(transform, out var bounds))
                    {
                        stats.SkippedVehicles++;
                        continue;
                    }

                    ApplyTightBoxCollider(transform.gameObject, bounds);
                    stats.VehiclesUpdated++;

                    if (IsCar(transform.gameObject.name))
                    {
                        stats.CarsUpdated++;
                    }
                    else
                    {
                        stats.VansUpdated++;
                    }
                }
            }

            EditorSceneManager.SaveScene(scene);

            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            var summary = stats.ToSummary();
            WriteSummaryLog(summary, triggeredAutomatically);
            Debug.Log(summary);
        }

        private static bool IsTargetVehicle(GameObject gameObject)
        {
            return IsCar(gameObject.name) || IsVan(gameObject.name);
        }

        private static bool IsCar(string name)
        {
            return name.StartsWith("Private Car A.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVan(string name)
        {
            return name.StartsWith("Van A.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetHierarchyBounds(Transform root, out Bounds bounds)
        {
            var initialized = false;
            bounds = default;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!TryGetRendererLocalBounds(renderer, root, out var rendererBounds))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = rendererBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds.min);
                    bounds.Encapsulate(rendererBounds.max);
                }
            }

            return initialized;
        }

        private static bool TryGetRendererLocalBounds(Renderer renderer, Transform root, out Bounds bounds)
        {
            Bounds rendererLocalBounds;

            if (renderer.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                rendererLocalBounds = meshFilter.sharedMesh.bounds;
            }
            else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            {
                rendererLocalBounds = skinnedMeshRenderer.localBounds;
            }
            else
            {
                bounds = default;
                return false;
            }

            var toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var corners = GetBoundsCorners(rendererLocalBounds);
            var min = toRoot.MultiplyPoint3x4(corners[0]);
            var max = min;

            for (var i = 1; i < corners.Length; i++)
            {
                var point = toRoot.MultiplyPoint3x4(corners[i]);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;

            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };
        }

        private static void ApplyTightBoxCollider(GameObject vehicle, Bounds bounds)
        {
            foreach (var collider in vehicle.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var min = bounds.min;
            var max = bounds.max;

            var upAxis = GetUpAxis(vehicle.transform, out var upSign);
            var forwardAxis = GetLongestHorizontalAxis(bounds.size, upAxis);
            var sideAxis = 3 - upAxis - forwardAxis;

            var shrink = IsCar(vehicle.name)
                ? new Vector3(0.88f, 0.78f, 0.92f)
                : new Vector3(0.9f, 0.84f, 0.92f);

            var size = bounds.size;
            var adjustedSize = size;
            SetAxisValue(ref adjustedSize, sideAxis, Mathf.Max(GetAxisValue(size, sideAxis) * shrink.x, MinSize));
            SetAxisValue(ref adjustedSize, upAxis, Mathf.Max(GetAxisValue(size, upAxis) * shrink.y, MinSize));
            SetAxisValue(ref adjustedSize, forwardAxis, Mathf.Max(GetAxisValue(size, forwardAxis) * shrink.z, MinSize));

            var adjustedCenter = bounds.center;
            var newUpSize = GetAxisValue(adjustedSize, upAxis);
            var originalMinUp = GetAxisValue(min, upAxis);
            var originalMaxUp = GetAxisValue(max, upAxis);

            if (upSign >= 0f)
            {
                SetAxisValue(ref adjustedCenter, upAxis, originalMinUp + (newUpSize * 0.5f));
            }
            else
            {
                SetAxisValue(ref adjustedCenter, upAxis, originalMaxUp - (newUpSize * 0.5f));
            }

            var boxCollider = vehicle.AddComponent<BoxCollider>();
            boxCollider.center = adjustedCenter;
            boxCollider.size = adjustedSize;
        }

        private static int GetUpAxis(Transform transform, out float sign)
        {
            var directions = new[]
            {
                transform.right,
                transform.up,
                transform.forward,
            };

            var bestAxis = 0;
            var bestDot = Vector3.Dot(directions[0], Vector3.up);

            for (var i = 1; i < directions.Length; i++)
            {
                var dot = Vector3.Dot(directions[i], Vector3.up);
                if (Mathf.Abs(dot) > Mathf.Abs(bestDot))
                {
                    bestAxis = i;
                    bestDot = dot;
                }
            }

            sign = Mathf.Sign(bestDot);
            return bestAxis;
        }

        private static int GetLongestHorizontalAxis(Vector3 size, int upAxis)
        {
            var candidates = new List<int> { 0, 1, 2 };
            candidates.Remove(upAxis);
            return GetAxisValue(size, candidates[0]) >= GetAxisValue(size, candidates[1]) ? candidates[0] : candidates[1];
        }

        private static float GetAxisValue(Vector3 value, int axis)
        {
            return axis switch
            {
                0 => value.x,
                1 => value.y,
                _ => value.z,
            };
        }

        private static void SetAxisValue(ref Vector3 value, int axis, float axisValue)
        {
            switch (axis)
            {
                case 0:
                    value.x = axisValue;
                    break;
                case 1:
                    value.y = axisValue;
                    break;
                default:
                    value.z = axisValue;
                    break;
            }
        }

        private static string GetAutoRunKey()
        {
            return $"{AutoRunKeyPrefix}.{Application.dataPath}";
        }

        private static void WriteSummaryLog(string summary, bool triggeredAutomatically)
        {
            var header = triggeredAutomatically ? "Auto-run" : "Manual run";
            var content = $"{header} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{summary}\n";
            var logPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "ClayVehicleColliderRefiner.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, content);
        }

        private sealed class ProcessingStats
        {
            public int VehiclesUpdated;
            public int CarsUpdated;
            public int VansUpdated;
            public int SkippedVehicles;

            public string ToSummary()
            {
                return
                    "Clay vehicle collider refinement complete.\n" +
                    $"Vehicles updated: {VehiclesUpdated}\n" +
                    $"Cars updated: {CarsUpdated}\n" +
                    $"Vans updated: {VansUpdated}\n" +
                    $"Vehicles skipped: {SkippedVehicles}";
            }
        }
    }
}
