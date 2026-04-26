# LAST PATROL — Unity 아키텍처 설계 v0.1

**대상**: HTML v11 프로토 → Unity C# 이식 설계
**목적**: 어떤 컴포넌트를 어떻게 분리할지, 데이터를 어떻게 ScriptableObject로 빼낼지 명확히 정리

---

## 1. 전체 컴포넌트 다이어그램

```
GameManager (싱글톤)
├─ SceneController (씬 전환)
├─ SaveSystem
└─ GameState (현재 챕터, 진행 상황)

DialogueSystem (전역)
├─ DialoguePanel (UI)
├─ TypewriterEffect
└─ ProfileRenderer (정면 픽셀 아트 또는 3D 렌더)

Scene-Specific:
├─ MarenController
├─ M07Controller
├─ EncounterDirector (인카운터 트리거)
├─ DistanceRule
└─ LevelManager (씬별 사건 진행)

Per-Object:
├─ EnemyAI (인간형/드론/큐레이터/헌터/앰부시)
├─ Cover (엄폐물)
├─ ClueObject (단서)
├─ HiddenDoor (구석방 문)
└─ Bullet (탄환)
```

---

## 2. 캐릭터 컨트롤러

### 2.1 MarenController.cs

**v11 HTML 대응**: `state.cop`, `updateCop()`, 엄폐 로직, 충전 로직

**책임**:
- 입력 처리 (이동, 엄폐, 충전, 조사)
- 엄폐 자동 스냅 시스템
- 배터리 충전 (R 키 홀드)
- 단서 조사 (F 키)
- HP 관리

**구조**:
```csharp
public class MarenController : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private M07Controller robot;
    [SerializeField] private InputReader input;
    [SerializeField] private CharacterMovement movement;
    [SerializeField] private CoverSystem cover;
    [SerializeField] private InteractionSystem interaction;
    
    [Header("Stats")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float chargeRange = 60f;
    [SerializeField] private float chargeRate = 84f; // per second
    
    private float currentHP;
    private bool isCharging;
    
    void Update()
    {
        movement.Tick(input.MoveAxis);
        cover.Tick(input.CoverHeld);
        HandleCharging();
        HandleInteraction();
    }
    
    void HandleCharging() { /* R 홀드 + 거리 체크 */ }
    void HandleInteraction() { /* F 키 → 단서 또는 문 */ }
    public void TakeDamage(float amount) { /* IDamageable */ }
}
```

**서브 컴포넌트로 분리:**
- `CharacterMovement` — 이동·중력·계단 자동 등반
- `CoverSystem` — 엄폐 스냅·해제·총알 차단 판정
- `InteractionSystem` — 주변 단서·문·로봇 잔해 감지

### 2.2 M07Controller.cs

**v11 HTML 대응**: `state.robot`, `updateRobot()`, 자동 사격 로직

**책임**:
- 마렌 따라가기 (호위)
- 적 자동 탐지 (LOS 체크)
- 자동 사격 (배터리 소모)
- 점프 (외부 운전 중엔 비활성)
- 시안→붉은색 시각 전환 (해킹 시)

**구조**:
```csharp
public class M07Controller : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float detectRange = 520f;
    [SerializeField] private float aimAssistRadius = 80f;
    [SerializeField] private float fireCooldown = 0.12f;
    
    [Header("References")]
    [SerializeField] private MarenController maren;
    [SerializeField] private TurretController turret;
    [SerializeField] private RobotEyes eyes; // 시안→붉은색 머티리얼 전환
    
    private float currentBattery;
    private bool isHacked = false;
    
    void Update()
    {
        DetectEnemies();
        AutoFire();
        FollowMaren();
    }
    
    void DetectEnemies() { /* LOS + 거리 체크 */ }
    void AutoFire() { /* 자동 조준 + 사격 */ }
    void FollowMaren() { /* 거리 기반 따라가기 */ }
    
    public void Hack()
    {
        isHacked = true;
        eyes.SwitchToRed();
        // 추가 변화: 음성 톤, 사격 대상 변경 등
    }
}
```

