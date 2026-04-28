#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LastPatrol.EditorTools
{
    /// <summary>
    /// Missing Script 컴포넌트 자동 제거.
    /// Tools → LastPatrol → Remove Missing Scripts (선택 GO + 자식 모두)
    /// 또는 Tools → LastPatrol → Remove Missing Scripts in Project (전체 prefab 스캔)
    /// </summary>
    public static class MissingScriptCleaner
    {
        [MenuItem("Tools/LastPatrol/Remove Missing Scripts (Selection + Children)")]
        public static void RemoveInSelection()
        {
            int total = 0;
            foreach (var go in Selection.gameObjects)
            {
                total += CleanRecursive(go);
            }
            Debug.Log($"[MissingScriptCleaner] Selection 에서 {total}개 missing script 제거.");
        }

        [MenuItem("Tools/LastPatrol/Remove Missing Scripts in All Prefabs")]
        public static void RemoveInAllPrefabs()
        {
            int total = 0;
            int prefabsTouched = 0;
            var guids = AssetDatabase.FindAssets("t:GameObject");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                int removed = CleanRecursive(go);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(go);
                    PrefabUtility.SavePrefabAsset(go);
                    prefabsTouched++;
                    total += removed;
                    Debug.Log($"  [{path}]: {removed}개 제거");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MissingScriptCleaner] 전체 prefab 스캔: {total}개 제거 (prefab {prefabsTouched}개 영향).");
            EditorUtility.DisplayDialog("Missing Script 정리", $"{total}개 missing script 제거됨\n({prefabsTouched}개 prefab 변경)", "OK");
        }

        [MenuItem("Tools/LastPatrol/Remove Missing Scripts in Open Scene")]
        public static void RemoveInOpenScene()
        {
            int total = 0;
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                total += CleanRecursive(root);
            }
            if (total > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[MissingScriptCleaner] 현재 씬 '{scene.name}' 에서 {total}개 missing script 제거.");
            EditorUtility.DisplayDialog("Missing Script 정리", $"씬 '{scene.name}'\n{total}개 missing script 제거됨", "OK");
        }

        [MenuItem("Tools/LastPatrol/Remove Missing Scripts EVERYWHERE (Prefabs + All Scenes)")]
        public static void RemoveEverywhere()
        {
            int prefabsTotal = 0, prefabsTouched = 0;
            var prefabGuids = AssetDatabase.FindAssets("t:GameObject");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                int removed = CleanRecursive(go);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(go);
                    PrefabUtility.SavePrefabAsset(go);
                    prefabsTouched++;
                    prefabsTotal += removed;
                    Debug.Log($"  [Prefab {path}]: {removed}개 제거");
                }
            }

            int scenesTotal = 0, scenesTouched = 0;
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/")) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int removed = 0;
                foreach (var root in scene.GetRootGameObjects())
                    removed += CleanRecursive(root);
                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    scenesTouched++;
                    scenesTotal += removed;
                    Debug.Log($"  [Scene {path}]: {removed}개 제거");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MissingScriptCleaner] 전체: prefab {prefabsTotal}개 ({prefabsTouched} prefab) + scene {scenesTotal}개 ({scenesTouched} scene).");
            EditorUtility.DisplayDialog("Missing Script 정리",
                $"Prefab: {prefabsTotal}개 제거 ({prefabsTouched}개 prefab)\nScene: {scenesTotal}개 제거 ({scenesTouched}개 scene)",
                "OK");
        }

        private static int CleanRecursive(GameObject go)
        {
            if (go == null) return 0;
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                count += CleanRecursive(go.transform.GetChild(i).gameObject);
            }
            return count;
        }
    }
}
#endif
