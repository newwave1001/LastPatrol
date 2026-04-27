# S01 Outdoor Driving — Greybox Setup Guide

**상태**: Week 4 시작 시점 (2026-04-27).
**목표**: 마렌의 차로 그레이박스 도시 블록을 직부감 카메라로 운전할 수 있다 (WASD, 가속/조향/충돌 슬라이드).

이 문서는 Claude가 자동으로 만들 수 없는 Unity 에디터 작업(씬 생성, 컴포넌트 붙이기, 인스펙터 값 설정)을
체크리스트로 정리한 것이다.

---

## 0. 사전 작업 (자동 적용됨)

다음 코드/데이터는 이미 작업되어 있다. Unity를 다시 열면 자동 import.

- `Assets/_Project/Core/Input/LastPatrolInputs.inputactions` — **Drive 액션맵 추가됨**
  - `Move` (Vector2): WASD / 화살표
  - `Boost` (Button): Left Shift
  - `Exit` (Button): F (하차)
  - `Pause` (Button): Esc
- `Assets/_Project/Core/Input/InputReader.cs` — `EnableFootControls()` / `EnableDriveControls()` 모드 전환 추가
- `Assets/_Project/Systems/Vehicle/CarController.cs` — v04 mockup 차량 물리 이식
- `Assets/_Project/Systems/Vehicle/TopDownCarCamera.cs` — 직부감 lookahead 추종

> Unity 에디터를 다시 열면 Input Action Asset이 reimport 되며 `LastPatrolInputs.cs`가 자동 재생성된다.
> 그 후 InputReader가 `inputs.Drive.Move` 등에 정상 접근.

---

## 1. 씬 생성: `S01_OutdoorDriving.unity`

1. Project 창에서 `Assets/_Project/Scenes/S01_OutdoorDriving/` 우클릭 → Create → Scene → 이름 `S01_OutdoorDriving`.
2. 씬 더블클릭으로 열기.
3. 기본 `SampleScene` 카메라/Directional Light는 그대로 둔다.

## 2. 그레이박스 환경 (Probuilder 또는 큐브)

### Ground

- 빈 GameObject `World` 생성 (위치 0,0,0).
- 자식으로 `Plane` 추가, Scale (10, 1, 10) → 100m × 100m 그라운드.
- Material: `Assets/_Project/Art/Materials/M_Snow.mat`이 있으면 적용. 없으면 흰색 임시.

### Roads (4교차로)

도로는 검정/짙은 회색 평면 4개 (가로 2 + 세로 2 = 십자형 교차).

| 이름      | Position    | Scale (X,1,Z) | 비고                   |
|-----------|-------------|---------------|------------------------|
| Road_H_N  | (0, 0.01, 18) | (100, 1, 6)   | 북쪽 가로 도로         |
| Road_H_S  | (0, 0.01, -18)| (100, 1, 6)   | 남쪽 가로 도로         |
| Road_V_W  | (-22, 0.01, 0)| (6, 1, 100)   | 서쪽 세로 도로         |
| Road_V_E  | (22, 0.01, 0) | (6, 1, 100)   | 동쪽 세로 도로         |

> Y를 0.01로 살짝 띄우는 건 ground와 z-fighting 방지.

### Buildings (더미)

도로 사이 6~8개 큐브 건물. v04 BUILDINGS 배열 참고하되 그레이박스니까 대충 배치.

- `Building_01` ~ `Building_08`: Cube 프리미티브, 각각 Scale 대략 (8, 6, 8) ~ (12, 8, 14).
- Layer: **Obstacle** (Edit → Project Settings → Tags and Layers에서 `Obstacle` 레이어 추가 — 8번 권장).
- Box Collider 자동 부여됨.

> **반드시 Obstacle 레이어에** — CarController의 Obstacle Mask가 이 레이어를 BoxCast해서 충돌 슬라이드 처리.

## 3. 차량 (P_Car_Temp)

### GameObject 구성

```
P_Car_Temp (root, layer Default)
├── Body (Cube, Scale 1.8 × 1.0 × 3.2 — 임시 차체)
└── (선택) ForwardMarker (Cube 0.3 × 0.3 × 0.3, position (0, 0.6, 1.8))
```

### Components on root

