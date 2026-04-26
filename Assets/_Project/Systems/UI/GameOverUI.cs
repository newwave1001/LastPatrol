using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastPatrol.Characters;

namespace LastPatrol.Systems.UI
{
    // 마렌 사망 시 오버레이 + 리스타트. v11 HTML end-screen 톤.
    public class GameOverUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MarenController maren;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;

        [Header("Display")]
        [SerializeField] private string titleText = "사건 중단";
        [SerializeField] private string hintText = "[R] 다시 시도";
        [SerializeField] private float fadeInSeconds = 1.5f;

        private bool isShown;
        private float showStartTime;

        void Awake()
        {
            HideImmediate();
            if (titleLabel != null) titleLabel.text = titleText;
            if (hintLabel != null) hintLabel.text = hintText;
        }

        void OnEnable()
        {
            if (maren != null) maren.OnDied += Show;
        }

        void OnDisable()
        {
            if (maren != null) maren.OnDied -= Show;
        }

        void Update()
        {
            if (!isShown) return;
            if (canvasGroup != null && fadeInSeconds > 0f)
            {
                float t = (Time.unscaledTime - showStartTime) / fadeInSeconds;
                canvasGroup.alpha = Mathf.Clamp01(t);
            }

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        public void Show()
        {
            if (isShown) return;
            isShown = true;
            showStartTime = Time.unscaledTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        void HideImmediate()
        {
            isShown = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }
    }
}
