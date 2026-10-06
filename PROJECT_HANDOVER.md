# 프로젝트 인수인계: 복셀 레이 Supercover (LineSegmentation)

- 작성: 2026-10-05
- 기준 커밋: `6b08761 Implement VoxelSupercover`
- 환경: Unity 6000.6.4f1, URP, Input System 전용(`activeInputHandler: 1`), 스크립팅 백엔드 Mono

## 1. 요약

복셀 게임에서 플레이어와 타겟 사이를 레이캐스트하기 전에, 레이를 가리는 복셀이 있는지 복셀 격자에서 먼저 걸러내는 사전 필터다. 레이가 지나가거나 닿기만 하는 복셀을 빠짐없이 구한다(closed supercover).

| 항목 | 상태 |
|---|---|
| 알고리즘(메인 스레드) | 완료. 사양 문서의 샘플 코드를 버그 수정하고 최적화했다 |
| EditMode 테스트 | 22개 모두 통과(케이스 약 24,000건) |
| 테스트 씬 | 완료. `Assets/Scenes/VoxelRayTest.unity` |
| Job/Burst 버전 | 미착수. 이번 범위는 메인 스레드만이었다 |
| 플레이어 빌드 성능 측정 | 미측정 |

## 2. 사양 문서 (주의: 저장소에 없음)

구현은 아래 두 문서를 따른다.

- `Docs/voxel-ray-supercover.md`: 구현 지침(요구 사항, 알고리즘, 샘플 코드, 필수 테스트, 미결정 사항)
- `Docs/algorithm-research.md`: 배경과 다른 알고리즘(Bresenham, DDA, Zingl 두께 선, 원형 Amanatides–Woo) 비교

**두 문서는 로컬에만 있다.** git에서 삭제됐고(`ef52b6a`, `42a8c80`) `.gitignore`에 `/[Dd]ocs/`가 있다. 저장소를 새로 받으면 없으니 원본(Force Masters Game Design 위키)에서 받거나 따로 전달받는다. `CLAUDE.md`와 `AGENTS.md`도 `.gitignore` 대상이라 로컬에만 있다.

## 3. 코드 구조

| 경로 | 어셈블리 | 내용 |
|---|---|---|
| `Assets/Scripts/VoxelRay/VoxelSupercover.cs` | `VoxelRay` | 알고리즘 본체. `IVoxelVisitor`, `VoxelSupercover.Traverse`, `WorldToVoxel` |
| `Assets/Scripts/VoxelRay/VoxelListCollector.cs` | `VoxelRay` | 방문 복셀을 `List<int3>`에 모으는 방문자(메인 스레드 전용) |
| `Assets/Tests/EditMode/VoxelSupercoverTests.cs` | `VoxelRay.Tests.EditMode` | NUnit 테스트 |
| `Assets/Tests/EditMode/SupercoverOracle.cs` | 〃 | 정답 판정기(BigInteger 유리수 계산) |
| `Assets/Tests/EditMode/SupercoverChecks.cs` | 〃 | 입력 유형 생성기와 합격 기준 검사 |
| `Assets/Tests/EditMode/ReferenceSupercover.cs` | 〃 | 사양 문서 5절 샘플 코드 원본(비교와 벤치마크용) |
| `Assets/Scripts/Demo/VoxelRayDemo.cs` | `Assembly-CSharp` | 테스트 씬 진행: 타겟 생성, segmentation, 표시, HUD |
| `Assets/Scripts/Demo/PlayerMover.cs` | 〃 | 플레이어 이동 |
| `Assets/Scripts/Demo/OrbitCamera.cs` | 〃 | 궤도 카메라 |
| `Assets/Materials/*.mat` | — | 자리표시자 머티리얼(Player, Target, Muzzle, RayLine) |
| `Assets/Scripts/Rasterizer4.cs`, `RasterizerTest.cs`, `Utils.cs` | `Assembly-CSharp` | 이전 Bresenham 계열 래스터라이저. 테스트 씬에서 쓰지 않으며 손대지 않았다 |

