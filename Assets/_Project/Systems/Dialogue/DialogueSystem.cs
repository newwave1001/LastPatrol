using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Data;

namespace LastPatrol.Systems.Dialogue
{
    // 전역 대사 시스템. 라인 큐 + 타이프라이터 + 색상 테마.
    // 한국어/영어 자동 선택 (TextKR 비어있으면 EN fallback).
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private DialoguePanelUI panel;

        [Header("Timing")]
        [SerializeField] private float defaultDisplaySeconds = 3f;
        [SerializeField] private float charsPerSecond = 35f;
        [SerializeField] private float interLineGap = 0.25f;

        [Header("Locale")]
        [SerializeField] private bool preferKorean = true;

        private readonly Queue<DialogueLineSO> queue = new Queue<DialogueLineSO>();
        private Coroutine running;
        private bool skipRequested;

        public bool IsActive => running != null;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (panel != null) panel.Hide();
        }

        public void Show(DialogueLineSO line)
        {
            if (line == null) return;
            queue.Enqueue(line);
            if (running == null) running = StartCoroutine(RunQueue());
        }

        public void Show(DialogueSequenceSO seq)
        {
            if (seq == null) return;
            foreach (var l in seq.lines) if (l != null) queue.Enqueue(l);
            if (running == null) running = StartCoroutine(RunQueue());
        }

        public void Clear()
        {
            queue.Clear();
            if (running != null) { StopCoroutine(running); running = null; }
            if (panel != null) panel.Hide();
        }

        public void Skip() { skipRequested = true; }

        private IEnumerator RunQueue()
        {
            while (queue.Count > 0)
            {
                var line = queue.Dequeue();
                yield return PlayLine(line);
                if (queue.Count > 0) yield return new WaitForSeconds(interLineGap);
            }
            running = null;
            if (panel != null) panel.Hide();
        }

        private IEnumerator PlayLine(DialogueLineSO line)
        {
            if (panel == null) yield break;

            panel.Show();
            panel.SetSpeaker(line.speakerId, ThemeColor(line.theme));

            string text = preferKorean
                ? (string.IsNullOrEmpty(line.textKR) ? line.textEN : line.textKR)
                : (string.IsNullOrEmpty(line.textEN) ? line.textKR : line.textEN);
            text = text ?? "";

            // 타이프라이터.
            int len = text.Length;
            float perChar = 1f / Mathf.Max(1f, charsPerSecond);
            int shown = 0;
            skipRequested = false;
            while (shown < len)
            {
                if (skipRequested) { shown = len; break; }
                shown++;
                panel.SetBody(text.Substring(0, shown));
                yield return new WaitForSeconds(perChar);
            }
            panel.SetBody(text);

            // 표시 유지.
            float hold = line.displaySecondsOverride > 0f ? line.displaySecondsOverride : defaultDisplaySeconds;
            float elapsed = 0f;
            skipRequested = false;
            while (elapsed < hold)
            {
                if (skipRequested) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        public static Color ThemeColor(ColorTheme t)
        {
            switch (t)
            {
                case ColorTheme.Cyan:  return new Color(0.49f, 0.78f, 0.85f);
                case ColorTheme.Amber: return new Color(0.85f, 0.54f, 0.29f);
                case ColorTheme.Blood: return new Color(0.66f, 0.19f, 0.16f);
                default:               return new Color(0.96f, 0.93f, 0.88f); // Subtle / paper
            }
        }
    }
}