### 2.3 EnemyAI.cs (베이스)

**v11 HTML 대응**: `updateHumanoid`, `updateDrone`, `updateAmbush`

**책임**:
- 타겟 결정 (마렌 우선 / 로봇 차선)
- 조준 텔레그래프 (55프레임 등)
- 사격
- HP 관리

**구조**: 스테이트 머신 + ScriptableObject 데이터
```csharp
public abstract class EnemyAI : MonoBehaviour, IDamageable
{
    [SerializeField] protected EnemyDataSO data;
    protected MarenController target;
    protected M07Controller robot;
    protected EnemyState state = EnemyState.Patrol;
    
    enum EnemyState { Patrol, Alert, Aim, Cooldown }
    
    abstract void Tick();
    abstract void OnAim();
    abstract void OnFire();
}

public class HumanoidAI : EnemyAI { /* 지상 보행 + 사격 */ }
public class DroneAI : EnemyAI { /* 공중 호버 + 빠른 조준 */ }
public class AmbushAI : EnemyAI { /* 경찰 추적 + 빠른 처형 */ }
public class CuratorAI : EnemyAI { /* 보스, HP 높음, 두 무기 */ }
public class HunterAI : EnemyAI { /* CH5 특수 고속 유닛 */ }
```

---

## 3. 시스템 컴포넌트

### 3.1 DistanceRule.cs

**v11 HTML 대응**: `updateDistance()`, 비네트 페이드

**책임**:
- 마렌-M-07 거리 모니터링
- SAFE / WARNING / DANGER 상태 결정
- 인카운터 적 스폰 트리거 (DANGER 시)

```csharp
public class DistanceRule : MonoBehaviour
{
    [Header("Thresholds")]
    [SerializeField] private float safeDist = 5f;
    [SerializeField] private float warningDist = 10f;
    
    [Header("Events")]
    public UnityEvent<DistanceBand> OnBandChanged;
    public UnityEvent OnDangerSpawn;
    
    public enum DistanceBand { Safe, Warning, Danger }
    public DistanceBand CurrentBand { get; private set; }
}
```

### 3.2 DialogueSystem.cs

**v11 HTML 대응**: `showDialogue()`, `updateDialogueTyping()`, 프로필 렌더

**책임**:
- 대사 큐 관리
- 타이프라이터 효과
- 화자 프로필 표시 (마렌 / M-07 / 페카 등)
- 색상 테마 (시안 / 앰버 / 블러드)

```csharp
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;
    
    [SerializeField] private DialoguePanelUI panel;
    [SerializeField] private TypewriterEffect typewriter;
    
    public void Show(DialogueLineSO line) { /* 패널 표시 + 타이프라이터 */ }
    public void Show(string speakerId, string text, ColorTheme theme) { }
    public void Clear() { }
}
```

**ScriptableObject로 분리**: `DialogueLineSO`, `DialogueSequenceSO`

### 3.3 InvestigationSystem.cs

**v11 HTML 대응**: `INVESTIGATION` 객체, `handleInspect()`, 사건 보드

**책임**:
- 챕터별 단서 진행 (기-승-전-결)
- 단서 자동 발견 (경찰 근접 시)
- 단서 수동 조사 (F 키)
- 사건 보드 UI

```csharp
public class InvestigationSystem : MonoBehaviour
{
    [SerializeField] private CaseDataSO currentCase;
    private HashSet<string> discoveredClues = new();
    
    public void Discover(string clueId) { }
    public void Inspect(ClueObject clue) { }
    public bool HasDiscovered(string clueId) => discoveredClues.Contains(clueId);
}
```

### 3.4 EncounterDirector.cs

**v11 HTML 대응**: `spawnAmbush()`, 인카운터 시스템

**책임**:
- 외부 운전 중 인카운터 트리거
- 인간형 차량 추격 vs 드론 공중 공격 분기
- 양쪽 차 정지 → 하차 단계 관리