- `Unity.Mathematics`는 이 Unity 버전에서 엔진 모듈(`UnityEngine.MathematicsModule`)이라 asmdef에 패키지 참조가 필요 없다.
- `int3`/`double3`는 `Unity.Mathematics` 타입이다. 같은 이름의 `Unity.Mathematics.Random`이 있어서 `System.Random`이나 `UnityEngine.Random`을 쓸 때는 전체 이름을 적어야 한다.

## 4. 알고리즘

### 사용법

```csharp
struct OcclusionVisitor : IVoxelVisitor
{
    public bool Blocked; public int3 Hit;
    public bool Visit(int3 v)
    {
        if (!IsSolid(v)) return false;   // IsSolid는 프로젝트의 복셀 저장소에 맞춘다
        Blocked = true; Hit = v;
        return true;                     // true를 반환하면 즉시 멈춘다
    }
}

var visitor = new OcclusionVisitor();
double3 p0 = VoxelSupercover.WorldToVoxel(muzzle.position, gridOrigin, voxelSize);
double3 p1 = VoxelSupercover.WorldToVoxel(lockOnPoint, gridOrigin, voxelSize);
VoxelSupercover.Traverse(p0, p1, ref visitor);
```

- 시작점에서 끝점 방향 순서로 방문한다. 첫 번째 꽉 찬 복셀에서 멈추면 가장 가까운 가림 복셀을 얻는다.
- 방문자는 제네릭 struct라서 가상 호출과 힙 할당이 없다.

### 방식

사양 문서 3절의 이벤트 점 경계 상자 순회(Event-Box Supercover)다. 레이가 정수 경계면을 넘는 시점(이벤트)을 가까운 순서로 구하고, 시작점·각 이벤트 점·끝점에서 그 점이 닿는 복셀 전부(1/2/4/8개)를 방문한다. 직전 점의 상자에 있던 복셀은 건너뛴다. 꼭짓점, 모서리, 면을 정확히 지나는 경우와 경계면 위를 따라가는 경우가 한 규칙으로 처리된다.

### 샘플 코드에서 고친 버그

샘플 코드는 끝점 입력을 검사하지 않는다. 재현 결과는 다음과 같다.

| 입력 | 샘플 코드 동작 | 현재 구현 |
|---|---|---|
| `p1 = (+∞, 0.5, 0.5)` | 무한 루프. t가 계속 0으로 계산된다 | `ArgumentException` |
| `p1 = (1e300, 0.5, 0.5)` | 사실상 무한 루프, int 넘침 | `ArgumentException` |
| `p1 = (NaN, 10.5, 0.5)` | 엉뚱한 복셀(음수 x 쪽)을 방문 | `ArgumentException` |

이 경우 말고는 샘플 코드가 정답과 완전히 일치했다(5절 검증).

### 최적화

사양 문서가 바꾸지 말라고 한 ε(`1e-9`), 상자 방문, 직전 상자 중복 제거 규칙은 그대로다. 아래 변경은 모두 결과를 바꾸지 않으며, 테스트가 샘플 코드와 **방문 순서까지 같은지** 매번 확인한다.

1. `int3`/`double3` 인덱서(fixed 포인터 + 범위 검사) 대신 축별 스칼라 변수
2. 경계를 넘는 축의 범위를 경계값 k에서 바로 `[k−1, k]`로 놓는다(`ceil(k−ε)−1`, `floor(k+ε)`와 같은 값)
3. 방향 성분이 0인 축의 범위는 한 번만 계산(`p0 + t·0 = p0`)
4. floor/ceil을 범위 검사된 값의 절삭 변환으로 계산
5. 직전 상자 포함 검사를 축별로 끌어올림
6. 넘는 축만 2칸인 흔한 상자는 루프 없이 방문(`VisitPair`)

