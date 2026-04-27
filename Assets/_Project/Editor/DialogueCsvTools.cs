#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LastPatrol.Data;

namespace LastPatrol.EditorTools
{
    /// <summary>
    /// DialogueLineSO 텍스트(textKR / textEN) 라운드트립 CSV 도구.
    ///
    /// 정책:
    ///   - 메타데이터(speakerId, theme, displaySecondsOverride)는 SO 인스펙터에서 관리. CSV는 텍스트만.
    ///   - ID는 asset GUID. 작가/번역자가 절대 만지지 말 것.
    ///   - Import는 텍스트 두 필드(Korean, English)와 Notes만 적용. 그 외는 무시.
    ///   - 새 라인 추가는 CSV로 안 함 — Unity에서 .asset 만든 후 export → 편집 → import.
    ///
    /// 사용:
    ///   Tools → LastPatrol → Localization → Export Dialogues to CSV
    ///   Tools → LastPatrol → Localization → Import Dialogues from CSV
    /// </summary>
    public static class DialogueCsvTools
    {
        private const string MenuExport = "Tools/LastPatrol/Localization/Export Dialogues to CSV";
        private const string MenuImport = "Tools/LastPatrol/Localization/Import Dialogues from CSV";

        // RFC 4180 기반 단순 구현 + UTF-8 BOM (엑셀 한국어 호환)
        private static readonly string[] Headers = { "ID", "AssetPath", "Speaker", "Theme", "Korean", "English", "Notes" };

        [MenuItem(MenuExport)]
        public static void ExportDialogues()
        {
            string defaultName = "lastpatrol_dialogues.csv";
            string path = EditorUtility.SaveFilePanel("Export Dialogues to CSV",
                Application.dataPath, defaultName, "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sos = LoadAllDialogueLines();
            var sb = new StringBuilder();

            // BOM
            sb.Append('﻿');
            // Header
            for (int i = 0; i < Headers.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(Headers[i]));
            }
            sb.Append('\n');

            int count = 0;
            foreach (var so in sos)
            {
                if (so == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(so);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                string row = string.Join(",",
                    EscapeCsv(guid),
                    EscapeCsv(assetPath),
                    EscapeCsv(so.speakerId ?? ""),
                    EscapeCsv(so.theme.ToString()),
                    EscapeCsv(so.textKR ?? ""),
                    EscapeCsv(so.textEN ?? ""),
                    EscapeCsv("")); // Notes (빈 칸으로 시작)
                sb.Append(row);
                sb.Append('\n');
                count++;
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            Debug.Log($"[DialogueCsv] exported {count} lines to {path}");
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuImport)]
        public static void ImportDialogues()
        {
            string path = EditorUtility.OpenFilePanel("Import Dialogues from CSV",
                Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path)) return;

            string text = File.ReadAllText(path, Encoding.UTF8);
            var rows = ParseCsv(text);
            if (rows.Count < 2)
            {
                Debug.LogError("[DialogueCsv] CSV가 비어있거나 헤더만 있음.");
                return;
            }

            // 헤더 매핑
            var header = rows[0];
            int idxId = header.IndexOf("ID");
            int idxKR = header.IndexOf("Korean");
            int idxEN = header.IndexOf("English");
            if (idxId < 0 || (idxKR < 0 && idxEN < 0))
            {
                Debug.LogError("[DialogueCsv] 필수 컬럼(ID + Korean/English) 누락.");
                return;
            }

            int updated = 0, missing = 0;
            int unchanged = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row.Count == 0 || string.IsNullOrEmpty(row[idxId])) continue;
                    string guid = row[idxId];
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) { missing++; continue; }
                    var so = AssetDatabase.LoadAssetAtPath<DialogueLineSO>(assetPath);
                    if (so == null) { missing++; continue; }

                    bool changed = false;
                    if (idxKR >= 0 && idxKR < row.Count)
                    {
                        string newKr = row[idxKR];
                        if ((so.textKR ?? "") != (newKr ?? "")) { so.textKR = newKr; changed = true; }
                    }
                    if (idxEN >= 0 && idxEN < row.Count)
                    {
                        string newEn = row[idxEN];
                        if ((so.textEN ?? "") != (newEn ?? "")) { so.textEN = newEn; changed = true; }
                    }
                    if (changed)
                    {
                        EditorUtility.SetDirty(so);
                        updated++;
                    }
                    else unchanged++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[DialogueCsv] import: {updated} updated, {unchanged} unchanged, {missing} missing (id 매칭 실패).");
        }

        // ---- helpers ----

        private static List<DialogueLineSO> LoadAllDialogueLines()
        {
            var list = new List<DialogueLineSO>();
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(DialogueLineSO));
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var so = AssetDatabase.LoadAssetAtPath<DialogueLineSO>(path);
                if (so != null) list.Add(so);
            }
            list.Sort((a, b) =>
                string.Compare(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b),
                    System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return "";
            bool needsQuote = s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
            if (!needsQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        // 단순 RFC 4180 파서 — 따옴표·개행·콤마 처리.
        // 결과: List<List<string>> (행 → 셀)
        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            // BOM 제거
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

            var current = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;
            int i = 0;
            int n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    cell.Append(c);
                    i++;
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                        i++;
                    }
                    else if (c == ',')
                    {
                        current.Add(cell.ToString());
                        cell.Length = 0;
                        i++;
                    }
                    else if (c == '\r')
                    {
                        i++; // 다음 \n에서 행 종료
                    }
                    else if (c == '\n')
                    {
                        current.Add(cell.ToString());
                        cell.Length = 0;
                        rows.Add(current);
                        current = new List<string>();
                        i++;
                    }
                    else
                    {
                        cell.Append(c);
                        i++;
                    }
                }
            }
            // 마지막 셀/행
            if (cell.Length > 0 || current.Count > 0)
            {
                current.Add(cell.ToString());
                rows.Add(current);
            }
            return rows;
        }
    }
}
#endif
