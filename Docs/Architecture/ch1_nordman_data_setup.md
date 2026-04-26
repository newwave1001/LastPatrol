# CH1 노르드만 사건 — ScriptableObject 데이터 셋업 가이드

**대상**: Unity 에디터에서 직접 인스턴스 생성 (사용자 작업)
**참조**: `Docs/Story/last_patrol_synopsis_scenes_v01.md` § CH1
**위치**: 모든 .asset 파일은 `Assets/_Project/Data/` 하위

---

## 1. DialogueLine 인스턴스 (4개)

각 단서 발견 시 마렌 대사. 위치: `Assets/_Project/Data/Dialogue/CH1/`

폴더 우클릭 → Create → LastPatrol → Dialogue Line.

| 파일명 | speakerId | textKR | theme |
|---|---|---|---|
| `DL_CH1_BloodFound.asset` | `MAREN` | `피가 말랐다. 몇 시간 전 일이야. 누가 여기서 다쳤다.` | Subtle |
| `DL_CH1_ChildDrawing.asset` | `MAREN` | `...셋이었어.` | Subtle |
| `DL_CH1_DragMarks.asset` | `MAREN` | `누군가 끌려갔어. 깊은 자국. 두 사람 분.` | Subtle |
| `DL_CH1_NoteFound.asset` | `MAREN` | `타코? 타코 술집 말인가... 왜 이 사람이 거기에 갈 일이.` | Amber |

> textEN은 비워둬도 됨 (preferKorean=true가 fallback 처리).
> displaySecondsOverride는 0 그대로.

추가 옵션 (시간 되면): 결 단서 발견 후 M-07 대사도 추가:
- `DL_CH1_NoteAnalysisM07.asset` / speaker `M-07` / "분석: 비공식 만남. 범죄적 가능성 높음." / theme Cyan
- `DL_CH1_NoteFinalMaren.asset` / speaker `MAREN` / "...아이는 없었어." / theme Subtle

---

## 2. Clue 인스턴스 (4개)

위치: `Assets/_Project/Data/Clues/CH1/` (폴더 만들기)

폴더 우클릭 → Create → LastPatrol → Clue Data.

| 파일명 | clueId | act | labelKR | autoDiscover | requiresPreviousClueId | requiresRoomClear | discoveryDialogue |
|---|---|---|---|---|---|---|---|
| `CL_CH1_DriedBlood.asset` | `ch1_blood` | Intro | 마른 혈흔 | ✅ | (비움) | ✅ | DL_CH1_BloodFound |
| `CL_CH1_ChildDrawing.asset` | `ch1_drawing` | Rising | 아이 그림 | ✅ | ch1_blood | ✅ | DL_CH1_ChildDrawing |
| `CL_CH1_DragMarks.asset` | `ch1_drag` | Turn | 끌린 자국 | ✅ | ch1_drawing | ✅ | DL_CH1_DragMarks |
| `CL_CH1_Note.asset` | `ch1_note` | Resolution | 구겨진 쪽지 | ❌ (F로 조사) | ch1_drag | ✅ | DL_CH1_NoteFound |

> 진행 순서: 혈흔(거실 자동) → 아이 그림(주방, 적 처치 후 자동) → 끌린 자국(2층 자동) → 쪽지(구석방, F 조사).
> 모두 `requiresRoomClear=true` — 적이 살아있는 동안엔 발견 X (긴장감 유지).

---

## 3. CaseData 인스턴스 (1개)

위치: `Assets/_Project/Data/Cases/`

폴더 우클릭 → Create → LastPatrol → Case Data → 이름 `C_CH1_Nordman.asset`.

| 필드 | 값 |
|---|---|
| caseId | `CH1_NORDMAN` |
| caseTitleKR | 노르드만 가족 사건 |
| caseAddressKR | 에이리넨 광장 동쪽 132번지 |
| clues | (List에 위 4개 ClueDataSO 순서대로 드래그) |
| introDialogue | (선택, 시간되면 시퀀스 추가) |
| outroDialogue | (선택) |

---

## 4. ClueObject GameObject 셋업 (씬에 배치)

`S03_IndoorInvestigation` 씬에서:

각 단서마다 빈 GameObject 만들고 ClueObject 컴포넌트 + Collider:

### CL_CH1_DriedBlood (거실)
- Hierarchy → Create Empty → 이름 `Clue_DriedBlood`
- Position: 거실 중앙 부근, Y=0.05 (바닥에 근접)
- **BoxCollider** 추가 → Is Trigger ✅, Size (1.5, 0.5, 1)
- **ClueObject** 컴포넌트 추가 → Data: `CL_CH1_DriedBlood` 드래그
- 시각용 빨간 Cube 자식으로 (Scale (1, 0.05, 0.6), 머티리얼 M_Temp_Blood)

### CL_CH1_ChildDrawing (주방, 임시로 거실 한쪽)
- 같은 패턴, 다른 위치
- 자식 시각: 작은 흰 Cube (그림 종이)

### CL_CH1_DragMarks (복도, 임시로 거실 다른 쪽)
- 같은 패턴
- 자식 시각: 길쭉한 어두운 Cube

### CL_CH1_Note (구석방, 임시로 책장 옆)
- **autoDiscover=false라 BoxCollider Is Trigger 끔** (F 조사 트리거는 InteractionSystem이 OverlapSphere로 처리)
- 자식 시각: 작은 흰 Cube (쪽지)

---

## 5. InvestigationSystem GameObject

씬에 InvestigationSystem 컴포넌트가 있어야 단서가 동작.

- Hierarchy 우클릭 → Create Empty → 이름 `InvestigationSystem`
- Add Component → **InvestigationSystem**
- **Current Case**: `C_CH1_Nordman.asset` 드래그
- **Play Intro On Start**: 일단 false (intro 시퀀스 안 만들었으면)

---

## 6. DialogueSystem GameObject + UI 패널

Canvas + 패널 셋업이 가장 무거운 단계. 따로 가이드 추가 필요 (다음 단계).

일단 단서 발견 시 Dialogue 안 떠도 → Console에 InvestigationSystem이 Discover 호출됨. DebugHUD에 단서 수 표시 추가하면 검증 가능.

---

*v0.1 — 셋업 후 막히는 항목 있으면 표시.*
