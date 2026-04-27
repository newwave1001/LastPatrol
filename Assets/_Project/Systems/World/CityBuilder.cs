using UnityEngine;
using LastPatrol.Data;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// CityDataSO를 받아서 그라운드/도로/건물 큐브를 자동 생성. 그레이박스용.
    /// 폴리싱 단계에서 진짜 모델로 교체 가능 — Builder 비활성하고 수동 자산 배치하면 됨.
    ///
    /// 사용:
    ///   1. 빈 GameObject 'City' 만들고 CityBuilder 부착
    ///   2. data 슬롯에 CityDataSO (.asset) 드래그
    ///   3. ContextMenu: Build City (또는 Build On Start ✅ 시 Awake 자동)
    ///
    /// 주의: Build City 호출 시 자식 모두 삭제 후 재생성. 수동으로 추가한 자식도 사라짐.
    /// </summary>
    [DisallowMultipleComponent]
    public class CityBuilder : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CityDataSO data;

        [Header("Behavior")]
        [Tooltip("씬 시작 시 자동 빌드. false면 ContextMenu로 수동 빌드 후 그 결과를 씬에 저장.")]
        [SerializeField] private bool buildOnStart = false;

        [Header("Ground")]
        [Tooltip("그라운드 크기 = svgSize × scale × 이 값. 1이면 SVG 영역 정확. 1.2면 외곽 여유.")]
        [SerializeField] private float groundPadding = 1.2f;

        [Header("Layout")]
        [SerializeField] private bool placeRoads = true;
        [SerializeField] private bool placeBuildings = true;
        [Tooltip("건물 라벨(TextMesh) 자동 부착. 그레이박스 디버깅용.")]
        [SerializeField] private bool placeLabels = true;

        void Start()
        {
            if (buildOnStart) Build();
        }

        [ContextMenu("Build City")]
        public void Build()
        {
            if (data == null)
            {
                Debug.LogError("[CityBuilder] CityDataSO 필요. data 슬롯에 .asset 드래그.", this);
                return;
            }

            ClearChildren();

            if (data.buildGround) BuildGround();
            if (placeRoads)        BuildRoads();
            if (placeBuildings)    BuildBuildings();
        }

        [ContextMenu("Clear")]
        public void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else                       DestroyImmediate(child);
            }
        }

        // ---- Builders ----

        private void BuildGround()
        {
            float w = data.svgSize.x * data.svgToUnityScale * groundPadding;
            float h = data.svgSize.y * data.svgToUnityScale * groundPadding;
            // SVG 중심 기준 그라운드 배치
            Vector2 svgCenter = data.svgSize * 0.5f;
            Vector3 center = data.SvgToUnity(svgCenter);
            center.y = 0f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.position = center;
            // Plane은 기본 10×10 → scale 0.1 = 1 unit
            ground.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);

            ApplyMaterial(ground, data.groundMaterial, data.groundColor);
        }

        private void BuildRoads()
        {
            for (int i = 0; i < data.roads.Count; i++)
            {
                var r = data.roads[i];
                if (r == null) continue;

                Vector3 a = data.SvgToUnity(r.svgFromXY);
                Vector3 b = data.SvgToUnity(r.svgToXY);
                a.y = data.groundBaseY;
                b.y = data.groundBaseY;

                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                float width = data.SvgScale(r.widthSVG);

                var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                road.name = $"Road_{r.name}";
                road.transform.SetParent(transform, false);
                road.transform.position = mid;
                if (len > 0.01f) road.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                road.transform.localScale = new Vector3(width, 0.05f, len);
                // Road는 obstacle 아님 — Layer = Default 유지, Collider 제거 (운전 방해 방지)
                var col = road.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }
                ApplyMaterial(road, data.roadMaterial, data.roadColor);
            }
        }

        private void BuildBuildings()
        {
            int obstacleLayer = LayerMask.NameToLayer(data.obstacleLayerName);

            for (int i = 0; i < data.buildings.Count; i++)
            {
                var b = data.buildings[i];
                if (b == null) continue;

                // 좌상단 + 크기/2 → 중심 SVG
                Vector2 centerSvg = b.svgPosXY + b.svgSizeWH * 0.5f;
                Vector3 centerUnity = data.SvgToUnity(centerSvg);
                float w = data.SvgScale(b.svgSizeWH.x);
                float d = data.SvgScale(b.svgSizeWH.y);
                float h = b.heightUnity;

                centerUnity.y = h * 0.5f + data.groundBaseY;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"B_{b.code}_{b.displayName}";
                go.transform.SetParent(transform, false);
                go.transform.position = centerUnity;
                go.transform.localScale = new Vector3(w, h, d);

                if (obstacleLayer >= 0) go.layer = obstacleLayer;

                ApplyMaterial(go, b.overrideMaterial != null ? b.overrideMaterial : data.defaultBuildingMaterial, b.tint);

                if (placeLabels) AttachLabel(go.transform, b.displayName, h);
            }
        }

        private static void AttachLabel(Transform parent, string text, float buildingHeight)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(0f, buildingHeight * 0.5f + 0.5f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 36;
            tm.characterSize = 0.18f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.227f, 0.180f, 0.157f); // ink
        }

        // URP/Lit는 _BaseColor, Built-in Standard는 _Color. 둘 다 set해서 양쪽 호환.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private static void ApplyMaterial(GameObject go, Material mat, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            if (mat != null) rend.sharedMaterial = mat;

            // MaterialPropertyBlock — 머티리얼 인스턴스 leak 없이 색상만 변경.
            // Edit mode/Play mode 둘 다 안전.
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, color);
            mpb.SetColor(ColorId, color);
            rend.SetPropertyBlock(mpb);
        }
    }
}