넣지 않은 최적화: 나눗셈 대신 역수 곱. .NET 9 Release 측정에서 3%만 빨라졌고, 사양 문서가 정한 `(다음 경계 − P0) / d` 계산 규칙을 바꾸므로 제외했다.

### 입력 범위

- `MaxCoordinate = 2^30`(복셀 좌표): 복셀 인덱스가 int에 들어가기 위한 한계다. 넘으면 `ArgumentException`.
- 정밀도 한계는 따로 있다. ε = 1e-9가 의미 있으려면 좌표가 대략 ±1e5 안이어야 한다. 좌표가 커지면 원점을 가까운 청크 기준으로 옮긴 뒤 변환한다(사양 문서 5절). ε 값 자체는 사양 문서 8절의 미결정 사항이다.

## 5. 검증

### 정답 판정기

`SupercoverOracle`은 닫힌 선분과 닫힌 복셀 `[v, v+1]^3`의 교차를 유리수로 정확히 계산한다. double은 이진 유리수라서 끝점을 공통 2^s 배율의 BigInteger로 바꾸면 모든 비교가 정수 교차곱이 된다. 그래서 1/4 격자 입력뿐 아니라 임의의 double 입력도 정확히 판정한다. 후보 복셀은 선분을 0.5 이하 간격으로 표본화해 구하며, 순회 알고리즘과 독립이다.

### 테스트 (`VoxelRay.Tests.EditMode`, 22개)

| 테스트 | 내용 |
|---|---|
| `MatchesOracle` ×8 | 사양 문서 7절 입력 유형: 1/4 격자 10,000 · 정수 끝점 3,000 · 축 평행 3,000 · 길이 0 1,000 · 무작위 실수 4,000 · 먼 긴 레이(1/4 격자) 600 · 먼 긴 레이(실수) 400 · 음수 좌표 2,000 |
| `CountsTouchedVoxels` ×11 | 손으로 센 경우(꼭짓점 8개, 모서리 4개, 면 2개, 대각선 등) |
| `RejectsNonFiniteAndOutOfRangeInput` | NaN, ±∞, 범위 밖 입력 거부 |
| `StopsImmediatelyWhenVisitorReturnsTrue` | 방문자가 멈추면 즉시 종료하고 방문한 앞부분이 전체 결과와 같다 |
| `Benchmark_ReferenceVsOptimized` | 성능 비교 로그(합격 기준 아님), 결과 해시 일치 확인 |

합격 기준(사양 문서 7절): 모든 입력에서 누락 0, 중복 방문 0, 샘플 코드와 방문 순서 일치. 정확히 표현되는 좌표(무작위 실수 두 유형 제외)는 추가 0, 첫 방문 t가 감소하지 않음까지 확인한다.

### 결과

- Unity EditMode: 22/22 통과(약 12초)
- Unity 밖 .NET 9 하네스: 102,000건 통과(누락·추가·중복·순서 위반·샘플 코드와 불일치 모두 0)
- 테스트 씬 플레이 모드: 화면에 놓인 Checkbox 위치가 레이 10개 모두 정답 판정기 결과와 일치

### 실행 방법

- 에디터가 열려 있을 때: Unity MCP의 `run_tests`(`mode: editor`, `filter_type: assembly`, `filter: VoxelRay.Tests.EditMode`) 또는 Test Runner 창
- 에디터가 닫혀 있을 때: `unity test --mode EditMode`
- `unity test`는 배치 모드 에디터를 새로 띄우므로 프로젝트가 열려 있으면 쓸 수 없다.

## 6. 성능

| 환경 | 샘플 코드 | 현재 구현 | 배율 | 조건 |
|---|---|---|---|---|
| Unity 에디터 Mono, Code Optimization: Debug | 5,697 ns/ray | 1,966 ns/ray | ×2.90 | 레이 20,000개, 평균 약 24복셀/레이 |
| .NET 9 Release(RyuJIT) | 574 ns/ray | 442 ns/ray | ×1.30 | 레이 200,000개, 평균 약 24복셀/레이 |