1. **InputReader** (`Assets/_Project/Core/Input/InputReader.cs`)
2. **CarController** (`Assets/_Project/Systems/Vehicle/CarController.cs`)
   - Input: 같은 GameObject (Reset이 자동 연결)
   - Auto Enable Drive On Start: ✅
   - Max Speed: 18, Boost Multiplier: 1.65, Reverse Multiplier: 0.5
   - Acceleration: 12, Friction: 2.1, Stop Threshold: 0.15
   - Max Turn Rate: 2.9, Full Turn Speed: 9
   - Obstacle Mask: **Obstacle** 레이어만 체크
   - Box Half Extents: (0.9, 0.5, 1.6) — 차체 콜라이더 절반 크기
   - Skin: 0.05, Collision Speed Damp: 0.3

> 자식 Body의 Box Collider는 제거하거나 Trigger로 설정. CarController는 자체 BoxCast로 충돌 처리하므로 root에 별도 Collider 불필요.

### 프리팹화

- `P_Car_Temp` 게임오브젝트를 `Assets/_Project/Characters/Maren/Prefabs/` (없으면 만들어) 안으로 드래그 → 프리팹 생성.
- 명명 규칙: `P_Car_Temp.prefab` (CLAUDE.md `P_` prefix).

### 씬 배치

- 씬에 `P_Car_Temp` 인스턴스 1개 배치. 위치 (0, 0.5, 0). Y rotation 0 (forward = +Z = 북쪽).

## 4. 카메라 (Main Camera)

> ⚠️ **TopDownCarCamera 는 Main Camera 전용이다.** 자동차(P_Car_Temp)에 붙이면
> 차가 78°로 누우면서 Y=18로 솟구치는 증상이 난다. RequireComponent(Camera)로
> 막아두긴 했지만, 실수로 붙였다면 그 컴포넌트를 제거하고 자동차 Transform의
> Position/Rotation을 (0, 0.5, 0) / (0, 0, 0) 으로 리셋할 것.

기존 SampleScene의 Main Camera에 다음 적용:

1. **TopDownCarCamera** 컴포넌트 추가
   - Target: `P_Car_Temp` 인스턴스 (Transform)
   - Car: `P_Car_Temp`의 CarController
   - Height: 18, Look Ahead: 4.5, Follow Smoothing: 0.08, Pitch: 78
   - Clamp To Bounds: 일단 ❌ (나중에 도시 외곽 정해지면 ✅ + Min/Max XZ)
