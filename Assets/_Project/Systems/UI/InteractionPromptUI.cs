using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastPatrol.Characters;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// 인터랙션 가능 대상 근처에서 "[F] 조사 · {라벨}" 화면 하단 표시.
    /// InteractionSystem.CurrentTarget을 매 프레임 읽어 텍스트/표시 갱신.
    ///
    /// 인스펙터 슬롯 비워두면 Awake에서 Canvas + 자식 자동 생성. 폰트만 인스펙터에서 설정.
    /// 마렌이 Cower 모드(M-07 활성)면 prompt 자동 숨김.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InteractionSystem source;
        [SerializeField] private MarenController maren;

        [Header("UI Slots (비워두면 Awake에서 자동 생성)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text label;

        [Header("Display")]
        [SerializeField] private string keyHint = "E";
        [SerializeField] private string actionVerb = "조사";
        [SerializeField] private float fadeSpeed = 10f;

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildIfMissing = true;
        [SerializeField] private TMP_FontAsset preferredFont;
        [SerializeField] private Vector2 panelSize = new Vector2(280f, 36f);
        [Tooltip("화면 하단으로부터 떨어진 거리(px).")]
        [SerializeField] private float bottomOffset = 80f;

        // 페이퍼/잉크 톤
        static readonly Color Paper = new Color(0.961f, 0.933f, 0.878f, 0.92f);
        static readonly Color Ink   = new Color(0.227f, 0.180f, 0.157f);

        void Awake()
        {
            if (maren == null) maren = FindAnyObjectByType<MarenController>();
            if (source == null && maren != null) source = maren.GetComponent<InteractionSystem>();
            if (source == null) source = FindAnyObjectByType<InteractionSystem>();

            if (autoBuildIfMissing && canvas == null) AutoBuild();

            // 시작 시 무조건 숨김 — Canvas 자체를 비활성으로
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (label != null) label.text = "";
        }

        void Update()
        {
            if (canvas == null) return;

            // M-07 활성(마렌 Cower) 중엔 prompt 숨김
            bool blockedByMode = (maren != null && maren.CurrentMode != MarenController.ControlMode.Manual);
            var target = blockedByMode ? null : (source != null ? source.CurrentTarget : null);
            bool show = target != null;

            // Canvas GameObject 자체를 토글 — 가장 강력한 표시/숨김
            if (canvas.gameObject.activeSelf != show) canvas.gameObject.SetActive(show);

            if (label != null && show)
                label.text = $"[<color=#D88A4A>{keyHint}</color>] {actionVerb}  ·  {target.PromptLabel}";

            // CanvasGroup 페이드는 보조 — Canvas active일 때만 의미.
            if (canvasGroup != null)
            {
                float wantAlpha = show ? 1f : 0f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, wantAlpha, fadeSpeed * Time.deltaTime);
            }
        }

        // -------- Auto-build --------

        private void AutoBuild()
        {
            var canvasGo = new GameObject("InteractionPrompt_Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var img = panelGo.AddComponent<Image>();
            img.color = Paper;
            img.raycastTarget = false;
            panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, bottomOffset);
            panel.sizeDelta = panelSize;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(panel, false);
            var rt = labelGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 4f);
            rt.offsetMax = new Vector2(-12f, -4f);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = ""; // 첫 프레임 default 보임 방지
            label.fontSize = 16;
            label.color = Ink;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.richText = true;
            label.raycastTarget = false;
            if (preferredFont != null) label.font = preferredFont;
        }
    }
}
