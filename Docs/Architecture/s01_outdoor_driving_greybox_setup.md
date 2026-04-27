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

## 10. 백로그 (다음 단계)

- [ ] S03 진입 시 사건 컨텍스트 전달 (CaseMarker 정보 → 사건 보드 표시)
- [ ] 차량 헤드라이트 SpotLight + 비콘 토글 (B 키, mockup의 beacon)
- [ ] 도로/건물에 v05 마스터 지도 반영 (eirinen_master_map_v01.html 참조)
- [ ] **Week 6-7**: 인카운터 시스템 (적 차량 추격 → 하차 트리거)
- [ ] HUD를 OnGUI → TMP Canvas로 교체 (폴리싱 단계)
- [ ] CaseMarker에 시각 자산 (폴 + 테이프) 자식 프리팹
- [ ] S03 → S01 복귀 트리거 (사건 종료 후 외부로)