```csharp
public class EncounterDirector : MonoBehaviour
{
    [SerializeField] private float baseInterval = 60f;
    [SerializeField] private bool isInvestigationActive;
    
    public void TriggerEncounter(EncounterType type) { }
    public void OnBothVehiclesStopped() { /* 하차 시퀀스 */ }
}
```

### 3.5 BatterySystem.cs

**v11 HTML 대응**: 배터리 전송, 충전 시각 효과

**책임**:
- M-07 배터리 상태
- 마렌 충전 처리
- 시각 피드백 (입자, 글로우, "+ CHARGING" 라벨)

### 3.6 CarController.cs (씬 1, 2)

**v11 HTML 대응**: `city_patrol_mockup_v04.html` 의 운전 로직

**책임**:
- 차량 이동 (직부감 시점)
- 카메라 추종 (Cinemachine)
- 차량 HP (= 마렌 생명)
- 하차 처리 (F 키)

---

## 4. 데이터 (ScriptableObject)

### 4.1 EnemyDataSO

```csharp
[CreateAssetMenu]
public class EnemyDataSO : ScriptableObject
{
    public string enemyId;
    public EnemyType type; // Humanoid, Drone, Curator, Hunter, Ambush
    public float maxHP;
    public float aimTimeSeconds;
    public float cooldownSeconds;
    public float bulletSpeed;
    public float damageVsCop;
    public float damageVsRobot;
    public float losRange;
    public GameObject visualPrefab;
}
```

### 4.2 DialogueLineSO

```csharp
[CreateAssetMenu]
public class DialogueLineSO : ScriptableObject
{
    public string speakerId; // "MAREN" / "M-07" / "PEKKA" 등
    [TextArea(2, 5)] public string textKR;
    [TextArea(2, 5)] public string textEN;
    public ColorTheme theme; // Cyan, Amber, Blood, Subtle
    public AudioClip voiceClip; // 선택
}

public enum ColorTheme { Cyan, Amber, Blood, Subtle }
```

### 4.3 CaseDataSO

```csharp
[CreateAssetMenu]
public class CaseDataSO : ScriptableObject
{
    public string caseId; // "CH1_NORDMAN", "SUB_A_MIA" 등
    public string caseTitle;
    public string caseAddress;
    public List<ClueDataSO> clues;
    public List<DialogueLineSO> introDialogue;
    public List<DialogueLineSO> outroDialogue;
}
```

### 4.4 ClueDataSO

```csharp
[CreateAssetMenu]
public class ClueDataSO : ScriptableObject
{
    public string clueId;
    public InvestigationAct act; // 기, 승, 전, 결
    public string label;
    public bool autoDiscover; // 근접 시 자동 vs F 키 필요
    public bool requiresRoomClear;
    public string requiresPreviousClue; // 선행 단서 ID
    public DialogueLineSO discoveryDialogue;
}
```

### 4.5 EncounterDataSO

```csharp
[CreateAssetMenu]
public class EncounterDataSO : ScriptableObject
{
    public string encounterId;
    public EncounterType type; // Humanoid, Drone, Mixed
    public List<EnemyDataSO> enemies;
    public bool requiresInvestigationActive;
}
```

---

## 5. 폴더 구조 (요약, 자세한 건 D 문서)

