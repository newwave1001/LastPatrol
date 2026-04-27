using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LastPatrol.Core.Input;
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

        [Header("Skip Input")]
        [Tooltip("대사 진행 중 마우스 좌클릭으로 스킵. " +
                 "첫 클릭 = 타이프라이터 즉시 완성, 두 번째 = 다음 라인.")]
        [SerializeField] private bool clickToSkip = true;
        [Tooltip("키보드 Space/Enter 로도 스킵 가능.")]
        [SerializeField] private bool keyboardSkip = true;

        [Header("Input Lock")]
        [Tooltip("대사 표시 중 캐릭터 이동/조작 입력 차단. 비워두면 자동 검색.")]
        [SerializeField] private InputReader inputToLock;
        [Tooltip("대사 중 입력 차단 활성화")]
        [SerializeField] private bool lockInputDuringDialogue = true;

        private readonly Queue<DialogueLineSO> queue = new Queue<DialogueLineSO>();
        private Coroutine running;
        private bool skipRequested;
        private bool _inputLocked; // PushLock/PopLock 균형 추적

        public bool IsActive => running != null;

        /// <summary>큐가 비어 마지막 라인 hold까지 끝난 직후 발화. 페이드/씬 전환 트리거에 사용.</summary>
        public event Action OnQueueEnded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (panel != null) panel.Hide();
            if (inputToLock == null) inputToLock = FindAnyObjectByType<InputReader>();
        }

        void OnDisable()
        {
            // 씬 전환·삭제 시 lock 잔재 방지.
            ReleaseInputLock();
        }

        private void AcquireInputLock()
        {
            if (!lockInputDuringDialogue || _inputLocked || inputToLock == null) return;
            inputToLock.PushLock();
            _inputLocked = true;
        }

        private void ReleaseInputLock()
        {
            if (!_inputLocked || inputToLock == null) return;
            inputToLock.PopLock();
            _inputLocked = false;
        }

        public void Show(DialogueLineSO line)
        {
            if (line == null) return;
            queue.Enqueue(line);
            if (running == null)
            {
                AcquireInputLock();
                running = StartCoroutine(RunQueue());
            }
        }

        // 런타임 inline — SO 에셋 없이 한 줄 표시. 내면 보이스 + 선택 결과에 사용.
        public void ShowInline(string speakerId, ColorTheme theme, string textKR, string textEN = null, float displaySecondsOverride = 0f)
        {
            var line = ScriptableObject.CreateInstance<DialogueLineSO>();
            line.speakerId = speakerId;
            line.theme = theme;
            line.textKR = textKR;
            line.textEN = textEN;
            line.displaySecondsOverride = displaySecondsOverride;
            Show(line);
        }

        public void Show(DialogueSequenceSO seq)
        {
            if (seq == null) return;
            foreach (var l in seq.lines) if (l != null) queue.Enqueue(l);
            if (running == null)
            {
                AcquireInputLock();
                running = StartCoroutine(RunQueue());
            }
        }

        public void Clear()
        {
            queue.Clear();
            if (running != null) { StopCoroutine(running); running = null; }
            if (panel != null) panel.Hide();
            ReleaseInputLock();
        }

        public void Skip() { skipRequested = true; }

        void Update()
        {
            if (running == null) return; // 대사 중이 아니면 무시

            if (clickToSkip)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) Skip();
            }
            if (keyboardSkip)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                    Skip();
            }
        }

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
            ReleaseInputLock();
            OnQueueEnded?.Invoke();
        }

        private IEnumerator PlayLine(DialogueLineSO line)
        {
            if (panel == null) yield break;

            panel.Show();
            panel.SetAccent(ThemeColor(line.theme));
            panel.SetSpeaker(line.speakerId);

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
                case ColorTheme.Cyan:   return new Color(0.49f, 0.78f, 0.85f);   // #7CC8D8 — M-07
                case ColorTheme.Amber:  return new Color(0.85f, 0.54f, 0.29f);   // #D88A4A — Maren / cop voice
                case ColorTheme.Blood:  return new Color(0.66f, 0.19f, 0.16f);   // #A8302A — grief / danger
                case ColorTheme.Cream:  return new Color(1.00f, 0.808f, 0.431f); // #FFCE6E — echo (Noah)
                case ColorTheme.Hunch:  return new Color(0.561f, 0.647f, 0.478f); // #8FA57A — hunch (intuition)
                default:                return new Color(0.541f, 0.498f, 0.459f); // #8A7F75 — subtle / cynicism
            }
        }
    }
}
