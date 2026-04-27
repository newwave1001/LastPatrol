using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastPatrol.Core;
using LastPatrol.Systems.Vehicle;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// 외부 운전 씬 우상단 HUD — TMP Canvas 버전 (이전 OnGUI 대체).
    /// 자동 빌드: 인스펙터 슬롯 비어있으면 Awake에서 Canvas + 자식 위젯 자동 생성.
    /// 폰트만 인스펙터에서 본고딕 SDF 같은 한국어 폰트 드래그하면 끝.
    ///
    /// 외부 API (이전과 동일):
    ///   SetDispatch(text, tone) / SetPrompt(text, tone) / ClearPrompt()
    /// </summary>
    [DisallowMultipleComponent]
    public class DriveHUD : MonoBehaviour
    {
        public enum DispatchTone { Ink, Cyan, Amber, Blood }

        [Header("References")]
        [SerializeField] private CarController car;
        [SerializeField] private GameClock clock;
        [SerializeField] private VehicleHealth carHealth;

        [Header("UI Slots (비워두면 Awake에서 자동 생성)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text kmhLabel;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text dispatchText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Image hpBack;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text huntedLabel;
        [SerializeField] private Image huntedDot;

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildIfMissing = true;
        [Tooltip("한국어 폰트 SDF (예: 본고딕 Bold). null이면 TMP default 폰트 사용.")]
        [SerializeField] private TMP_FontAsset preferredFont;
        [SerializeField] private Vector2 panelSize = new Vector2(300f, 156f);
        [SerializeField] private Vector2 panelMargin = new Vector2(12f, 12f);

        // CLAUDE.md 팔레트
        static readonly Color Paper = new Color(0.961f, 0.933f, 0.878f, 0.95f);
        static readonly Color Ink   = new Color(0.227f, 0.180f, 0.157f);
        static readonly Color Cyan  = new Color(0.486f, 0.784f, 0.847f);
        static readonly Color Amber = new Color(0.847f, 0.541f, 0.290f);
        static readonly Color Blood = new Color(0.659f, 0.188f, 0.165f);

        private string _dispatch = "STANDBY";
        private DispatchTone _dispatchTone = DispatchTone.Ink;
        private string _prompt = "";
        private DispatchTone _promptTone = DispatchTone.Amber;

        public void SetDispatch(string text, DispatchTone tone = DispatchTone.Ink)
        {
            _dispatch = string.IsNullOrEmpty(text) ? "STANDBY" : text;
            _dispatchTone = tone;
        }

        public void SetPrompt(string text, DispatchTone tone = DispatchTone.Amber)
        {
            _prompt = text ?? "";
            _promptTone = tone;
        }

        public void ClearPrompt() => _prompt = "";

        void Awake()
        {
            if (car == null) car = FindAnyObjectByType<CarController>();
            if (clock == null) clock = FindAnyObjectByType<GameClock>();
            if (carHealth == null && car != null) carHealth = car.GetComponent<VehicleHealth>();
            if (carHealth == null) carHealth = FindAnyObjectByType<VehicleHealth>();

            if (autoBuildIfMissing && canvas == null) AutoBuild();
        }

        void Update()
        {
            // 속도
            if (speedText != null && car != null)
            {
                int kmh = Mathf.RoundToInt(Mathf.Abs(car.CurrentSpeed) * 3.6f);
                speedText.text = kmh.ToString();
            }
            // 시계
            if (clockText != null && clock != null)
                clockText.text = clock.Display;

            // dispatch
            if (dispatchText != null)
            {
                dispatchText.text = _dispatch;
                dispatchText.color = ToneToColor(_dispatchTone);
            }
            // prompt — 비어있으면 숨김
            if (promptText != null)
            {
                bool show = !string.IsNullOrEmpty(_prompt);
                promptText.gameObject.SetActive(show);
                if (show)
                {
                    promptText.text = _prompt;
                    promptText.color = ToneToColor(_promptTone);
                }
            }
            // HP 바
            if (hpFill != null && carHealth != null)
                hpFill.fillAmount = carHealth.Normalized;
            if (hpLabel != null && carHealth != null)
                hpLabel.text = $"HP {Mathf.RoundToInt(carHealth.CurrentHealth)}/{Mathf.RoundToInt(carHealth.MaxHealth)}";

            // HUNTED 인디케이터
            bool hunted = PlayerStatus.IsHunted;
            if (huntedLabel != null) huntedLabel.gameObject.SetActive(hunted);
            if (huntedDot != null)   huntedDot.gameObject.SetActive(hunted);
        }

        private Color ToneToColor(DispatchTone t)
        {
            switch (t)
            {
                case DispatchTone.Cyan:  return Cyan;
                case DispatchTone.Amber: return Amber;
                case DispatchTone.Blood: return Blood;
                default: return Ink;
            }
        }

        // -------- Auto-build --------

        private void AutoBuild()
        {
            // Canvas
            var canvasGo = new GameObject("DriveHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel (페이퍼 박스, 우상단)
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = Paper;
            panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-panelMargin.x, -panelMargin.y);
            panel.sizeDelta = panelSize;

            // Speed (큰 숫자)
            speedText = MakeText("Speed", panel, new Vector2(0f, -6f), new Vector2(panelSize.x, 52f),
                                 44, Ink, FontStyles.Bold, TextAlignmentOptions.Center, "0");
            // KM/H
            kmhLabel  = MakeText("KMHLabel", panel, new Vector2(0f, -56f), new Vector2(panelSize.x, 16f),
                                 12, Ink, FontStyles.Normal, TextAlignmentOptions.Center, "KM/H");
            // Clock
            clockText = MakeText("Clock", panel, new Vector2(0f, -78f), new Vector2(panelSize.x, 16f),
                                 13, Ink, FontStyles.Normal, TextAlignmentOptions.Center, "07:14:00");
            // Dispatch
            dispatchText = MakeText("Dispatch", panel, new Vector2(0f, -98f), new Vector2(panelSize.x - 12f, 18f),
                                    13, Ink, FontStyles.Bold, TextAlignmentOptions.Center, "STANDBY");
            // Prompt (작게, 아래)
            promptText = MakeText("Prompt", panel, new Vector2(0f, -118f), new Vector2(panelSize.x - 12f, 14f),
                                  11, Amber, FontStyles.Bold, TextAlignmentOptions.Center, "");
            promptText.gameObject.SetActive(false);

            // HP 바 배경 + 채움
            hpBack = MakeImage("HPBack", panel,
                anchoredPos: new Vector2(0f, -140f),
                size: new Vector2(panelSize.x - 32f, 6f),
                color: new Color(Ink.r, Ink.g, Ink.b, 0.18f),
                anchorY: 1f, pivotY: 1f, centerX: true);

            hpFill = MakeImage("HPFill", hpBack.rectTransform,
                stretchToParent: true,
                color: Blood);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFill.fillAmount = 1f;

            // HP 라벨 (HP 100/100)
            hpLabel = MakeText("HPLabel", panel, new Vector2(0f, -126f), new Vector2(panelSize.x - 12f, 12f),
                               10, Ink, FontStyles.Normal, TextAlignmentOptions.Center, "HP 100/100");

            // HUNTED 인디케이터 (우측 상단 코너)
            huntedDot = MakeImage("HuntedDot", panel,
                anchoredPos: new Vector2(-12f, -12f),
                size: new Vector2(10f, 10f),
                color: Blood,
                anchorX: 1f, pivotX: 1f, anchorY: 1f, pivotY: 1f);
            huntedDot.gameObject.SetActive(false);

            huntedLabel = MakeText("HuntedLabel", panel, new Vector2(-26f, -10f), new Vector2(70f, 14f),
                                   11, Blood, FontStyles.Bold, TextAlignmentOptions.Right, "HUNTED");
            var hlRT = huntedLabel.rectTransform;
            hlRT.anchorMin = hlRT.anchorMax = new Vector2(1f, 1f);
            hlRT.pivot = new Vector2(1f, 1f);
            hlRT.anchoredPosition = new Vector2(-26f, -10f);
            huntedLabel.gameObject.SetActive(false);
        }

        private TMP_Text MakeText(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                                  int fontSize, Color color, FontStyles style, TextAlignmentOptions align, string defaultText)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = defaultText;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            if (preferredFont != null) t.font = preferredFont;
            return t;
        }

        private Image MakeImage(string name, Transform parent, Vector2 anchoredPos = default, Vector2 size = default,
                                Color color = default, bool stretchToParent = false,
                                float anchorX = 0.5f, float pivotX = 0.5f,
                                float anchorY = 1f, float pivotY = 1f,
                                bool centerX = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color == default ? Color.white : color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            if (stretchToParent)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                if (centerX) { anchorX = 0.5f; pivotX = 0.5f; }
                rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
                rt.pivot = new Vector2(pivotX, pivotY);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = size;
            }
            return img;
        }
    }
}