```
Assets/
├─ _Project/
│  ├─ Core/
│  │  ├─ GameManager.cs
│  │  ├─ SceneController.cs
│  │  ├─ SaveSystem.cs
│  │  └─ Input/
│  │     └─ InputReader.cs
│  ├─ Characters/
│  │  ├─ Maren/
│  │  │  ├─ MarenController.cs
│  │  │  ├─ CoverSystem.cs
│  │  │  └─ P_Maren.prefab
│  │  ├─ M07/
│  │  │  ├─ M07Controller.cs
│  │  │  ├─ TurretController.cs
│  │  │  ├─ RobotEyes.cs
│  │  │  └─ P_M07.prefab
│  │  └─ Enemies/
│  │     ├─ EnemyAI.cs
│  │     ├─ HumanoidAI.cs
│  │     ├─ DroneAI.cs
│  │     ├─ CuratorAI.cs
│  │     └─ Prefabs/
│  ├─ Systems/
│  │  ├─ Dialogue/
│  │  │  ├─ DialogueSystem.cs
│  │  │  ├─ DialoguePanelUI.cs
│  │  │  └─ TypewriterEffect.cs
│  │  ├─ Investigation/
│  │  │  ├─ InvestigationSystem.cs
│  │  │  ├─ ClueObject.cs
│  │  │  └─ HiddenDoor.cs
│  │  ├─ Encounter/
│  │  │  ├─ EncounterDirector.cs
│  │  │  └─ EncounterDataSO.cs
│  │  ├─ Combat/
│  │  │  ├─ Bullet.cs
│  │  │  ├─ Cover.cs
│  │  │  ├─ DistanceRule.cs
│  │  │  └─ BatterySystem.cs
│  │  └─ Vehicle/
│  │     └─ CarController.cs
│  ├─ Data/
│  │  ├─ Enemies/
│  │  ├─ Dialogue/
│  │  ├─ Cases/
│  │  └─ Clues/
│  ├─ Scenes/
│  │  ├─ S01_OutdoorDriving/
│  │  ├─ S02_OutdoorEncounter/
│  │  ├─ S03_IndoorInvestigation/  ← 첫 작업 씬
│  │  └─ S04_RobotBattery/
│  ├─ Art/
│  │  ├─ Materials/
│  │  ├─ Textures/
│  │  └─ Models/
│  └─ Audio/
├─ _ThirdParty/
│  ├─ Synty/
│  ├─ Mixamo/
│  └─ Quaternius/
└─ Plugins/
```

---

## 6. v11 HTML → Unity 매핑 표

| HTML v11 | Unity C# |
|---|---|
| `state.robot`, `state.cop` | `M07Controller`, `MarenController` 컴포넌트 |
| `state.enemies[]`, `state.ambushes[]` | 씬 내 EnemyAI 컴포넌트들 |
| `state.bullets[]` | Bullet 컴포넌트 풀링 |
| `state.covers[]` | Cover 컴포넌트들 |
| `INVESTIGATION` 객체 | CaseDataSO + ClueDataSO 에셋 |
| `DIALOGUE` 객체 | DialogueLineSO 에셋들 |
| `CFG` 상수 | 각 컴포넌트의 SerializeField |
| `update()` 루프 | 각 컴포넌트의 `Update()` |
| `render()` | Unity 자동 렌더링 (필요시 LineRenderer 등) |
| `ctx.fillRect` 픽셀 그리기 | 3D 모델 + 머티리얼 |
| 사이드뷰 카메라 | Cinemachine VirtualCamera (고정 사이드 각도) |
| 직부감 카메라 | Cinemachine VirtualCamera (탑다운) |

---

## 7. 우선순위 (씬 3 실내 수사 첫 작업 기준)

**1주차 — 기본 인프라:**
1. InputReader (New Input System)
2. CharacterMovement
3. MarenController (이동만)
4. M07Controller (따라가기만)
5. Cinemachine 사이드뷰 카메라
6. 임시 그레이박스 환경

**2주차 — 핵심 메카닉:**
1. EnemyAI 베이스 + HumanoidAI
2. Bullet 시스템
3. Cover 시스템 + 마렌 엄폐
4. DistanceRule
5. BatterySystem (R 충전)

**3주차 — 수사 + 대화:**
1. DialogueSystem + UI
2. InvestigationSystem
3. ClueObject + HiddenDoor
4. CH1 노르드만 집 ClueDataSO 데이터 입력

이 순서로 가면 3주 안에 **씬 3가 v11 프로토 수준으로 동작**합니다. 그 후 에셋 적용, 라이팅, 폴리싱.

---

*v0.1 — Unity 이식 시작 전 초안. 작업 진행하며 갱신.*
