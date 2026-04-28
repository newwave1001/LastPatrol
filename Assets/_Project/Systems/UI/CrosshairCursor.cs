using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.Combat;
using LastPatrol.Systems.World;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.UI
{
    /// <summary>
    /// M-07 사격 모드용 십자 조준 커서 + 사격 쿨다운 게이지 + 타겟 마커 + Hit confirmation flash.
    ///
    /// 활성 조건 (모두 만족):
    ///   - TurretController.MouseAimActive
    ///   - VehicleDismount.IsDismounted == true (도보)
    ///   - PartyController.Active == M07
    ///
    /// 자동 빌드 — 빈 GameObject + CrosshairCursor 부착 시 Canvas + 모든 UI 자동 생성.
    /// </summary>
    [DisallowMultipleComponent]
    public class CrosshairCursor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TurretController turret;
        [SerializeField] private PartyController party;
        [SerializeField] private VehicleDismount dismount;

        [Header("Style — Crosshair")]
        [SerializeField] private Color coolColor = new Color(0.49f, 0.78f, 0.85f, 0.85f);
        [SerializeField] private Color hotColor  = new Color(0.85f, 0.20f, 0.18f, 0.95f);
        [SerializeField] private Color hitFlashColor = new Color(1f, 1f, 0.85f, 1f); // 노랑 명중 깜빡
        [SerializeField] private float baseSize = 48f;
        [SerializeField] private float lockScale = 1.25f;
        [SerializeField] private float hitFlashScale = 1.6f;
        [SerializeField] private float lineThickness = 2f;
        [SerializeField] private float centerGap = 8f;

        [Header("Style — Cooldown Ring")]
        [Tooltip("쿨다운 ring 반경 (px). 십자 외곽에 표시.")]
        [SerializeField] private float cooldownRingSize = 56f;
        [SerializeField] private Color cooldownRingColor = new Color(0.49f, 0.78f, 0.85f, 0.7f);

        [Header("Style — Target Marker")]
        [Tooltip("적 위치 화면 마커 크기 (px).")]
        [SerializeField] private float targetMarkerSize = 80f;
        [SerializeField] private Color targetMarkerColor = new Color(0.85f, 0.20f, 0.18f, 0.85f);

        [Header("Behavior")]
        [Tooltip("Hit flash 지속 시간(초).")]
        [SerializeField, Range(0.05f, 0.6f)] private float hitFlashDuration = 0.18f;

        [Header("Auto Build")]
        [SerializeField] private bool autoBuildUI = true;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform crosshairRT;
        [SerializeField] private Image crosshairImage;
        [SerializeField] private RectTransform cooldownRingRT;
        [SerializeField] private Image cooldownRingImage;
        [SerializeField] private RectTransform targetMarkerRT;
        [SerializeField] private Image targetMarkerImage;

        private bool _hardwareCursorHiddenByMe;
        private float _hitFlashUntil;
        private Camera _cachedCam;

        void Awake()
        {
            if (turret == null) turret = FindAnyObjectByType<TurretController>(FindObjectsInactive.Include);
            if (party == null) party = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            if (autoBuildUI && canvas == null) BuildUI();
            HideAll();
        }

        void OnEnable()
        {
            Bullet.OnRobotHitEvent += HandleRobotHit;
        }

        void OnDisable()
        {
            Bullet.OnRobotHitEvent -= HandleRobotHit;
            if (_hardwareCursorHiddenByMe)
            {
                Cursor.visible = true;
                _hardwareCursorHiddenByMe = false;
            }
        }

        void Update()
        {
            if (turret == null) turret = FindAnyObjectByType<TurretController>(FindObjectsInactive.Exclude);
            if (party == null)  party  = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);

            bool active = ShouldShow();
            ToggleVisible(active);
            if (!active) return;

            UpdateCrosshair();
            UpdateCooldownRing();
            UpdateTargetMarker();
        }

        private void ToggleVisible(bool active)
        {
            if (crosshairRT != null && crosshairRT.gameObject.activeSelf != active)
                crosshairRT.gameObject.SetActive(active);
            if (cooldownRingRT != null && cooldownRingRT.gameObject.activeSelf != active)
                cooldownRingRT.gameObject.SetActive(active);
            // targetMarker는 자체 로직으로 토글 (적 있을 때만)
            if (!active && targetMarkerRT != null && targetMarkerRT.gameObject.activeSelf)
                targetMarkerRT.gameObject.SetActive(false);

            // 하드웨어 커서
            if (active && Cursor.visible)
            {
                Cursor.visible = false;
                _hardwareCursorHiddenByMe = true;
            }
            else if (!active && _hardwareCursorHiddenByMe)
            {
                Cursor.visible = true;
                _hardwareCursorHiddenByMe = false;
            }
        }

        private void UpdateCrosshair()
        {
            if (crosshairRT == null || crosshairImage == null) return;

            // 위치 — 마우스
            if (Mouse.current != null)
            {
                Vector2 mp = Mouse.current.position.ReadValue();
                crosshairRT.position = new Vector3(mp.x, mp.y, 0f);
            }

            // Hit flash 우선 — 짧은 시간 동안 강조
            bool hitFlash = Time.unscaledTime < _hitFlashUntil;
            bool locked = turret != null && turret.HasTarget;

            Color color;
            float scale;
            if (hitFlash)        { color = hitFlashColor; scale = hitFlashScale; }
            else if (locked)     { color = hotColor;      scale = lockScale; }
            else                 { color = coolColor;     scale = 1f; }

            crosshairImage.color = color;
            crosshairRT.sizeDelta = new Vector2(baseSize * scale, baseSize * scale);
        }

        private void UpdateCooldownRing()
        {
            if (cooldownRingImage == null || cooldownRingRT == null) return;
            if (turret == null) { cooldownRingImage.fillAmount = 0f; return; }

            // 쿨다운 ring 위치 = 십자와 동일
            cooldownRingRT.position = crosshairRT.position;
            // fillAmount = 쿨다운 진행 (1=방금 쏨, 0=ready)
            cooldownRingImage.fillAmount = turret.CooldownNormalized;
            // 색 — 락 시 hot, 아니면 cool
            cooldownRingImage.color = turret.HasTarget ? cooldownRingColor : cooldownRingColor * 0.6f;
        }

        private void UpdateTargetMarker()
        {
            if (targetMarkerRT == null || targetMarkerImage == null) return;
            var t = turret != null ? turret.CurrentTargetTransform : null;
            if (t == null)
            {
                if (targetMarkerRT.gameObject.activeSelf) targetMarkerRT.gameObject.SetActive(false);
                return;
            }
            if (_cachedCam == null || !_cachedCam.gameObject.activeInHierarchy) _cachedCam = Camera.main;
            if (_cachedCam == null)
            {
                if (targetMarkerRT.gameObject.activeSelf) targetMarkerRT.gameObject.SetActive(false);
                return;
            }

            // 적 머리 위 약 1.6m
            Vector3 worldHeadPos = t.position + Vector3.up * 1.6f;
            Vector3 sp = _cachedCam.WorldToScreenPoint(worldHeadPos);
            if (sp.z <= 0f)
            {
                if (targetMarkerRT.gameObject.activeSelf) targetMarkerRT.gameObject.SetActive(false);
                return;
            }
            if (!targetMarkerRT.gameObject.activeSelf) targetMarkerRT.gameObject.SetActive(true);
            targetMarkerRT.position = new Vector3(sp.x, sp.y, 0f);
            targetMarkerImage.color = targetMarkerColor;
        }

        private void HandleRobotHit(Vector3 worldPos)
        {
            _hitFlashUntil = Time.unscaledTime + hitFlashDuration;
        }

        private bool ShouldShow()
        {
            if (turret == null || !turret.MouseAimActive) return false;
            if (dismount != null && !dismount.IsDismounted) return false;
            if (party != null && party.Active != PartyController.ActiveCharacter.M07) return false;
            return true;
        }

        private void HideAll()
        {
            if (crosshairRT != null) crosshairRT.gameObject.SetActive(false);
            if (cooldownRingRT != null) cooldownRingRT.gameObject.SetActive(false);
            if (targetMarkerRT != null) targetMarkerRT.gameObject.SetActive(false);
        }

        // ---- Auto build ----

        private void BuildUI()
        {
            var canvasGo = new GameObject("CrosshairCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 1) Cooldown ring (밑에 깔림)
            var ringGo = new GameObject("CooldownRing");
            ringGo.transform.SetParent(canvasGo.transform, false);
            cooldownRingImage = ringGo.AddComponent<Image>();
            cooldownRingImage.sprite = CreateRingSprite();
            cooldownRingImage.color = cooldownRingColor;
            cooldownRingImage.raycastTarget = false;
            cooldownRingImage.type = Image.Type.Filled;
            cooldownRingImage.fillMethod = Image.FillMethod.Radial360;
            cooldownRingImage.fillOrigin = (int)Image.Origin360.Top;
            cooldownRingImage.fillClockwise = true;
            cooldownRingImage.fillAmount = 0f;
            cooldownRingRT = cooldownRingImage.rectTransform;
            cooldownRingRT.sizeDelta = new Vector2(cooldownRingSize, cooldownRingSize);
            cooldownRingRT.pivot = new Vector2(0.5f, 0.5f);
            cooldownRingRT.anchorMin = new Vector2(0f, 0f);
            cooldownRingRT.anchorMax = new Vector2(0f, 0f);

            // 2) 십자 (위에)
            var imgGo = new GameObject("Crosshair");
            imgGo.transform.SetParent(canvasGo.transform, false);
            crosshairImage = imgGo.AddComponent<Image>();
            crosshairImage.sprite = CreateCrosshairSprite();
            crosshairImage.raycastTarget = false;
            crosshairImage.color = coolColor;
            crosshairRT = crosshairImage.rectTransform;
            crosshairRT.sizeDelta = new Vector2(baseSize, baseSize);
            crosshairRT.pivot = new Vector2(0.5f, 0.5f);
            crosshairRT.anchorMin = new Vector2(0f, 0f);
            crosshairRT.anchorMax = new Vector2(0f, 0f);

            // 3) Target marker — 별도 root (적 위치 추적)
            var markerGo = new GameObject("TargetMarker");
            markerGo.transform.SetParent(canvasGo.transform, false);
            targetMarkerImage = markerGo.AddComponent<Image>();
            targetMarkerImage.sprite = CreateTargetBracketSprite();
            targetMarkerImage.color = targetMarkerColor;
            targetMarkerImage.raycastTarget = false;
            targetMarkerRT = targetMarkerImage.rectTransform;
            targetMarkerRT.sizeDelta = new Vector2(targetMarkerSize, targetMarkerSize);
            targetMarkerRT.pivot = new Vector2(0.5f, 0.5f);
            targetMarkerRT.anchorMin = new Vector2(0f, 0f);
            targetMarkerRT.anchorMax = new Vector2(0f, 0f);
            markerGo.SetActive(false);
        }

        // 64x64 십자 텍스처
        private Sprite CreateCrosshairSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, 0f);

            float center = (size - 1) * 0.5f;
            float thicknessHalf = lineThickness * 0.5f * (size / baseSize);
            float gapHalf = centerGap * 0.5f * (size / baseSize);
            float armLength = (size * 0.5f) - 2f;
            Color white = Color.white;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                bool h = Mathf.Abs(dy) <= thicknessHalf && Mathf.Abs(dx) >= gapHalf && Mathf.Abs(dx) <= armLength;
                bool v = Mathf.Abs(dx) <= thicknessHalf && Mathf.Abs(dy) >= gapHalf && Mathf.Abs(dy) <= armLength;
                bool dot = Mathf.Abs(dx) <= 1.0f && Mathf.Abs(dy) <= 1.0f;
                if (h || v || dot) pixels[y * size + x] = white;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // 128x128 thin ring (radial fill용)
        private Sprite CreateRingSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float outerR = (size * 0.5f) - 2f;
            float innerR = outerR - 4f; // ring 두께 4px
            Color white = Color.white;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r >= innerR && r <= outerR)
                    pixels[y * size + x] = white;
                else
                    pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // 80x80 4-corner bracket sprite (target marker)
        private Sprite CreateTargetBracketSprite()
        {
            const int size = 80;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, 0f);

            int armLen = 14;        // 코너 라인 길이
            int thickness = 3;
            Color white = Color.white;

            // 4 코너에 ㄱ자 그리기
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = (corner == 1 || corner == 3);
                bool top   = (corner == 2 || corner == 3);
                int xAnchor = right ? size - 2 : 1;
                int yAnchor = top ? size - 2 : 1;
                int xDir = right ? -1 : 1;
                int yDir = top ? -1 : 1;

                // 가로 arm
                for (int t = 0; t < thickness; t++)
                for (int a = 0; a < armLen; a++)
                {
                    int xx = xAnchor + xDir * a;
                    int yy = yAnchor + yDir * t;
                    if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                        pixels[yy * size + xx] = white;
                }
                // 세로 arm
                for (int t = 0; t < thickness; t++)
                for (int a = 0; a < armLen; a++)
                {
                    int xx = xAnchor + xDir * t;
                    int yy = yAnchor + yDir * a;
                    if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                        pixels[yy * size + xx] = white;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
