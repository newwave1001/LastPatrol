using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Systems.Audio;

namespace LastPatrol.Systems.Battery
{
    /// <summary>
    /// 루팅 가능한 폐로봇. 마렌이 [E] 누르면 BatteryInventory에 배터리 1개(혹은 batteryCount) 추가.
    ///
    /// 두 가지 사용처:
    ///   1) 인카운터 적 차량 파괴 시 EnemyDeathLootDropper가 차 옆에 Instantiate
    ///   2) RobotWreckSpawner가 도로 변에 무작위 스폰
    ///
    /// 자동 빌드:
    ///   - autoBuildVisual ✅ + 자식 없음 → Body + Head 큐브 자동 생성 (placeholder)
    ///   - autoBuildTrigger ✅ + 콜라이더 없음 → BoxCollider trigger 자동 부착
    ///
    /// 루팅 후 lootedDestroyDelay 지나면 destroy. 루팅 안 한 wreck는 RobotWreckSpawner가 거리 기반 정리.
    /// </summary>
    [DisallowMultipleComponent]
    public class LootableRobot : MonoBehaviour, IInteractable
    {
        public enum VisualStyle
        {
            /// <summary>로봇 — 몸체 + 머리 큐브. RobotWreckSpawner / 일반 wreck 용.</summary>
            Robot,
            /// <summary>알약 배터리 — 누운 원통 + 양 끝 단자. 적 처치 보상용.</summary>
            BatteryPill
        }

        [Header("Loot")]
        [SerializeField] private int batteryCount = 1;
        [SerializeField] private string label = "폐로봇";
        [Tooltip("Auto build 시 어떤 모양으로 만들지. SetVisualStyle로 외부에서 변경 가능.")]
        [SerializeField] private VisualStyle visualStyle = VisualStyle.Robot;

        [Header("Lifecycle")]
        [Tooltip("루팅 후 destroy까지 대기(초). 0이면 즉시 destroy (집어든 느낌).")]
        [SerializeField] private float lootedDestroyDelay = 0f;
        [Tooltip("> 0 이면 루팅 안 해도 N초 후 자동 despawn. -1 이면 비활성 (Spawner가 관리).")]
        [SerializeField] private float autoDespawnAfterSeconds = -1f;

        [Header("Visual (auto)")]
        [SerializeField] private bool autoBuildVisual = true;
        [SerializeField] private Renderer mainRenderer;
        [SerializeField] private Color baseTint   = new Color(0.45f, 0.45f, 0.48f);
        [SerializeField] private Color lootedTint = new Color(0.30f, 0.30f, 0.32f);

        [Header("Trigger (auto)")]
        [SerializeField] private bool autoBuildTrigger = true;
        [SerializeField] private Vector3 triggerSize = new Vector3(2f, 2f, 2f);