- 테스트 씬 플레이 모드(에디터, Debug)에서는 레이당 약 31~36복셀에 2.7~3.8 µs다.
- 에디터 Debug 모드는 인라이닝을 하지 않아서 인덱서 제거 효과가 크게 보이고, RyuJIT Release는 샘플 코드도 잘 최적화해서 차이가 작게 보인다.
- 실제 게임에 가까운 플레이어 빌드(Mono Release, IL2CPP)는 측정하지 않았다.

## 7. 테스트 씬

`Assets/Scenes/VoxelRayTest.unity`. 빌드 설정에 인덱스 1로 들어 있다(0은 기존 `SampleScene`).

### 조작

| 입력 | 동작 |
|---|---|
| WASD | 카메라 기준 수평 이동 |
| E / Q | 상승 / 하강 |
| Shift | 가속(×3) |
| 우클릭 드래그 / 휠 | 카메라 회전 / 줌 |
| T 또는 Spawn targets 버튼 | 타겟 생성(슬라이더로 1~10개) |
| R 또는 Segment rays 버튼 | 총구 → 각 타겟 중심 레이를 segmentation하고 복셀마다 Checkbox 배치 |
| C 또는 Clear 버튼 | 타겟과 결과 모두 지우기 |
| L 또는 Live update 토글 | 매 프레임 다시 segmentation |

HUD에 타겟·레이 수, segment 수, 고유 복셀 수, 순회 시간(표시 비용 제외)이 나온다.

### 씬 구성

```
Main Camera            OrbitCamera
Directional Light
Player                 PlayerMover (위치 = 실린더 중심)
├─ Body                실린더, 스케일 (1, 0.9, 1) → 높이 1.8, 폭 1
└─ Muzzle              노란 구, 로컬 (0, 0, 0.6) = 레이 시작점
VoxelRayDemo           VoxelRayDemo
├─ Targets             (실행 중 생성) 타겟 큐브
└─ Rays                (실행 중 생성) Ray NN: LineRenderer + Checkbox "Voxel (x, y, z)"
HUD                    Canvas(Screen Space Overlay, 1920×1080 기준) → Panel(슬라이더, 버튼, 토글, 텍스트)
EventSystem            InputSystemUIInputModule
```

레이마다 그룹 오브젝트를 두어 Hierarchy에서 레이 하나씩 켜고 끌 수 있다. Checkbox는 레이별로 풀링해서 다시 쓴다.

### 설계 결정 (요청에 명시되지 않아 정한 것)

- 기존 `SampleScene`은 그대로 두고 씬을 새로 만들었다.
- 3차원 이동을 위해 상승/하강 키 E/Q를 추가했다.
- 플레이어 정면(총구 방향)은 카메라 yaw를 따른다(TPS 방식).
- 타겟은 복셀 격자에 맞춰 놓아(`snapTargetsToGrid`) 락온 포인트가 그 복셀의 중점이 되게 했다. 사양 문서 2절의 "복셀 형태 타겟"에 맞춘 것이다. 거리는 플레이어 중심에서 타겟 중심까지 2~30유닛이다.
- Checkbox 크기를 0.98로 줄였다(`checkboxScale`). 1이면 타겟 복셀의 Checkbox가 타겟 큐브와 겹쳐 깜빡인다.
- **`Checkbox.prefab`에는 중력이 켜진 Rigidbody와 BoxCollider가 있다.** 그대로 두면 떨어지므로 생성한 인스턴스에서만 물리를 끈다(`DisablePhysics`). 프리팹 자산은 수정하지 않았다.
- Checkbox 머티리얼은 원래 URP Unlit 투명에 알파 텍스처라서 테두리만 보인다. 에셋의 의도대로다.
- 첫 Segment 측정값에 JIT 컴파일 시간이 섞이지 않도록 `Awake`에서 순회를 한 번 미리 돌린다.
- 클릭한 UI가 선택된 채로 남으면 WASD가 UI 내비게이션으로 먹혀 슬라이더 값이 바뀐다. 그래서 마우스를 누르고 있지 않으면 매 프레임 선택을 해제한다.
- UI는 uGUI + TextMesh Pro이고, 이를 위해 TMP Essentials를 가져왔다(`Assets/TextMesh Pro/`).