2. Camera 자체:
   - Projection: **Perspective** (FOV 45) 또는 Orthographic Size 12 — 누아르 톤은 약간 원근감 있는 perspective 추천.
   - Clear Flags: Solid Color (#1A2330 정도, 푸른 황혼).
   - Position은 LateUpdate에서 자동 갱신되므로 초기값 무관 (예: (0, 18, -4)).

## 5. Build Settings

- File → Build Profiles → Scene List에 `S01_OutdoorDriving.unity` 추가.

## 6. 검증 (Task 5)

Play 누르고 다음 확인:

- [ ] 컴파일 에러 0
- [ ] WASD/화살표로 차량이 가속·후진·회전
- [ ] Shift 누르면 부스트 (1.65배)
- [ ] 정지 상태에서 좌우 입력 → **회전 안 됨** (자동차 느낌)
- [ ] 건물에 부딪치면 한 축 슬라이드 (벽 따라 미끄러짐), 속도가 30%로 감쇠
- [ ] 카메라가 부드럽게 추종, 진행 방향으로 살짝 미리 보기
- [ ] Console에 InputSystem 관련 경고/에러 없음

문제 있으면:
- 회전이 너무 빠르다/느리다 → CarController의 `Max Turn Rate` 조절 (1.5 ~ 4 사이)
- 카메라가 어지럽다 → `Follow Smoothing` 낮추기 (0.04 ~ 0.06), `Look Ahead` 줄이기
- 충돌이 이상하다 → `Box Half Extents` 차체와 정확히 맞추기, Obstacle 레이어 확인

## 7. HUD 셋업 (속도계 / 시계 / dispatch)

**이미 작업된 코드:**
- `Assets/_Project/Systems/World/GameClock.cs` — 게임 내 시간 (실시간 0.1초 = 게임 1초)
- `Assets/_Project/Systems/UI/DriveHUD.cs` — 우상단 OnGUI HUD (페이퍼/잉크 톤)

### 7.1 GameClock 배치

씬에 빈 GameObject `World` (또는 기존 사용)에 **GameClock** 컴포넌트 추가.
- Start Hour: 7, Start Minute: 14, Start Second: 0 (default OK)
- Real Seconds Per Game Second: 0.1 (v04 정신 = 10× 가속)
- Paused: ❌

### 7.2 DriveHUD 배치

빈 GameObject `HUD` 새로 만들거나 Main Camera에 **DriveHUD** 컴포넌트 추가.
- Car: P_Car_Temp의 CarController 드래그 (비워도 Awake에서 자동 검색)
- Clock: World의 GameClock 드래그 (비워도 자동 검색)
- Show HUD: ✅
- Margin / Box Size 기본값 (240×110 우상단)

### 7.3 검증

Play 후:
- 우상단에 페이퍼색 박스, 잉크 텍스트로 큰 숫자(km/h) + KM/H + 시계(07:14:00 → 진행) + STANDBY
- W로 가속하면 km/h 숫자 즉시 변동
- 시계가 부드럽게 흘러감 (1초 실시간 ≈ 10초 게임 시간)

### 7.4 외부에서 dispatch 변경 (다음 작업)

```csharp
hud.SetDispatch("CASE 12 NORDMAN", DriveHUD.DispatchTone.Cyan);
hud.SetDispatch("ON SCENE", DriveHUD.DispatchTone.Blood);
```

## 8. Dispatch 시스템 셋업 (35초 후 사건 호출)

**이미 작업된 코드:**
- `Assets/_Project/Systems/Dispatch/CaseMarker.cs` — 사건 위치 컴포넌트 (caseId, addressText, dispatchBlurb)
- `Assets/_Project/Systems/Dispatch/DispatchSystem.cs` — 타이머 + 거리 → HUD 전환 (Standby→EnRoute→Near→OnScene)

### 8.1 CaseMarker 배치

씬 빈 GameObject `CaseMarker_Nordman` 생성 → **CaseMarker** 컴포넌트 추가.
- Position: 도로변 적당한 곳 (예: `(15, 0.5, 12)`)
- Case Id: `CASE-0417` (default)
- Address Text: `WESTSIDE 132` (또는 NORDMAN 12 등 시나리오에 맞게)
- Dispatch Blurb: `주거지 이상 신고 — 가족 연락 두절`

> Gizmo로 빨간 구 + 폴이 표시됨. Play 시 자동 숨김 → dispatch 시 활성화.

### 8.2 DispatchSystem 배치

빈 GameObject `Dispatch` 생성 → **DispatchSystem** 컴포넌트 추가.
- 모든 References (Clock / HUD / Car / Marker) 비워둬도 Awake에서 자동 검색.
- Dispatch At Game Second: **35** (기본)
- Near Distance: 18, Arrived Distance: 6 (Unity 미터)
- Hide Marker Until Dispatch: ✅

### 8.3 검증

Play 후:
- 우상단 HUD에 `STANDBY` (잉크색)
- 시계가 07:14:00에서 흘러 07:14:35 도달 (실시간 ≈ 3.5초) → HUD 전환:
  - `EN ROUTE  ·  CASE-0417` (앰버)
  - 콘솔에 `[Dispatch] CASE-0417 주거지 이상 신고...` 로그
- CaseMarker GameObject가 켜짐 (Hierarchy에서 활성 표시)
- 차량으로 마커 18m 이내 진입 → `NEAR  ·  WESTSIDE 132` (시안)
- 6m 이내 도착 → `ON SCENE  ·  WESTSIDE 132` (블러드)

### 8.4 외부 연결 포인트 (다음 단계)

- `DispatchSystem.OnDispatched` 이벤트 → 사건 보드/대화 트리거
- `DispatchSystem.OnArrived` 이벤트 → 실내 진입 프롬프트 (F = enter scene 3)
- `CaseMarker`에 ScriptableObject `CaseDataSO` 참조 추가 → 데이터 외부화

## 9. 씬 전환 셋업 (ON SCENE → F → S03)

**이미 작업된 코드:**
- `Assets/_Project/Systems/World/SceneTransitionService.cs` — 싱글톤 페이드+로드. DontDestroyOnLoad.
- `Assets/_Project/Systems/Dispatch/DispatchSystem.cs` — ON SCENE 시 InputReader Exit(F) 구독 → 씬 로드.

### 9.1 SceneTransitionService 배치

씬에 빈 GameObject `_SceneTransitionService` (또는 기존 `World`에 같이) → **SceneTransitionService** 컴포넌트.
- Default Fade Out: 0.6, Fade In: 0.4
- Fade Color: Black (또는 잉크 #3A2E28)

> 첫 씬에서 1번만 만들면 DontDestroyOnLoad로 다음 씬에도 살아남음. 다만 S03을 먼저 직접 Play할 때는
> 거기에도 SceneTransitionService가 있는 게 안전. 두 번째 인스턴스는 Awake에서 자동 제거됨.

### 9.2 Build Settings 등록

**File → Build Profiles → Scene List**에 다음 씬 추가:
1. `Assets/_Project/Scenes/S01_OutdoorDriving/S01_OutdoorDriving.unity` (index 0)
2. `Assets/_Project/Scenes/S03_IndoorInvestigation/S03_IndoorInvestigation.unity` (index 1)

> `nextSceneOnArrival` 기본값이 `S03_IndoorInvestigation` 이라 이름 일치 필요.
> 등록 안 되어 있으면 콘솔에 `LoadSceneAsync(...) failed` 에러.

### 9.3 DispatchSystem Inspector

- Input 슬롯 비워두면 Awake에서 자동 검색 (P_Car_Temp의 InputReader)
- Next Scene On Arrival: `S03_IndoorInvestigation` (default)
- Show Enter Prompt: ✅ (HUD에 `[F] ENTER` 추가 표시)

### 9.4 검증

Play → 운전 → 35초 후 EN ROUTE → 마커 6m 이내 → HUD: `ON SCENE  ·  WESTSIDE 132  ·  [F] ENTER` (블러드)
→ **F 키** → 검정 페이드 0.6초 → S03 로드 → 페이드 인 0.4초.

> S03 안에서 S01로 돌아갈 트리거는 별도 작업 (OnExitVehiclePressed 동작이 InputReader 모드에 따라 다름).
> 지금은 S03이 끝나면 Play 정지 또는 Edit 다시 S01 셋업.

## 10. 헤드라이트 + 비콘 (B 토글)

**이미 작업된 코드:**
- `Assets/_Project/Systems/Vehicle/Headlights.cs` — 자식 SpotLight ON/OFF + 속도 비례 미세 흔들림
- `Assets/_Project/Systems/Vehicle/BeaconLight.cs` — 적/청 SpotLight 교차 깜빡임, B 토글 (InputReader.OnBeaconPressed)
- `LastPatrolInputs.inputactions` — Drive 맵에 Beacon Button 액션 + B 키 바인딩 추가됨 (Unity 다음 import에 .cs 자동 재생성)

### 10.1 P_Car_Temp 자식 추가

```
P_Car_Temp (root)
├── Body (기존 Cube)
├── Headlight_L (Light: SpotLight)
├── Headlight_R (Light: SpotLight)
├── Beacon_Red   (Light: SpotLight, 옵션 Cube emissive)
└── Beacon_Blue  (Light: SpotLight, 옵션 Cube emissive)
```

#### Headlight_L / Headlight_R

| 필드 | 값 |
|---|---|
| Position | L=(-0.55, 0.55, 1.65), R=(0.55, 0.55, 1.65) — Body 앞쪽 좌우 |
| Rotation | (10, 0, 0) — 살짝 아래로 |
| Light → Type | Spot |
| Light → Range | 25 |
| Light → Inner Spot Angle | 28 |
| Light → Outer Spot Angle | 60 |
| Light → Color | `#FFE5B4` (따뜻한 백색) |
| Light → Intensity | 110 |
| Light → Shadows | Soft Shadows (URP supports) |

#### Beacon_Red / Beacon_Blue

| 필드 | 적색 | 청색 |
|---|---|---|
| Position | (-0.25, 1.05, -0.1) | (0.25, 1.05, -0.1) — Body 위 좌우 |
| Rotation | (35, -25, 0) | (35, 25, 0) — 위쪽 + 약간 옆 |
| Type | Spot | Spot |
| Range | 16 | 16 |
| Inner / Outer | 30 / 60 | 30 / 60 |
| Color | `#DD3838` | `#3E78DD` |
| Intensity | 80 | 80 |

> 옵션 — 작은 Cube(0.18×0.12×0.18) 자식에 두고 Emission 머티리얼(URP/Lit, `M_Beacon_Red.mat` / `M_Beacon_Blue.mat`) 적용하면 BeaconLight 컴포넌트가 emission도 깜빡임에 맞춰 갱신함. 셋업하지 않으면 Light만 깜빡임.

### 10.2 컴포넌트 추가

P_Car_Temp(root)에 다음 두 컴포넌트 추가:

1. **Headlights**
   - Spots: 비워두면 자식 SpotLight 자동 검색 (Beacon들도 잡히지 않도록 — 자동검색이 SpotLight 전부를 잡으므로 명시적으로 Headlight_L, Headlight_R 두 개만 드래그하는 게 안전)
   - Default On: ✅
   - Car: 자기 자신(P_Car_Temp의 CarController) 자동 연결
   - Jitter Degrees At Max Speed: 0.8

2. **BeaconLight**
   - Red Light: Beacon_Red의 Light 컴포넌트 드래그
   - Blue Light: Beacon_Blue의 Light 컴포넌트 드래그
   - (옵션) Red Bulb / Blue Bulb: 큐브 자식의 MeshRenderer 드래그
   - Cycle Seconds: 0.6
   - Input: 비워둬도 OK (씬에서 자동 검색)
   - Start On: ❌ (B 토글로 켜기)

### 10.3 씬 라이팅 (어둑한 푸른 황혼)

헤드라이트 임팩트 살리려면 환경광 어둡게:

| 항목 | 권장값 |
|---|---|
| Directional Light → Intensity | 0.25 (default 1.0에서 낮춤) |
| Directional Light → Color | `#5A6E85` (푸른 회색) |
| Window → Rendering → Lighting → Environment → Source | Color |
| Environment → Ambient Color | `#1A2330` (짙은 푸른 인디고) |
| Skybox Material | 비우거나 어두운 그라데이션 |
| Camera → Clear Flags | Solid Color, `#1A2330` |

> URP라면 Volume(Post-process)에서 Bloom 0.5~1, Vignette 0.25 정도 추가하면 헤드라이트가 더 강조됨 (선택).

### 10.4 검증

- Play → 어둑한 푸른 화면, 차량 앞쪽으로 따뜻한 빛 두 줄
- W로 가속 시 헤드라이트가 살짝 흔들림 (속도 비례)
- 그레이박스 큐브 건물에 빛 닿는 면이 밝아짐
- **B 키** → 비콘 켜짐, 적/청 0.3초씩 교차 깜빡임. 그라운드 위에 적/청 원형 빛 찍힘
- 다시 B → 비콘 꺼짐
- HUD/dispatch 동작 그대로

## 11. 사건 컨텍스트 전달 (S01 → S03)

**작업된 코드:**
- `Assets/_Project/Systems/World/ActiveCase.cs` — 정적 글로벌. `Current`, `SetCurrent`, `Clear`, `OnChanged`.
- `CaseMarker` — `CaseDataSO caseData` 필드 추가. CaseId/AddressText는 caseData 우선, 빈 칸이면 인스펙터 폴백.
- `DispatchSystem.HandleExitPressed` — LoadScene 직전 `ActiveCase.SetCurrent(marker.CaseData)`.
- `InvestigationSystem.Awake` — `ActiveCase.HasCase`면 `currentCase = ActiveCase.Current` (직접 Play 시엔 인스펙터 default).

### 11.1 CaseMarker_Nordman 인스펙터 셋업

CaseMarker 컴포넌트 → **Case Data** 슬롯에 `Assets/_Project/Data/Cases/C_CH1_Nordman.asset` 드래그.
- 슬롯 채우면 caseId/addressText 인스펙터 값은 무시되고 SO 값 사용.
- C_CH1_Nordman.asset 의 caseAddressKR가 비어 있으면 CaseMarker 인스펙터의 addressText 값 폴백.

### 11.2 동작 플로우

```
S01 운전 → 35초 dispatch → ON SCENE → F
  DispatchSystem: ActiveCase.SetCurrent(C_CH1_Nordman)
  SceneTransitionService.LoadScene("S03_IndoorInvestigation")
  ↓
S03 로드 → InvestigationSystem.Awake
  ActiveCase.HasCase → currentCase = ActiveCase.Current
  Start → introDialogue 재생 (C_CH1_Nordman의 introDialogue)
```

### 11.3 검증

- S01에서 Play → 도착 → F → S03 로드 → CH1 norman intro 자동 재생 확인
- S03 직접 Play(디버그) → ActiveCase 없으니 인스펙터 default(이미 박혀있는 currentCase) 그대로 작동
- Console에 어떤 사건이 활성됐는지 보고 싶으면 `ActiveCase.OnChanged += c => Debug.Log($"[Case] {c?.caseId ?? "cleared"}")` 한 줄 추가

### 11.4 사건 종료 시 정리

S03이 끝났을 때 (`InvestigationSystem.OnCaseCompleted`) 또는 다른 사건 트리거 전에:
```csharp
ActiveCase.Clear();
```
호출. 다음 사건 시작 전 잔존 컨텍스트 방지.

## 12. CaseMarker 시각 자산 (폴 + 노란 테이프)

그레이박스용 단순 시각. CaseMarker GameObject 자식으로 두면 dispatch 활성화 시 자동 함께 켜짐.

```
CaseMarker_Nordman (root, CaseMarker 컴포넌트)
└── Visual (빈 GameObject)
    ├── Pole       (Cylinder, Scale 0.05 × 1.6 × 0.05, Position (0, 0.8, 0))
    ├── Tape       (Cube,     Scale 0.6  × 0.05 × 0.6, Position (0, 1.4, 0))
    └── Tape_Cross (Cube,     Scale 0.6  × 0.05 × 0.6, Position (0, 1.4, 0), Rotation (0, 45, 0))
```

머티리얼 (Assets/_Project/Art/Materials/ 에 새로 생성):
- `M_Temp_Pole_Wood.mat` — URP/Lit, Base Color `#3A2E28` (잉크), Smoothness 0.1
- `M_Temp_Caution_Yellow.mat` — URP/Lit, Base Color `#F2C835`, Emission On, Emission Color `#F2C835` 강도 1.5 (어둑한 씬에서 도드라짐)

검증: dispatch 발생 시 마커 위에 **잉크 색 폴 + 노란 X자 테이프** 보임. 헤드라이트가 닿으면 노란색이 강조됨.

## 13. 사건 완료 → S01 복귀

**작업된 코드:**
- `Assets/_Project/Systems/World/CaseExitController.cs` — InvestigationSystem.OnCaseCompleted 구독 → ActiveCase.Clear + LoadScene
- `InvestigationSystem` — `[ContextMenu] Force Complete Case (Debug)` 추가. 5개 단서 다 발견 안 해도 강제 종료로 검증 가능.

### 13.1 S03 씬 셋업

S03_IndoorInvestigation 씬에 빈 GameObject `CaseExit` → **CaseExitController** 컴포넌트 추가.
- Investigation 슬롯 비워둬도 자동 검색
- Return Scene Name: `S01_OutdoorDriving` (default)
- Delay Before Transition: 4초 (outro 다이얼로그 재생 시간 정도)
- Clear Active Case: ✅
- Log Events: ✅ (콘솔 로그)

### 13.2 디버그 검증 (5개 단서 다 안 풀어도 OK)

1. S01에서 Play → 도착 → F → S03 자동 로드
2. S03 진입 후 Hierarchy에서 `InvestigationSystem` GameObject 선택
3. Inspector에서 InvestigationSystem 컴포넌트 우상단 ⋮ → **Force Complete Case (Debug)**
4. 콘솔: `[Investigation] forced complete: CH1_NORDMAN` → outro 다이얼로그 시작
5. 4초 후 (delayBeforeTransition) → 페이드 → S01_OutdoorDriving 자동 로드
6. S01 진입 시 `ActiveCase.Current` 비어있음 → DispatchSystem이 Standby 상태로 다시 시작

### 13.3 정상 흐름

5개 단서 정상 진행 시:
- 마지막 단서 inner voice + 선택지 끝 → CheckCaseComplete → outro 시작 → 4초 후 자동 복귀.
- delayBeforeTransition이 outro 길이보다 짧으면 outro 잘림 → 인스펙터에서 늘림.

## 14. 인카운터 시스템 — Week 6 1차

**작업된 코드:**
- `Assets/_Project/Systems/Vehicle/VehicleHealth.cs` — IDamageable 구현. OnDamaged/OnDeath. ContextMenu 디버그.
- `Assets/_Project/Systems/Encounter/EnemyVehicle.cs` — 단순 chase AI + 박치기 데미지 (BoxCast 충돌)
- `Assets/_Project/Systems/Encounter/EncounterSpawner.cs` — dispatched 후 N초 → 마렌 뒤편에 적 스폰
- `DriveHUD` — HP 게이지 추가 (페이퍼 박스 하단)

### 14.1 P_Car_Temp에 VehicleHealth 추가

P_Car_Temp(root)에 **VehicleHealth** 컴포넌트 추가.
- Max Health: 100 (default)
- Damage Multiplier: 1
- Log Damage: ✅ (디버깅용, 나중에 끄기)

### 14.2 P_EnemyCar_Temp 프리팹

`Assets/_Project/Characters/Enemies/Prefabs/` 에 `P_EnemyCar_Temp.prefab` 생성:

```
P_EnemyCar_Temp (root)
└── Body (Cube, Scale 1.8 × 1.0 × 3.2 — P_Car_Temp와 동일)
```

Root에 컴포넌트 추가:
- **Collider**: Box Collider (size ≈ Body Scale, root에 부착)
- **EnemyVehicle** — Default 값 그대로 (Max Speed 16, Acceleration 28, Ram Damage 12, Cooldown 0.6)
  - Obstacle Mask: Default + Obstacle 레이어 둘 다 체크 (마렌 차도 hit 받게)
- **VehicleHealth** — Max Health 60 (마렌 100보다 약함, 그레이박스 스파링 균형)

머티리얼: 적색 톤(`#A8302A` 블러드 + 좀 더 어두운 빨강) `M_Temp_Enemy.mat` 만들어 Body에 적용.

### 14.3 EncounterSpawner 배치

S01 씬에 빈 GameObject `Encounter` → **EncounterSpawner** 컴포넌트.
- Enemy Prefab: P_EnemyCar_Temp.prefab 드래그
- Target / Dispatch System 비워두면 자동 검색
- Spawn After Seconds: 30 (dispatched 후 30초 = 실시간 3초)
- Require Dispatched: ✅
- Max Spawns: 1 (Week 6 1차는 1대만)
- Stop After Arrival: ✅
- Spawn Distance Behind: 30
- Spawn Lateral Random: 4

### 14.4 DriveHUD HP 게이지

DriveHUD 컴포넌트 인스펙터:
- Car Health: 비워두면 Awake에서 P_Car_Temp의 VehicleHealth 자동 연결
- Show Health Bar: ✅
- Box Size: (280, 154) — 기존 132에서 22 늘림 (HP 게이지 공간)
  - 기존 prefab override 박혀 있으면 ⋮ → Reset 또는 직접 (280, 154) 입력

### 14.5 검증

1. S01 Play → 35초 dispatch 발생
2. 그 후 30초 더 경과 (총 65 게임초, 실시간 ≈ 6.5초) → 콘솔 `[Encounter] spawned enemy #1 at ...`
3. 마렌 뒤편에서 빨간 큐브 차가 따라옴
4. 적이 박치기 → HP 게이지 12% 줄어듦, `[Health] P_Car_Temp -12 from Enemy → 88/100` 콘솔
5. 마렌이 적을 박으면 적 HP도 줄어듦 (양방향)
6. 적 HP 0 → 적 차량 1.5초 후 자동 소멸
7. 마렌 HP 0 → 콘솔에 OnDeath 로그 (게임오버 흐름은 Week 7에서)

### 14.6 ContextMenu 디버그

- VehicleHealth → Debug Damage 25 / Debug Kill (인스펙터 우클릭)
- EncounterSpawner → Spawn Now (Debug) — 시간 안 기다리고 즉시 스폰

## 15. PlayerStatus — 추격 대상 상태

**작업된 코드:**
- `Assets/_Project/Systems/World/PlayerStatus.cs` — 정적 글로벌. `IsHunted`, `CompletedCases`, `OnHuntedChanged` 이벤트.
- `CaseExitController` — 사건 완료 시 `PlayerStatus.SetHunted(true)` + `NotifyCaseCompleted()`.
- `EncounterSpawner` — `requireHunted` 옵션 추가 (default ✅). `IsHunted=false`면 스폰 안 함.
- `DriveHUD` — `IsHunted=true`일 때 우상단 박스 안에 빨간 점 + `HUNTED` 라벨.

### 15.1 의도 (디자인)

> "진실을 알게 된 자가 표적이 된다."
>
> 첫 사건 전엔 도시는 평화로운 운전 + dispatch 무전.
> 사건 하나 해결 → ELI 진영이 마렌을 위협으로 인식 → 다음 사이클부터 적 차량이 추격해서 수사 방해.

### 15.2 흐름

```
첫 Play (S01) → IsHunted=false → 인카운터 스폰 안 됨 → 평화롭게 운전
   dispatch → 도착 → S03
S03 사건 완료 (5단서 또는 Force Complete)
   PlayerStatus.SetHunted(true)
   PlayerStatus.NotifyCaseCompleted()
   → S01 복귀 (현장 앞 위치) → HUD에 ● HUNTED 표시
   → 다음 dispatch → IsHunted=true 체크 통과 → EncounterSpawner 작동 → 적 차량 스폰 → 추격
```

### 15.3 Inspector

- **EncounterSpawner** → Require Hunted: ✅ (default). 끄면 첫 사이클부터 인카운터 (디버그용).
- **CaseExitController** → Mark Player As Hunted: ✅ (default). 끄면 사건 완료해도 hunted 안 됨.

### 15.4 디버그

PlayerStatus는 정적 클래스라 인스펙터에 안 보임. 두 가지로 빠른 검증:
- HUD: ● HUNTED 라벨 보이면 IsHunted=true
- 콘솔: `Debug.Log(PlayerStatus.IsHunted)` 임시 호출 또는 Hierarchy → 아무 GO 우클릭 → Add Component → 디버그 스크립트

> 도메인 리로드(Play 정지/재시작) 시 자동 false로 초기화. 영속 저장은 SaveSystem 책임.

## 16. 캐릭터 전환 (Tab) — 마렌 ↔ M-07

**작업된 코드:**
- `MarenController` — `ControlMode { Manual, Cower }` + `SetMode(mode)`. Cower 모드: M-07 쪽으로 자동 이동, `cowerStopRadius` 안에 들어오면 멈춤.
- `M07Controller` — `ControlMode { Follow, Manual }` + `SetMode(mode)`. Manual 모드: InputReader.MoveAxis로 직접 이동.
- `FollowBehavior.ManualMove(axis)` — 플레이어 입력 기반 이동. SteeringHelper로 회피 적용.
- `Systems/World/PartyController.cs` — Tab(OnSwitchPressed) → 활성 캐릭터 토글.

### 16.1 흐름

```
Tab 누름
  Active = Maren → M07 (또는 그 반대)
  ↓
  Maren  : ControlMode.Cower / Manual 자동 적용
  M-07   : ControlMode.Follow / Manual 자동 적용
```

### 16.2 S03 씬 셋업

S03_IndoorInvestigation 씬에 빈 GameObject `Party` → **PartyController** 컴포넌트.
- 모든 슬롯 비워둬도 Awake에서 자동 검색
- Start With: `Maren` (사건 시작 시 마렌이 활성)
- Auto Switch On Maren Down: ✅ (마렌 사망 시 M-07로 전환)

### 16.3 검증

- S03 진입 → Tab 누름:
  - 마렌 입력 정지, **M-07이 WASD로 이동** ← 활성
  - 마렌이 자동으로 M-07 근처로 걸어옴 (`cowerStopRadius` 1.8m까지)
- 다시 Tab → 원래대로
- 마렌 HP 0 → M-07로 자동 전환

### 16.4 인스펙터 튜닝

**MarenController:**
- Cower Stop Radius (default 1.8) — M-07 너무 붙으면 줄임
- Cower Catchup Radius (default 6) — 거리 멀수록 빠르게 catch-up
- Auto Cover When Cowering: ❌ (1차 단순. 추후 가까운 엄폐물 자동 진입 옵션)

### 16.5 백로그 (Phase B)

- M-07 점프 입력 (Cover 키 또는 별도). 현재 ManualMove는 평지 이동만.
- M-07 자동 사격 (TurretController) — Manual 모드에서도 자동? 또는 별도 키 입력?
- 마렌 Cower 시 자동 엄폐 (가까운 엄폐물 검색 + 진입)
- 활성 캐릭터에 **카메라 추종 변경** (현재는 한 카메라가 마렌 추종 가정. 활성 변경 시 카메라 타겟도 바꿔야 자연스러움)

## 17. 백로그 (다음 단계)

### Week 7 — 인카운터 2차
- [ ] **하차 트리거** (정지 + F → InputReader.EnableFootControls + 마렌 캐릭터 활성화)
- [ ] **인카운터 종료 처리** (적 처치 후 dispatch 재개, 또는 마렌 사망 시 게임오버)
- [ ] **드론 적 유형** (공중 + 원거리 사격)
- [ ] **사이드 사건** 추가 (사건 종료 후 ActiveCase null 상태 + 다음 사건 자동 트리거)

### 그 외
- [ ] 도로/건물에 v05 마스터 지도 반영 (eirinen_master_map_v01.html 참조)
- [ ] HUD를 OnGUI → TMP Canvas로 교체 (폴리싱 단계)
- [ ] 대사 CSV export/import 도구 (Editor Tools, 텍스트 라운드트립)
- [ ] CaseExitController 옵션 — outro 다이얼로그 끝나는 시점을 정확히 감지
