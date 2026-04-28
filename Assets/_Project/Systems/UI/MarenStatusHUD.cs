using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastPatrol.Characters;
using LastPatrol.Systems.Battery;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// 화면 우측, M07StatusHUD 바로 위 마렌 상태 패널.
    /// 프로필 (amber) + 우상단 HUNTED dot + 배터리 X/3 + HP 바(빨강) + 현재 사건명.
    ///
    /// 자동 빌드: 슬롯 비어있으면 Awake에서 Canvas + 자식 위젯 생성.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarenStatusHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MarenController maren;

        [Header("UI Slots (비워두면 Awake에서 자동 생성)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text batteryText;
        [SerializeField] private TMP_Text caseText;
        [SerializeField] private Image profileFrame;
        [SerializeField] private Image hpBack;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Image huntedDot;
        [SerializeField] private TMP_Text huntedLabel;

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildIfMissing = true;
        [SerializeField] private TMP_FontAsset preferredFont;
        [SerializeField] private Vector2 panelSize = new Vector2(260f, 200f);
        [Tooltip("M07StatusHUD 패널 위 띄울 거리(px). M07 panelHeight/2 + gap.")]
        [SerializeField] private float anchorOffsetY = 108f;
        [SerializeField] private float anchorOffsetX = 12f;

        // 팔레트
        static readonly Color Paper = new Color(0.961f, 0.933f, 0.878f, 0.95f);
        static readonly Color Ink   = new Color(0.227f, 0.180f, 0.157f);
        static readonly Color Cyan  = new Color(0.486f, 0.784f, 0.847f);
        static readonly Color Amber = new Color(0.847f, 0.541f, 0.290f);
        static readonly Color Blood = new Color(0.659f, 0.188f, 0.165f);

        void Awake()
        {
            if (maren == null) maren = FindAnyObjectByType<MarenController>(FindObjectsInactive.Include);
            if (autoBuildIfMissing && canvas == null) AutoBuild();
        }

        void OnEnable()
        {
            BatteryInventory.OnChanged += RefreshBattery;
            ActiveCase.OnChanged += HandleCaseChanged;
            RefreshBattery();
            HandleCaseChanged(ActiveCase.Current);
        }

        void OnDisable()
        {
            BatteryInventory.OnChanged -= RefreshBattery;
            ActiveCase.OnChanged -= HandleCaseChanged;
        }

        void Update()
        {
            // HP 바 + 라벨 — MarenController.CurrentHP / MaxHP
            if (maren != null)
            {
                float pct = maren.MaxHP > 0f ? Mathf.Clamp01(maren.CurrentHP / maren.MaxHP) : 0f;
                if (hpFill != null) hpFill.fillAmount = pct;
                if (hpLabel != null)
                    hpLabel.text = $"HP {Mathf.RoundToInt(maren.CurrentHP)}/{Mathf.RoundToInt(maren.MaxHP)}";
            }

            // HUNTED dot + label — PlayerStatus.IsHunted
            bool isHunted = PlayerStatus.IsHunted;
            if (huntedDot != null && huntedDot.gameObject.activeSelf != isHunted)
                huntedDot.gameObject.SetActive(isHunted);
            if (huntedLabel != null && huntedLabel.gameObject.activeSelf != isHunted)
                huntedLabel.gameObject.SetActive(isHunted);
        }

        private void RefreshBattery()
        {
            if (batteryText == null) return;
            int n = BatteryInventory.Count;
            int max = BatteryInventory.Max;
            batteryText.text = $"배터리 {n}/{max}";
            batteryText.color = n == 0 ? Blood : (n >= max ? Cyan : Ink);
        }

        private void HandleCaseChanged(LastPatrol.Data.CaseDataSO data)
        {
            if (caseText == null) return;
            if (data == null)
            {
                caseText.text = "사건 없음";
                caseText.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
            }
            else
            {
                string title = !string.IsNullOrEmpty(data.caseTitleKR) ? data.caseTitleKR
                            : (!string.IsNullOrEmpty(data.caseTitleEN) ? data.caseTitleEN
                            : data.caseId);
                caseText.text = $"추적 중 · {title}";
                caseText.color = Amber;
            }
        }

        // ---- Auto Build ----

        private void AutoBuild()
        {
            var canvasGo = new GameObject("MarenStatusHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel — 우측 중앙에서 위로 (pivot bottom-right)
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = Paper;
            panelImg.raycastTarget = false;
            panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-anchorOffsetX, anchorOffsetY);
            panel.sizeDelta = panelSize;

            // 프로필 (amber 사각, 좌상단)
            profileFrame = MakeImage("ProfileFrame", panel,
                anchoredPos: new Vector2(12f, -12f),
                size: new Vector2(50f, 50f),
                color: Amber,
                anchorX: 0f, pivotX: 0f, anchorY: 1f, pivotY: 1f);

            // HUNTED dot + 라벨 — 패널 우상단 코너 (DriveHUD 기존 스타일)
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

            // Title "마렌"
            titleText = MakeText("Title", panel, new Vector2(76f, -16f), new Vector2(panelSize.x - 90f, 22f),
                                  18, Ink, FontStyles.Bold, TextAlignmentOptions.Left, "마렌");
            var trt = titleText.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(76f, -16f);

            // Battery numeric "배터리 X/3"
            batteryText = MakeText("Battery", panel, new Vector2(76f, -42f), new Vector2(panelSize.x - 90f, 16f),
                                    14, Ink, FontStyles.Normal, TextAlignmentOptions.Left, "배터리 0/3");
            var brt = batteryText.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(76f, -42f);

            // HP bar back
            hpBack = MakeImage("HPBack", panel,
                anchoredPos: new Vector2(0f, -82f),
                size: new Vector2(panelSize.x - 24f, 10f),
                color: new Color(Ink.r, Ink.g, Ink.b, 0.18f),
                anchorY: 1f, pivotY: 1f, centerX: true);

            // HP fill — 빨강
            hpFill = MakeImage("HPFill", hpBack.rectTransform,
                stretchToParent: true,
                color: Blood);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFill.fillAmount = 1f;

            // HP label "HP 100/100"
            hpLabel = MakeText("HPLabel", panel, new Vector2(0f, -98f), new Vector2(panelSize.x - 16f, 14f),
                                11, Ink, FontStyles.Normal, TextAlignmentOptions.Center, "HP 100/100");
            var hrt = hpLabel.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -98f);

            // Case "추적 중 · ..." — word wrap 허용해 긴 제목도 안 잘리게
            caseText = MakeText("Case", panel, new Vector2(0f, -130f), new Vector2(panelSize.x - 16f, 50f),
                                 13, Amber, FontStyles.Bold, TextAlignmentOptions.Center, "사건 없음");
            var crt = caseText.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -130f);
            caseText.textWrappingMode = TextWrappingModes.Normal;
            caseText.overflowMode = TextOverflowModes.Ellipsis;
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
