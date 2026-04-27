using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Systems.Vehicle;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// 외부 운전 씬 우상단 HUD. v04 mockup의 #speedo / #clock / #dispatch-status 이식.
    /// 그레이박스 단계라 OnGUI로 빠르게. 폴리싱 시 TMP Canvas로 교체.
    ///
    /// 색상 팔레트 (CLAUDE.md):
    ///   페이퍼 #F5EEE0 (배경) / 잉크 #3A2E28 (텍스트) / 시안 #7CC8D8 (M-07/dispatch)
    ///   앰버 #D88A4A (마렌/경고) / 블러드 #A8302A (위험)
    ///
    /// 외부 호출:
    ///   SetDispatch("CASE 12 NORDMAN", DispatchTone.Cyan);
    ///   SetDispatch("ON SCENE", DispatchTone.Blood);
    /// </summary>
    [DisallowMultipleComponent]
    public class DriveHUD : MonoBehaviour
    {
        public enum DispatchTone { Ink, Cyan, Amber, Blood }

        [Header("References")]
        [SerializeField] private CarController car;
        [SerializeField] private GameClock clock;
        [SerializeField] private VehicleHealth carHealth;

        [Header("Display")]
        [SerializeField] private bool showHUD = true;
        [Tooltip("우상단으로부터 여백 (px)")]
        [SerializeField] private Vector2 margin = new Vector2(12f, 12f);
        [Tooltip("HUD 박스 폭/높이 (px). HP 게이지 포함하려면 높이 154 권장.")]
        [SerializeField] private Vector2 boxSize = new Vector2(280f, 154f);

        [Header("HP Gauge")]
        [SerializeField] private bool showHealthBar = true;
        [Tooltip("HP 게이지 색상. 페이퍼/잉크 톤에 어울리는 블러드(#A8302A) 권장.")]
        [SerializeField] private Color healthFillColor = new Color(0.659f, 0.188f, 0.165f);
        [SerializeField] private Color healthBackColor = new Color(0.227f, 0.180f, 0.157f, 0.18f);

        // 톤 매핑 (CLAUDE.md 팔레트)
        static readonly Color Paper  = new Color(0.961f, 0.933f, 0.878f, 0.95f);
        static readonly Color Ink    = new Color(0.227f, 0.180f, 0.157f);
        static readonly Color Cyan   = new Color(0.486f, 0.784f, 0.847f);
        static readonly Color Amber  = new Color(0.847f, 0.541f, 0.290f);
        static readonly Color Blood  = new Color(0.659f, 0.188f, 0.165f);

        // dispatch 상태 (외부에서 SetDispatch로 변경)
        private string _dispatch = "STANDBY";
        private DispatchTone _dispatchTone = DispatchTone.Ink;

        // prompt — 별도 라인 ([F] ENTER 같은 입력 안내)
        private string _prompt = string.Empty;
        private DispatchTone _promptTone = DispatchTone.Amber;

        // 스타일 캐시
        private GUIStyle _bigStyle, _smallStyle, _accentStyle, _promptStyle;
        private bool _stylesReady;
        private Texture2D _whitePixel;

        public void SetDispatch(string text, DispatchTone tone = DispatchTone.Ink)
        {
            _dispatch = string.IsNullOrEmpty(text) ? "STANDBY" : text;
            _dispatchTone = tone;
        }

        public void SetPrompt(string text, DispatchTone tone = DispatchTone.Amber)
        {
            _prompt = text ?? string.Empty;
            _promptTone = tone;
        }

        public void ClearPrompt() => _prompt = string.Empty;

        void Awake()
        {
            if (car == null) car = FindFirstObjectByType<CarController>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (carHealth == null && car != null) carHealth = car.GetComponent<VehicleHealth>();
            if (carHealth == null) carHealth = FindFirstObjectByType<VehicleHealth>();
        }

        void OnDestroy()
        {
            if (_whitePixel != null) Destroy(_whitePixel);
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _bigStyle    = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _smallStyle  = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            _accentStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _bigStyle.normal.textColor   = Ink;
            _smallStyle.normal.textColor = Ink;

            // 1×1 white texture (배경 박스용)
            _whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();

            _stylesReady = true;
        }

        Color ToneToColor(DispatchTone t)
        {
            switch (t)
            {
                case DispatchTone.Cyan:  return Cyan;
                case DispatchTone.Amber: return Amber;
                case DispatchTone.Blood: return Blood;
                default: return Ink;
            }
        }

        void OnGUI()
        {
            if (!showHUD) return;
            EnsureStyles();

            float w = boxSize.x, h = boxSize.y;
            float x = Screen.width - w - margin.x;
            float y = margin.y;

            // 페이퍼 배경 박스
            Color prev = GUI.color;
            GUI.color = Paper;
            GUI.DrawTexture(new Rect(x, y, w, h), _whitePixel);
            // 잉크 테두리 (얇게 4면)
            GUI.color = Ink;
            GUI.DrawTexture(new Rect(x,         y,         w, 1),  _whitePixel);
            GUI.DrawTexture(new Rect(x,         y + h - 1, w, 1),  _whitePixel);
            GUI.DrawTexture(new Rect(x,         y,         1, h),  _whitePixel);
            GUI.DrawTexture(new Rect(x + w - 1, y,         1, h),  _whitePixel);
            GUI.color = prev;

            // 속도계 (km/h). v04는 speed * 30, 여기선 m/s × 3.6 = km/h.
            int kmh = car != null ? Mathf.RoundToInt(Mathf.Abs(car.CurrentSpeed) * 3.6f) : 0;
            GUI.Label(new Rect(x, y + 4,  w, 44), kmh.ToString(), _bigStyle);
            GUI.Label(new Rect(x, y + 48, w, 14), "KM/H",        _smallStyle);

            // 시계 (HH:MM:SS)
            string timeText = clock != null ? clock.Display : "07:14:00";
            GUI.Label(new Rect(x, y + 66, w, 14), timeText, _smallStyle);

            // dispatch 상태 (메인 텍스트)
            _accentStyle.normal.textColor = ToneToColor(_dispatchTone);
            GUI.Label(new Rect(x + 4, y + 86, w - 8, 16), _dispatch, _accentStyle);

            // prompt (입력 안내, 별도 라인)
            if (!string.IsNullOrEmpty(_prompt))
            {
                _promptStyle.normal.textColor = ToneToColor(_promptTone);
                GUI.Label(new Rect(x + 4, y + 108, w - 8, 14), _prompt, _promptStyle);
            }

            // HUNTED 인디케이터 (오른쪽 위 코너에 작은 빨간 점 + 라벨)
            if (PlayerStatus.IsHunted)
            {
                float dotR = 6f;
                float dotX = x + w - 12f - dotR * 2f;
                float dotY = y + 12f;
                GUI.color = Blood;
                GUI.DrawTexture(new Rect(dotX, dotY, dotR * 2f, dotR * 2f), _whitePixel);
                GUI.color = prev;
                _accentStyle.normal.textColor = Blood;
                GUI.Label(new Rect(x + w - 80f, dotY - 1f, 60f, 14f), "HUNTED", _accentStyle);
            }

            // HP 게이지 (가장 아래)
            if (showHealthBar && carHealth != null)
            {
                float gaugeY = y + h - 16f;
                float gaugeH = 6f;
                float gaugeX = x + 16f;
                float gaugeW = w - 32f;
                // 배경
                GUI.color = healthBackColor;
                GUI.DrawTexture(new Rect(gaugeX, gaugeY, gaugeW, gaugeH), _whitePixel);
                // 채움
                float fillW = gaugeW * Mathf.Clamp01(carHealth.Normalized);
                GUI.color = healthFillColor;
                GUI.DrawTexture(new Rect(gaugeX, gaugeY, fillW, gaugeH), _whitePixel);
                GUI.color = prev;
                // 수치
                int hp = Mathf.RoundToInt(carHealth.CurrentHealth);
                int maxHp = Mathf.RoundToInt(carHealth.MaxHealth);
                _smallStyle.fontSize = 11;
                GUI.Label(new Rect(x, gaugeY - 14f, w, 12), $"HP {hp}/{maxHp}", _smallStyle);
                _smallStyle.fontSize = 13;
            }
        }
    }
}