## 8. 작업 환경 메모

- **Unity MCP**(`unity-editor-mcp`): GameObject는 `instanceId`가 아니라 `hierarchyPath`로 지정한다(JSON에서 64비트 정밀도가 손실됨). 스크립트 재컴파일(도메인 리로드) 중에는 연결이 끊겼다가 다시 붙는다.
- **콘솔의 Pipeline 오류**: `Failed to handle /api/commands request: ... Connection reset by peer`는 Pipeline 패키지 서버 쪽 로그이고 프로젝트 오류가 아니다.
- **TMP 경고**: `LiberationSans.ttf.meta ... below the supported minimum` 경고는 TMP 패키지 자산에서 나오며 무해하다.
- **에디터 Code Optimization이 Debug**라서 에디터 안의 성능 수치는 실제 빌드보다 크다.
- **Unity 밖 검증 하네스**: 대량 검증과 Release 성능 측정에 쓴 .NET 9 콘솔 프로젝트는 세션 임시 폴더에 있었고 **저장소에 포함되지 않았다.** 다시 만들려면 `Assets/Scripts/VoxelRay/*.cs`와 `Assets/Tests/EditMode/`의 `ReferenceSupercover.cs`, `SupercoverOracle.cs`, `SupercoverChecks.cs`를 링크하고, `int3`/`double3`/`math`/`Vector3`를 흉내 낸 작은 대체 타입 파일을 더하면 된다. 진입점은 `SupercoverChecks.Run(category, count, seed)`다.

## 9. 미결정 사항 (사양 문서 8절, 손대지 않음)

사양 문서가 에이전트가 임의로 정하지 말라고 한 항목이다. 현재 코드는 이 중 어느 것도 가정하지 않는다.

- 복셀 형태 타겟의 자기 복셀 처리. 타겟이 지형과 같은 격자에 있으면 타겟 복셀을 가림이 아니라 "타겟 도달"로 봐야 한다.
- 시작점(총구)이 벽 복셀 안이나 표면에 있을 때 바로 가림으로 판정하는 것이 의도인지
- 유도탄 경로: 발사 가능 여부를 직선 시야로 판정할지
- 격자 범위 밖(로드되지 않은) 복셀을 빈 칸으로 볼지 막힌 칸으로 볼지
- 필터링 대상: 복셀 지형만인지, 캐릭터·오브젝트용 공간 분할 격자도 같은 순회를 쓰는지
- 서버·클라이언트 결과 일치(double 연산의 플랫폼 간 차이, 고정소수점 전환 여부)
- ε 값, 실행 위치, 성능 목표

## 10. 다음 작업 제안

1. **Job/Burst 버전**: `Traverse`는 struct 방문자, 할당 없음, 문자열 리터럴 예외만 쓰는 구조라 Burst로 옮기기 쉽게 짰지만 **Burst 컴파일은 검증하지 않았다.** `VoxelListCollector`는 관리 `List`를 들고 있어 Job에서 못 쓰므로 `NativeList<int3>` 방문자가 필요하다.
2. **플레이어 빌드 성능 측정**: Mono Release와 IL2CPP 각각에서 벤치마크를 돌려 실제 수치를 얻는다.
3. **실제 가림 방문자**: 프로젝트의 복셀 저장소(청크 조회)에 맞춘 `OcclusionVisitor`. 실제 비용은 순회보다 복셀 조회가 클 가능성이 높다.
4. **ε와 좌표 범위 재검토**: 복셀 크기와 월드 크기가 정해지면 청크 기준 원점 이동과 함께 정한다.
5. **이전 래스터라이저 정리**: `Rasterizer4.cs` 등을 남길지 지울지 결정한다.
