using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastPatrol.Systems.World;
using LastPatrol.Systems.Audio;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// 전투 상태 전환 시 화면 중앙 toast 메시지.
    ///   InCombat false → true : "AMBUSH" (적색, 강한 페이드 인)
    ///   InCombat true → false : "전투 종료" (페이퍼, 부드러운 페이드)
    ///
    /// CombatStatus.InCombat 폴링 — 별도 이벤트 없이 매 프레임 변화 감지.
    /// 자동 빌드 — 빈 GameObject에 컴포넌트만 부착하면 Canvas/Text 자동 생성.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatHUD : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private string ambushText = "AMBUSH";
        [SerializeField] private string clearText  = "전투 종료";
        [SerializeField] private string ambushSubtitle = "마렌, 차에서 내려 도주!";
        [SerializeField] private string clearSubtitle  = "위협 제거. 차로 돌아가자.";

        [Header("Style")]
        [SerializeField] private Color ambushColor = new Color(0.85f, 0.20f, 0.18f);
        [SerializeField] private Color clearColor  = new Color(0.95f, 0.93f, 0.88f);

        [Header("Timing")]
        [SerializeField] private float showDuration = 2.5f;
        [SerializeField] private float fadeInDuration  = 0.4f;
        [SerializeField] private float fadeOutDuration = 0.8f;

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildUI = true;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;

        private bool _wasInCombat;
        private Coroutine _fadeRoutine;

        void Awake()
        {
            if (autoBuildUI && canvas == null) BuildUI();
            if (group != null) group.alpha = 0f;
        }

        void Update()
        {
            bool now = CombatStatus.InCombat;
            if (now && !_wasInCombat)
            {
                ShowToast(ambushText, ambushSubtitle, ambushColor);
                AudioManager.PlaySfx(SfxKey.AmbushAlarm);
            }
            else if (!now && _wasInCombat)
            {
                ShowToast(clearText, clearSubtitle, clearColor);
                AudioManager.PlaySfx(SfxKey.CombatClear);
            }
            _wasInCombat = now;
        }

        private void ShowToast(string title, string subtitle, Color color)
        {
            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = color;
            }
            if (subtitleLabel != null)
            {
                subtitleLabel.text = subtitle;
                subtitleLabel.color = color * 0.85f;
            }
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            // Fade in
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            if (group != null) group.alpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(showDuration);

            // Fade out
            t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
                yield return null;
            }
            if (group != null) group.alpha = 0f;
            _fadeRoutine = null;
        }

        // ---- Auto build ----

        private void BuildUI()
        {
            var canvasGo = new GameObject("CombatHUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8000; // GameOver(9999)보다 아래

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false; // 입력 차단 X — 정보 toast만

            titleLabel    = CreateText(canvasGo.transform, "Title",    ambushText,    120f, FontStyles.Bold,   ambushColor,           new Vector2(0.5f, 0.72f));
            subtitleLabel = CreateText(canvasGo.transform, "Subtitle", ambushSubtitle, 40f, FontStyles.Normal, ambushColor * 0.85f,   new Vector2(0.5f, 0.62f));
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
