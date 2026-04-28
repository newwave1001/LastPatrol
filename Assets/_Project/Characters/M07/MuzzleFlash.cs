using System.Collections;
using UnityEngine;

namespace LastPatrol.Characters.M07
{
    /// <summary>
    /// M-07 머즐 플래시 — TurretController.OnFireEvent 구독해서 사격 순간 짧게 빛 + 스파크.
    ///
    /// 자동 빌드 — 빈 GameObject + MuzzleFlash 부착 시 Light + 메시 자식 자동 생성.
    /// 보통 씬 root에 두면 됨 (위치 무관 — Fire 시 muzzle 좌표로 순간 이동).
    /// </summary>
    [DisallowMultipleComponent]
    public class MuzzleFlash : MonoBehaviour
    {
        [Header("Style")]
        [SerializeField] private Color flashColor = new Color(0.55f, 0.85f, 1.0f);
        [Tooltip("플래시 지속(초). 매우 짧게 — 0.05~0.12.")]
        [SerializeField, Range(0.02f, 0.3f)] private float flashDuration = 0.08f;
        [Tooltip("Light 최대 강도. URP point light 단위.")]
        [SerializeField] private float maxLightIntensity = 8f;
        [Tooltip("스파크 메시 최대 스케일.")]
        [SerializeField] private float maxMeshScale = 0.45f;

        [Header("Auto Build")]
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private Light flashLight;
        [SerializeField] private MeshRenderer flashMesh;
        [SerializeField] private Transform meshTransform;

        private Coroutine _flashRoutine;
        private Material _meshMat;

        void Awake()
        {
            if (autoBuild && flashLight == null) BuildVisual();
            HideImmediate();
        }

        void OnEnable()
        {
            TurretController.OnFireEvent += HandleFire;
        }

        void OnDisable()
        {
            TurretController.OnFireEvent -= HandleFire;
        }

        private void HandleFire(Vector3 muzzlePos, Vector3 dir)
        {
            transform.position = muzzlePos;
            transform.rotation = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                float k = 1f - (t / flashDuration); // 1 → 0
                float ease = k * k; // quadratic ease-out
                if (flashLight != null)
                {
                    flashLight.enabled = true;
                    flashLight.intensity = maxLightIntensity * ease;
                    flashLight.color = flashColor;
                }
                if (meshTransform != null)
                {
                    meshTransform.localScale = Vector3.one * (maxMeshScale * ease);
                }
                if (_meshMat != null)
                {
                    Color c = flashColor;
                    c.a = ease;
                    _meshMat.color = c;
                    if (_meshMat.HasProperty("_BaseColor")) _meshMat.SetColor("_BaseColor", c);
                    if (_meshMat.HasProperty("_EmissionColor")) _meshMat.SetColor("_EmissionColor", flashColor * (ease * 4f));
                }
                yield return null;
            }
            HideImmediate();
            _flashRoutine = null;
        }

        private void HideImmediate()
        {
            if (flashLight != null) flashLight.enabled = false;
            if (meshTransform != null) meshTransform.localScale = Vector3.zero;
        }

        private void BuildVisual()
        {
            // Point light
            var lightGo = new GameObject("FlashLight");
            lightGo.transform.SetParent(transform, false);
            flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 4f;
            flashLight.color = flashColor;
            flashLight.intensity = 0f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;

            // Sphere primitive — bright cyan (메시 자체는 콜라이더 X)
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "FlashMesh";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.zero;
            // 콜라이더 제거
            var col = sphere.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

            flashMesh = sphere.GetComponent<MeshRenderer>();
            meshTransform = sphere.transform;

            // 머티리얼 — URP/Lit fallback. emission 살리기 위해 sharedMaterial 신규 인스턴스.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _meshMat = new Material(shader);
            _meshMat.color = flashColor;
            if (_meshMat.HasProperty("_BaseColor")) _meshMat.SetColor("_BaseColor", flashColor);
            if (_meshMat.HasProperty("_EmissionColor")) _meshMat.SetColor("_EmissionColor", flashColor * 4f);
            _meshMat.EnableKeyword("_EMISSION");
            flashMesh.material = _meshMat;
            flashMesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashMesh.receiveShadows = false;
        }
    }
}
