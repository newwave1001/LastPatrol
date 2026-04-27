using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 씬 전환 + 페이드 서비스. 싱글톤(DontDestroyOnLoad)이라 어디서든 호출 가능.
    /// 그레이박스 단계: OnGUI로 검정 페이드. 폴리싱 시 Canvas+Image로 교체 권장.
    ///
    /// 사용:
    ///   SceneTransitionService.Instance.LoadScene("S03_IndoorInvestigation");
    ///   SceneTransitionService.Instance.LoadScene("S03_IndoorInvestigation", 0.6f, 0.4f);
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneTransitionService : MonoBehaviour
    {
        public static SceneTransitionService Instance { get; private set; }

        [Header("Default Fade")]
        [SerializeField] private float defaultFadeOut = 0.6f;
        [SerializeField] private float defaultFadeIn  = 0.4f;
        [Tooltip("페이드 색상. 누아르 톤은 검정 또는 잉크(#3A2E28).")]
        [SerializeField] private Color fadeColor = Color.black;

        [Header("Auto-bootstrap")]
        [Tooltip("씬에 SceneTransitionService 인스턴스 없을 때 LoadScene 호출 시 자동 생성.")]
        [SerializeField] private bool warnIfMissing = true;

        private float _alpha;
        private bool _busy;
        private Texture2D _whitePixel;

        public bool IsTransitioning => _busy;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 부모가 root라야 DontDestroyOnLoad 작동
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        void OnDestroy()
        {
            if (_whitePixel != null) Destroy(_whitePixel);
            if (Instance == this) Instance = null;
        }

        public void LoadScene(string sceneName)
        {
            LoadScene(sceneName, defaultFadeOut, defaultFadeIn);
        }

        public void LoadScene(string sceneName, float fadeOut, float fadeIn)
        {
            if (_busy)
            {
                if (warnIfMissing) Debug.LogWarning("[SceneTransition] already in progress, request ignored.");
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, fadeOut, fadeIn));
        }

        private IEnumerator LoadRoutine(string sceneName, float fadeOut, float fadeIn)
        {
            _busy = true;

            // Fade out
            yield return Fade(0f, 1f, Mathf.Max(0.01f, fadeOut));

            // Async load (활성화는 fade in 직전에)
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneTransition] LoadSceneAsync('{sceneName}') failed. " +
                               "Build Settings의 Scene List에 등록됐는지 확인.");
                _alpha = 0f;
                _busy = false;
                yield break;
            }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) yield return null;
            op.allowSceneActivation = true;
            // op completes when alpha=1 still applied
            while (!op.isDone) yield return null;

            // 한 프레임 대기 — 새 씬 Awake/Start가 끝나도록
            yield return null;

            // Fade in
            yield return Fade(1f, 0f, Mathf.Max(0.01f, fadeIn));

            _alpha = 0f;
            _busy = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            _alpha = from;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            _alpha = to;
        }

        void OnGUI()
        {
            if (_alpha <= 0.001f) return;
            Color prev = GUI.color;
            GUI.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, _alpha);
            GUI.depth = -1000; // 다른 OnGUI보다 위에
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whitePixel);
            GUI.color = prev;
        }
    }
}
