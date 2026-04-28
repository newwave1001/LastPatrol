using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// 화면 우측에 M-07 상태 패널 — 프로필 + 배터리 막대 + 추적 메시지.
    /// 실내 (S03)와 동일한 톤, 외부 운전 씬에 배치.
    ///
    /// 메시지:
    ///   배터리 > 0 + 살아 있음   → "위치 추적 방어 중"   (cyan, 안정)
    ///   배터리 = 0 또는 사망     → "추적 받는 중"       (blood, 경고)
    ///
    /// 자동 빌드: 슬롯 비어있으면 Awake에서 Canvas + 자식 위젯 생성.
    /// 폰트는 인스펙터에서 본고딕 SDF 같은 한국어 폰트 드래그.
    /// </summary>
    [DisallowMultipleComponent]
    public class M07StatusHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private M07Controller m07;
        [SerializeField] private TrackingDirector tracking;

        [Header("UI Slots (비워두면 Awake에서 자동 생성)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleText;     // "M-07"
        [SerializeField] private TMP_Text batteryNumber; // "65%"
        [SerializeField] private Image batteryBack;
        [SerializeField] private Image batteryFill;
        [SerializeField] private TMP_Text statusText;    // 위치 추적 방어 중 / 추적 받는 중
        [SerializeField] private Image profileFrame;     // 프로필 placeholder

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildIfMissing = true;
        [SerializeField] private TMP_FontAsset preferredFont;
        [SerializeField] private Vector2 panelSize = new Vector2(260f, 200f);
        [SerializeField] private Vector2 panelMargin = new Vector2(12f, 0f); // 우측 가운데 기준

        // CLAUDE.md 팔레트
        static readonly Color Paper = new Color(0.961f, 0.933f, 0.878f, 0.95f);
        static readonly Color Ink   = new Color(0.227f, 0.180f, 0.157f);
        static readonly Color Cyan  = new Color(0.486f, 0.784f, 0.847f);
        static readonly Color Amber = new Color(0.847f, 0.541f, 0.290f);
        static readonly Color Blood = new Color(0.659f, 0.188f, 0.165f);

        void Awake()
        {
            if (m07 == null) m07 = FindAnyObjectByType<M07Controller>(FindObjectsInactive.Include);
            if (tracking == null) tracking = FindAnyObjectByType<TrackingDirector>(FindObjectsInactive.Include);
            if (autoBuildIfMissing && canvas == null) AutoBuild();
        }

        void Update()
        {
            float pct = m07 != null ? m07.BatteryPercent : 0f;
            bool alive = m07 != null && m07.IsAlive;
            bool defending = alive && pct > 0.001f;

            if (batteryFill != null)
            {
                batteryFill.fillAmount = Mathf.Clamp01(pct);
                // 배터리 = 파랑 (M-07 식별 색)
                batteryFill.color = Cyan;
            }
            if (batteryNumber != null)
                batteryNumber.text = $"{Mathf.RoundToInt(pct * 100f)}%";

            if (statusText != null)
            {
                if (defending)
                {
                    statusText.text = "위치 추적 방어 중";
                    statusText.color = Cyan;
                }
                else
                {
                    statusText.text = "추적 받는 중";
                    statusText.color = Blood;
                }
            }

            if (profileFrame != null)
                profileFrame.color = defending ? Cyan : (alive ? Amber : Blood);
        }

        // ---- Auto Build ----

        private void AutoBuild()
        {
            // Canvas — 자체 새로 (DriveHUD와 충돌 없게 별도)
            var canvasGo = new GameObject("M07StatusHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel — 우측 중앙
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = Paper;
            panelImg.raycastTarget = false;
            panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-panelMargin.x, panelMargin.y);
            panel.sizeDelta = panelSize;

            // 프로필 placeholder (사각 프레임, 좌상단)
            profileFrame = MakeImage("ProfileFrame", panel,
                anchoredPos: new Vector2(12f, -12f),
                size: new Vector2(50f, 50f),
                color: Cyan,
                anchorX: 0f, pivotX: 0f, anchorY: 1f, pivotY: 1f);

            // Title "M-07" (프로필 우측)
            titleText = MakeText("Title", panel, new Vector2(76f, -22f), new Vector2(panelSize.x - 90f, 22f),
                                  18, Ink, FontStyles.Bold, TextAlignmentOptions.Left, "M-07");
            var trt = titleText.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(76f, -16f);

            // Battery Number "100%" (프로필 우측 아래)
            batteryNumber = MakeText("BatteryNumber", panel, new Vector2(76f, -46f), new Vector2(panelSize.x - 90f, 16f),
                                     14, Ink, FontStyles.Normal, TextAlignmentOptions.Left, "100%");
            var brt = batteryNumber.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(76f, -42f);

            // Battery Bar 배경
            batteryBack = MakeImage("BatteryBack", panel,
                anchoredPos: new Vector2(0f, -78f),
                size: new Vector2(panelSize.x - 24f, 10f),
                color: new Color(Ink.r, Ink.g, Ink.b, 0.18f),
                anchorY: 1f, pivotY: 1f, centerX: true);

            // Battery Fill
            batteryFill = MakeImage("BatteryFill", batteryBack.rectTransform,
                stretchToParent: true,
                color: Cyan);
            batteryFill.type = Image.Type.Filled;
            batteryFill.fillMethod = Image.FillMethod.Horizontal;
            batteryFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            batteryFill.fillAmount = 1f;

            // Status Text (패널 하단)
            statusText = MakeText("Status", panel, new Vector2(0f, -108f), new Vector2(panelSize.x - 16f, 22f),
                                   15, Cyan, FontStyles.Bold, TextAlignmentOptions.Center, "위치 추적 방어 중");
            var srt = statusText.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -108f);
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
