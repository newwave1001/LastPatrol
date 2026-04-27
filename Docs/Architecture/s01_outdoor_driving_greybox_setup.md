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

## 7. 다음 단계 (이번 주 외)

- [ ] 도로/건물에 v05 마스터 지도 반영 (eirinen_master_map_v01.html 참조)
- [ ] 차량 헤드라이트 SpotLight + 비콘 토글 (B 키, mockup의 beacon)
- [ ] HUD: 속도계, 시계, dispatch 상태 (mockup #speedo, #clock 참고)
- [ ] 사건 마커 (Case Marker) 시스템 — 35초 후 dispatch 트리거
- [ ] **Week 6-7**: 인카운터 시스템 (적 차량 추격 → 하차 트리거)
