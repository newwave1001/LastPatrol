using UnityEngine;

namespace LastPatrol.Characters.M07
{
    // 로봇의 눈 색은 핵심 시각 비트.
    // 평소: 시안(노아의 유산). 해킹 시: 붉은색.
    public class RobotEyes : MonoBehaviour
    {
        [Header("Eye Renderer")]
        [SerializeField] private Renderer eyeRenderer;
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Header("Colors")]
        [SerializeField] private Color cyanColor = new Color(0.486f, 0.784f, 0.847f);  // #7CC8D8
        [SerializeField] private Color hackedRedColor = new Color(0.659f, 0.188f, 0.165f); // #A8302A

        [Header("Transition")]
        [SerializeField] private float transitionSeconds = 0.6f;

        private MaterialPropertyBlock mpb;
        private Color currentColor;
        private Color targetColor;
        private float transitionTimer;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            currentColor = cyanColor;
            targetColor  = cyanColor;
            ApplyImmediate();
        }

        void Update()
        {
            if (transitionTimer < transitionSeconds)
            {
                transitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(transitionTimer / transitionSeconds);
                currentColor = Color.Lerp(currentColor, targetColor, t);
                Apply();
            }
        }

        public void SwitchToCyan()  => StartTransition(cyanColor);
        public void SwitchToRed()   => StartTransition(hackedRedColor);

        private void StartTransition(Color to)
        {
            targetColor = to;
            transitionTimer = 0f;
        }

        private void Apply()
        {
            if (eyeRenderer == null) return;
            eyeRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(emissionProperty, currentColor);
            mpb.SetColor("_BaseColor", currentColor);
            eyeRenderer.SetPropertyBlock(mpb);
        }

        private void ApplyImmediate()
        {
            currentColor = targetColor;
            Apply();
        }
    }
}
