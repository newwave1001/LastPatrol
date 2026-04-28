#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace LastPatrol.EditorTools
{
    /// <summary>
    /// 모든 prefab/scene의 TMP_Text font를 LiberationSans → Noto 로 일괄 교체.
    /// Tools → LastPatrol → Replace LiberationSans → Noto.
    ///
    /// 이후 LiberationSans SDF 에셋 파일 삭제 가능.
    /// TMP Default Font Asset도 Project Settings → TextMesh Pro → Settings 에서 Noto로 변경 권장.
    /// </summary>
    public static class FontReplaceTool
    {
        [MenuItem("Tools/LastPatrol/Replace LiberationSans → Noto")]
        public static void ReplaceLiberationToNoto()
        {
            TMP_FontAsset noto = FindFontByName("Noto");
            TMP_FontAsset lib = FindFontByName("LiberationSans");

            if (noto == null)
            {
                EditorUtility.DisplayDialog("Font Replace", "Noto SDF font not found. Project에 Noto SDF asset이 있어야 함.", "OK");
                return;
            }
            if (lib == null)
            {
                Debug.LogWarning("[FontReplaceTool] LiberationSans SDF asset 없음 — 이미 제거됐거나 다른 이름. " +
                                 "TMP Settings 의 Default Font Asset 만 Noto로 바꾸면 충분할 수도.");
            }

            int prefabCount = 0;
            int sceneCount = 0;

            // 1) Prefabs
            var prefabGuids = AssetDatabase.FindAssets("t:GameObject");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                bool changed = false;
                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    if (lib != null && t.font == lib) { t.font = noto; changed = true; prefabCount++; }
                    else if (t.font == null) { t.font = noto; changed = true; prefabCount++; }
                }
                if (changed)
                {
                    EditorUtility.SetDirty(go);
                    PrefabUtility.SavePrefabAsset(go);
                }
            }

            // 2) Scenes
            string activeScenePath = EditorSceneManager.GetActiveScene().path;
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/")) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
                foreach (var t in texts)
                {
                    if (lib != null && t.font == lib) { t.font = noto; changed = true; sceneCount++; }
                    else if (t.font == null) { t.font = noto; changed = true; sceneCount++; }
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            // 활성 씬 복원
            if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();

            string msg = $"교체 완료\n  prefab TMP_Text: {prefabCount}\n  scene TMP_Text: {sceneCount}\n" +
                         $"\n다음 단계:\n" +
                         $"1) Project Settings → TextMesh Pro → Settings → Default Font Asset 을 Noto로 변경\n" +
                         $"2) Inspector에서 우리 UI 컴포넌트 (DriveHUD/M07StatusHUD/MarenStatusHUD/InteractionPromptUI) 의 Preferred Font 슬롯에 Noto 드래그\n" +
                         $"3) LiberationSans SDF asset 삭제";
            Debug.Log("[FontReplaceTool] " + msg);
            EditorUtility.DisplayDialog("Font Replace 완료", msg, "OK");
        }

        [MenuItem("Tools/LastPatrol/List Project Fonts")]
        public static void ListFonts()
        {
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            string list = $"=== TMP_FontAsset 목록 ({guids.Length}개) ===\n";
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                list += $"  {Path.GetFileNameWithoutExtension(path)}  ({path})\n";
            }
            Debug.Log(list);
        }

        private static TMP_FontAsset FindFontByName(string nameFragment)
        {
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
            return null;
        }
    }
}
#endif