        [Header("Ground Snap")]
        [Tooltip("Start 시 visual의 AABB 바닥이 groundY에 닿도록 root 위치 자동 보정. " +
                 "prefab pivot이 모델 가운데인 경우(반쯤 묻히는 현상) 자동 해결.")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private float groundY = 0f;

        public bool IsLooted { get; private set; }
        public string PromptLabel => IsLooted ? "" : label;
        public int BatteryCount => batteryCount;

        public bool CanInteract(GameObject actor)
        {
            if (IsLooted) return false;
            if (BatteryInventory.IsFull) return false;
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (IsLooted) { Debug.Log($"[Loot] {name} 이미 루팅됨"); return; }
            int added = BatteryInventory.Add(batteryCount);
            if (added <= 0)
            {
                Debug.Log($"[Loot] {name} 인벤토리 가득 ({BatteryInventory.Count}/{BatteryInventory.Max}) — 루팅 실패");
                return;
            }
            IsLooted = true;
            AudioManager.PlaySfx(SfxKey.BatteryLoot, transform.position);
            Debug.Log($"[Loot] {name} 루팅 +{added} → 배터리 {BatteryInventory.Count}/{BatteryInventory.Max}");
            // 즉시 또는 짧은 지연 후 사라짐
            if (lootedDestroyDelay <= 0f) Destroy(gameObject);
            else { ApplyLootedVisual(); Destroy(gameObject, lootedDestroyDelay); }
        }

        public void SetBatteryCount(int n) => batteryCount = Mathf.Max(0, n);
        // 외부에서 visualStyle 변경 — Awake와 Start 사이에 호출 (AddComponent 직후)
        public void SetVisualStyle(VisualStyle style) => visualStyle = style;

        void Awake()
        {
            if (autoBuildTrigger && GetComponent<Collider>() == null)
                BuildTrigger();

            if (autoDespawnAfterSeconds > 0f)
                Invoke(nameof(SelfDespawnIfUnlooted), autoDespawnAfterSeconds);
        }

        // BuildVisual을 Start로 이동 — 외부 caller가 SetVisualStyle 호출 후 빌드되도록.
        // Renderer bounds도 Start에서 보정.
        void Start()
        {
            if (autoBuildVisual && mainRenderer == null && transform.childCount == 0)
                BuildVisual();
            if (snapToGround) SnapToGround();
        }

        // Visual의 AABB 바닥을 groundY에 맞춤 (모델 pivot이 가운데인 경우 묻힘 방지).
        private void SnapToGround()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float bottom = b.min.y;
            float diff = groundY - bottom;
            if (Mathf.Abs(diff) < 1e-4f) return;
            transform.position += new Vector3(0f, diff, 0f);
        }

        private void SelfDespawnIfUnlooted()
        {
            if (IsLooted) return;
            Destroy(gameObject);
        }

        private void ApplyLootedVisual()
        {
            ApplyTintTo(mainRenderer, lootedTint);
            // 자식 메시도 같이 어둡게
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r != mainRenderer) ApplyTintTo(r, lootedTint * 0.85f);
        }

        // ---- Auto build helpers ----

        private void BuildVisual()
        {
            if (visualStyle == VisualStyle.BatteryPill) BuildBatteryPillVisual();
            else BuildRobotVisual();
        }

        // 로봇 모양 — 몸체 + 머리 큐브. RobotWreckSpawner 등 일반 wreck 용.
        private void BuildRobotVisual()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1.0f, 0.6f);
            DestroyChildCollider(body);
            mainRenderer = body.GetComponent<Renderer>();
            ApplyTintTo(mainRenderer, baseTint);

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            head.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
            DestroyChildCollider(head);
            ApplyTintTo(head.GetComponent<Renderer>(), baseTint * 0.9f);
        }

        // 알약 배터리 — 누운 원통 + 양 끝 단자. 휴머노이드/드론 격파 보상.
        private void BuildBatteryPillVisual()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "BatteryBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            body.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DestroyChildCollider(body);
            mainRenderer = body.GetComponent<Renderer>();
            ApplyTintTo(mainRenderer, new Color(0.20f, 0.70f, 0.85f)); // cyan

            var capPos = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capPos.name = "BatteryCapPositive";
            capPos.transform.SetParent(transform, false);
            capPos.transform.localPosition = new Vector3(0f, 0.1f, 0.22f);
            capPos.transform.localScale = new Vector3(0.08f, 0.08f, 0.05f);
            DestroyChildCollider(capPos);
            ApplyTintTo(capPos.GetComponent<Renderer>(), new Color(0.95f, 0.85f, 0.30f)); // yellow

            var capNeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capNeg.name = "BatteryCapNegative";
            capNeg.transform.SetParent(transform, false);
            capNeg.transform.localPosition = new Vector3(0f, 0.1f, -0.22f);
            capNeg.transform.localScale = new Vector3(0.08f, 0.08f, 0.05f);
            DestroyChildCollider(capNeg);
            ApplyTintTo(capNeg.GetComponent<Renderer>(), new Color(0.50f, 0.50f, 0.55f)); // gray
        }

        private void BuildTrigger()
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = triggerSize;
            box.center = new Vector3(0f, triggerSize.y * 0.5f, 0f);
        }

        private static void DestroyChildCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private static void ApplyTintTo(Renderer r, Color c)
        {
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
