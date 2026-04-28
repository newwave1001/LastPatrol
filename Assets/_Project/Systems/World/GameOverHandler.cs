using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.Audio;
using LastPatrol.Systems.Battery;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 마렌 사망 시 게임 오버 트리거 + UI 표시.
    /// 흐름: OnDied → 콘솔 로그 → pauseDelay 대기 → UI 페이드 인 → Time.timeScale=0.
    /// 입력: R = 현재 씬 재시작, Mouse Click = 동일.
    ///
    /// 자동 빌드: autoBuildUI ✅ + canvas 슬롯 비어있음 → ScreenSpaceOverlay Canvas + 검정 오버레이
    /// + 'GAME OVER' (적색) + 한글 부제 + 힌트 텍스트 자동 생성. prefab 불필요.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameOverHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MarenController maren;

        [Header("Behavior")]
        [Tooltip("OnDied 후 UI 등장까지 대기(초). 사망 모션·소리 들을 시간.")]
        [SerializeField] private float pauseDelay = 1.2f;
        [Tooltip("UI 페이드 인 시간(초).")]
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private bool logEvents = true;

        [Header("Text")]
        [SerializeField] private string titleText = "GAME OVER";
        [SerializeField] private string subtitleText = "마렌이 쓰러졌다";
        [SerializeField] private string hintText = "[R] 다시 시작";

        [Header("Style")]
        [SerializeField] private Color overlayColor   = new Color(0f, 0f, 0f, 0.92f);
        [SerializeField] private Color titleColor     = new Color(0.66f, 0.18f, 0.16f);  // blood
        [SerializeField] private Color subtitleColor  = new Color(0.95f, 0.93f, 0.88f);  // paper
        [SerializeField] private Color hintColor      = new Color(0.65f, 0.62f, 0.58f);

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildUI = true;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image overlay;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private TMP_Text hintLabel;

        public bool IsGameOver { get; private set; }

        void Awake()
        {
            if (maren == null) maren = FindAnyObjectByType<MarenController>(FindObjectsInactive.Include);
            if (maren != null) maren.OnDied += HandleDeath;

            if (autoBuildUI && canvas == null) BuildUI();
            if (group != null) group.alpha = 0f;
            if (canvas != null) canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (maren != null) maren.OnDied -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            if (logEvents) Debug.Log("===== GAME OVER =====", this);
            AudioManager.PlaySfx(SfxKey.MarenDeath);
            StartCoroutine(GameOverSequence());
        }

        private IEnumerator GameOverSequence()
        {
            // 사망 모션 시간 — unscaled로 대기 (혹시 외부에서 timeScale 건드려도 안전)
            float t = 0f;
            while (t < pauseDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // UI 등장
            if (canvas != null) canvas.enabled = true;
            t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            if (group != null) group.alpha = 1f;

            // 시간 정지 (페이드 끝난 후 — 페이드 자체는 unscaled 진행했지만 게임 월드 정지)
            Time.timeScale = 0f;
            AudioManager.PlaySfx(SfxKey.GameOver);
            if (logEvents) Debug.Log("[GameOver] Time.timeScale = 0", this);
        }

        void Update()
        {
            if (!IsGameOver) return;
            if (group == null || group.alpha < 0.1f) return; // 페이드 진행 중이면 입력 무시

            // R 또는 마우스 클릭 → 재시작
            bool restart = false;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) restart = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) restart = true;
            if (restart) Restart();
        }

        private void Restart()
        {
            Time.timeScale = 1f;

            // 정적 상태 reset — domain reload 비활성 환경에서도 깨끗한 재시작 보장.
            BatteryInventory.Reset();
            M07State.Reset();
            PlayerStatus.Reset();
            ActiveCase.Clear();
            PlayerSpawnPoint.Clear();
            CombatStatus.ResetEngagement();

            if (logEvents) Debug.Log("[GameOver] restart current scene + statics reset", this);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ---- Auto build UI ----

        private void BuildUI()
        {
            // 부모 Canvas
            var canvasGo = new GameObject("GameOverCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 최상위

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            // 검정 오버레이
            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(canvasGo.transform, false);
            overlay = overlayGo.AddComponent<Image>();
            overlay.color = overlayColor;
            overlay.raycastTarget = false;
            var orect = overlay.rectTransform;
            orect.anchorMin = Vector2.zero;
            orect.anchorMax = Vector2.one;
            orect.offsetMin = Vector2.zero;
            orect.offsetMax = Vector2.zero;

            // 텍스트
            titleLabel    = CreateText(canvasGo.transform, "Title",    titleText,    180f, FontStyles.Bold,   titleColor,    new Vector2(0.5f, 0.62f));
            subtitleLabel = CreateText(canvasGo.transform, "Subtitle", subtitleText, 52f,  FontStyles.Normal, subtitleColor, new Vector2(0.5f, 0.48f));
            hintLabel     = CreateText(canvasGo.transform, "Hint",     hintText,     36f,  FontStyles.Normal, hintColor,     new Vector2(0.5f, 0.30f));

            canvas.enabled = false;
        }

        private TMP_Text CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color, Vector2 anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            var rt = tmp.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1600f, size * 1.6f);
            rt.anchoredPosition = Vector2.zero;
            return tmp;
        }
    }
}
