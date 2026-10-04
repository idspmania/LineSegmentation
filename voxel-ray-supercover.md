# 복셀 레이 Supercover 순회 — 구현 지침

> 이 문서는 Unity 프로젝트에서 AI 에이전트가 따르는 구현 지침이다.
> - 작성: 2026-10-04 · 근거: Force Masters Game Design 위키(vault 커밋 `5c2c7e3`)의 "레이캐스트 사전 필터링", "Amanatides–Woo 복셀 순회", "Line segmentation 알고리즘 비교" 페이지
> - 배경과 다른 알고리즘 비교: [algorithm-research.md](algorithm-research.md)
> - 표기: **[사용자 결정]** 사용자가 정한 요구 · **[출처]** 수집 자료에 있는 내용 · **[AI 설계]** 출처에 없는 AI 설계 · **[미결정]** 아직 정하지 않은 것. 미결정 항목은 에이전트가 임의로 정하지 말고 사용자에게 묻는다.

## 1. 목적

복셀에는 콜라이더가 없다. 그래서 타겟까지 레이캐스트하기 전에 레이가 복셀 블록에 가리는지를 복셀 격자에서 직접 판정한다. 이 판정에 쓰는 line segmentation(레이가 지나가거나 닿는 복셀을 모두 구하는 일)을 구현한다.

## 2. 요구 사항 — [사용자 결정]

- 레이는 볼륨이나 면적이 없는 순수한 선분이다.
- 레이가 꼭짓점, 모서리, 면에 **정확히 닿거나 그 위를 지나면**, 그 점을 공유하는 **인접 복셀 모두**를 결과에 넣는다.
- 누락은 허용하지 않는다. 사전 필터링이므로 후보가 조금 많아지는 것은 허용한다.

### 형식 정의

- 복셀 좌표계에서 복셀 `(i, j, k)`는 닫힌 상자 `[i, i+1] × [j, j+1] × [k, k+1]`다.
- 레이는 닫힌 선분 `P(t) = P0 + t·(P1 − P0)`, `t ∈ [0, 1]`이다.
- 결과 집합은 **선분과 한 점이라도 공유하는 모든 복셀**이다(closed supercover).

예: 레이가 3D 꼭짓점 하나를 정확히 지나면 그 꼭짓점을 공유하는 8개 복셀이 모두 들어간다. 모서리 위를 따라가면 그 모서리를 공유하는 4개, 면 위를 따라가면 양쪽 2개가 들어간다.

### 레이의 두 끝점 — [사용자 결정]

- **시작점 P0:** 캐릭터가 유도 원거리 에너지탄을 발사하는 위치(FPS의 총구 끝에 해당)
- **끝점 P1:** 타겟의 락온 포인트. 자동 락온이라 타겟마다 정해져 있다.
  - 사람형 캐릭터: 배꼽이나 명치 정도
  - 복셀 형태 타겟 오브젝트: 그 오브젝트의 중심 복셀의 중점

## 3. 채택할 알고리즘 — [AI 설계, 프로토타입 검증됨]

**이벤트 점 경계 상자 순회(Event-Box Supercover)**: Amanatides–Woo 순회를 개선한 방식이다.

1. Amanatides–Woo처럼 레이가 복셀 경계면(정수 좌표 평면)을 넘는 시점 t를 가까운 순서로 하나씩 구한다. 이 시점들을 **이벤트**라 한다.
2. 시작점, 각 이벤트 점, 끝점에서 **그 점이 닿는 모든 복셀**(점을 포함하는 닫힌 복셀)을 방문한다.
   - 점이 복셀 내부에 있으면 1개, 면 위면 2개, 모서리 위면 4개, 꼭짓점 위면 8개다.
3. 직전 점에서 이미 방문한 복셀은 건너뛴다.

원형 Amanatides–Woo는 이벤트마다 축 하나만 골라 한 칸씩 이동하므로, 꼭짓점·모서리를 정확히 지날 때 닿기만 하는 이웃과 경계면 반대편 복셀을 놓친다(위키 AI 분석). 이 방식은 동률 축 조합을 따로 나열하지 않는다. 대신 각 이벤트 점에서 "닿는 복셀 전체"를 방문하므로 꼭짓점, 모서리, 면 통과, 경계면 위를 따라가는 레이, 경계 위의 시작·끝점이 하나의 규칙으로 처리된다.

### 왜 누락이 없는가

- 이벤트 사이의 열린 구간에서는 방향 성분이 0이 아닌 축 모두 정수 경계에 닿지 않는다. 그래서 그 구간에서 레이가 닿는 복셀은 바뀌지 않는다.
- 각 축의 좌표는 구간 끝 이벤트 점에서 같은 칸 안이나 그 칸의 경계 위에 있다. 따라서 구간 안의 점이 닿는 복셀은 구간 끝 이벤트 점이 닿는 복셀에 모두 포함된다.
- 선분 위의 모든 점은 시작점, 이벤트 점, 끝점이거나 어떤 열린 구간 안에 있다. 그러므로 위의 점들에서 방문한 복셀의 합집합이 정확히 supercover다.
- 방향 성분이 0인 축은 값이 변하지 않는다. 경계면 위라면 모든 점에서 양쪽 복셀이 들어간다.

### 수치 처리

- 모든 순회 계산은 `double`로 한다.
- 이벤트 t는 누적하지 않고 매번 `(다음 경계 − P0) / d`로 다시 계산한다. 원형의 `tMax += tDelta` 누적은 긴 레이에서 오차가 쌓인다.
- 이벤트 점에서 경계를 넘는 축의 좌표는 정수 경계값으로 정확히 놓는다.
- 점이 닿는 복셀의 축별 범위는 `[ceil(x − ε) − 1, floor(x + ε)]`이다. ε 안의 거의 동률은 동률로 취급한다. 그래서 동시에 넘는 축이 부동소수점 오차로 어긋나도 이웃을 놓치지 않는다. ε을 크게 잡으면 후보가 늘 뿐 누락은 생기지 않는다.

### 검증 결과 (작성 시점)

- 정답 판정기: 닫힌 선분과 닫힌 상자의 교차를 유리수로 정확히 계산한다.
- Python 프로토타입 40,300건, 아래 C# 참조 구현 24,200건에서 정답과 **완전히 일치**했다(누락 0, 추가 0, 중복 방문 0).
- 입력 유형: 1/4 단위 격자 끝점(꼭짓점, 모서리, 면을 정확히 지나는 경우 다수), 정수 꼭짓점 끝점, 축에 평행한 레이, 경계면 위의 레이, 길이 0 레이, 무작위 실수 끝점, 좌표 ±1000 근처의 긴 레이.
- C# 검증은 `Unity.Mathematics`의 `int3`/`double3`를 흉내 낸 대체 타입으로 .NET 9에서 했다. **Unity 안에서 컴파일하고 실행하지는 않았다.** 프로젝트에 넣은 뒤 6절의 테스트로 다시 검증한다.

## 4. 의사코드

```
traverse(P0, P1):                    # 복셀 좌표, double
  d = P1 − P0
  visitBox(P0)
  for each axis a:
    if d[a] > 0: step[a] = +1; next[a] = floor(P0[a]) + 1
    if d[a] < 0: step[a] = −1; next[a] = ceil(P0[a]) − 1
    tNext[a] = d[a] ≠ 0 ? (next[a] − P0[a]) / d[a] : +∞
  loop:
    a = argmin(tNext)                # 동률이면 아무 축이나
    t = tNext[a]
    if t > 1: break
    P = P0 + t·d;  P[a] = next[a]    # 넘는 축은 경계 위에 정확히
    visitBox(P)
    next[a] += step[a]
    tNext[a] = (next[a] − P0[a]) / d[a]
  visitBox(P1)

visitBox(P):                         # P가 닿는 복셀 전부(1~8개)
  for each axis a: lo[a] = ceil(P[a] − ε) − 1;  hi[a] = floor(P[a] + ε)
  for each voxel v in [lo, hi]:
    if v not in previous box: visit(v)   # 방문자가 멈추라고 하면 즉시 종료
  previous box = [lo, hi]
```

중복 제거는 직전 상자와만 비교하면 된다. 한 복셀과 선분의 교집합은 볼록하다. 그래서 같은 복셀은 연속된 이벤트 점들에서만 나타난다. 이 덕분에 `HashSet` 없이 할당 없이 동작한다.

## 5. C# 참조 구현

`Unity.Mathematics`를 쓴다. Burst와 Job에서 쓸 수 있도록 제네릭 struct 방문자를 받고, 힙 할당이 없다.

```csharp
using System;
using Unity.Mathematics;

public interface IVoxelVisitor
{
    // true를 반환하면 순회를 즉시 멈춘다.
    bool Visit(int3 voxel);
}

public static class VoxelSupercover
{
    // 복셀 좌표 단위. 계산 오차보다 크게, 복셀 크기보다 훨씬 작게 잡는다.
    public const double Epsilon = 1e-9;

    // 닫힌 선분 p0–p1(복셀 좌표)과 한 점이라도 공유하는 모든 닫힌 복셀 [i, i+1]^3을
    // p0에서 p1 방향 순서로 방문한다. 방문자가 멈추면 true를 반환한다.
    public static bool Traverse<T>(double3 p0, double3 p1, ref T visitor) where T : struct, IVoxelVisitor
    {
        double3 d = p1 - p0;
        int3 prevLo = new int3(1, 1, 1), prevHi = new int3(0, 0, 0); // 빈 상자

        if (VisitBox(p0, ref prevLo, ref prevHi, ref visitor)) return true;

        int3 step = default, nextK = default;
        double3 tNext = new double3(double.PositiveInfinity);
        for (int a = 0; a < 3; a++)
        {
            if (d[a] > 0) { step[a] = 1; nextK[a] = (int)Math.Floor(p0[a]) + 1; }
            else if (d[a] < 0) { step[a] = -1; nextK[a] = (int)Math.Ceiling(p0[a]) - 1; }
            else continue;
            tNext[a] = (nextK[a] - p0[a]) / d[a];
        }

        while (true)
        {
            int a = tNext.x <= tNext.y ? (tNext.x <= tNext.z ? 0 : 2) : (tNext.y <= tNext.z ? 1 : 2);
            double t = tNext[a];
            if (t > 1.0) break;

            double3 p = p0 + t * d;
            p[a] = nextK[a]; // 넘는 축은 경계 위에 정확히 놓는다
            if (VisitBox(p, ref prevLo, ref prevHi, ref visitor)) return true;

            nextK[a] += step[a];
            tNext[a] = (nextK[a] - p0[a]) / d[a]; // 누적하지 않고 매번 다시 계산
        }

        return VisitBox(p1, ref prevLo, ref prevHi, ref visitor);
    }

    // 점 p가 닿는 모든 복셀(축마다 1~2개, 최대 8개)을 방문한다. 직전 상자에 있던 복셀은 건너뛴다.
    static bool VisitBox<T>(double3 p, ref int3 prevLo, ref int3 prevHi, ref T visitor) where T : struct, IVoxelVisitor
    {
        int3 lo = default, hi = default;
        for (int a = 0; a < 3; a++)
        {
            lo[a] = (int)Math.Ceiling(p[a] - Epsilon) - 1;
            hi[a] = (int)Math.Floor(p[a] + Epsilon);
        }

        for (int x = lo.x; x <= hi.x; x++)
        for (int y = lo.y; y <= hi.y; y++)
        for (int z = lo.z; z <= hi.z; z++)
        {
            bool inPrev = x >= prevLo.x && x <= prevHi.x
                       && y >= prevLo.y && y <= prevHi.y
                       && z >= prevLo.z && z <= prevHi.z;
            if (!inPrev && visitor.Visit(new int3(x, y, z))) return true;
        }

        prevLo = lo; prevHi = hi;
        return false;
    }
}
```

### 가림 판정에 쓰는 예

```csharp
// VoxelWorld와 IsSolid는 자리표시자다. 프로젝트의 복셀 저장소에 맞춘다. [미결정]
struct OcclusionVisitor : IVoxelVisitor
{
    public VoxelWorld World;
    public bool Blocked;
    public int3 HitVoxel;

    public bool Visit(int3 v)
    {
        if (!World.IsSolid(v)) return false;
        Blocked = true; HitVoxel = v;
        return true; // 첫 번째 꽉 찬 복셀에서 멈춘다
    }
}

// 월드 좌표 → 복셀 좌표. float를 double로 바꾼 뒤 빼고 나눈다.
static double3 ToVoxel(Vector3 world, double3 gridOrigin, double voxelSize)
    => (new double3(world.x, world.y, world.z) - gridOrigin) / voxelSize;

var visitor = new OcclusionVisitor { World = world };
VoxelSupercover.Traverse(ToVoxel(muzzlePoint, origin, size), ToVoxel(lockOnPoint, origin, size), ref visitor);
if (!visitor.Blocked) { /* 복셀에 가리지 않음 → 캐릭터·오브젝트 콜라이더 레이캐스트 진행 */ }
```

- 방문 순서는 시작점에서 끝점 방향이다. 첫 번째 꽉 찬 복셀에서 멈추면 가장 가까운 가림 복셀을 얻는다. 한 이벤트 점의 상자(최대 8개) 안에서는 순서가 정해져 있지 않다.
- supercover는 레이와 복셀 경계면이 닿는 정도로 정해진다. 따라서 이 결과는 "레이 위치가 복셀 좌표로 바뀐 그 선분"에 대해 정확하다. 월드→복셀 변환 오차는 변환에서 생기는 것이다. 좌표가 커지면 원점을 가까운 청크 기준으로 옮긴 뒤 변환한다.

## 6. 에이전트 구현 규칙

반드시:

- 2절의 형식 정의(닫힌 선분과 닫힌 복셀이 한 점이라도 공유)를 기준으로 구현하고 테스트한다.
- 순회 계산은 `double`로 한다. `float`(`Vector3`)는 입력을 받을 때만 쓴다.
- 이벤트 t를 매번 다시 계산하고, 넘는 축의 좌표를 정수 경계값으로 놓는다.
- 방향 성분이 0인 축은 t를 +∞로 둔다. 0으로 나누지 않는다.
- 아래 테스트를 만들고, 알고리즘을 바꿀 때마다 통과시킨다.

하지 말 것:

- 원형 Amanatides–Woo(`<` 비교로 축 하나만 이동하고 그 칸만 방문)를 그대로 쓰지 않는다.
- 최대 방문 수 상한(예: `MAX_STEPS = 128`)으로 레이를 끊지 않는다. 종료 조건은 `t > 1`이다.
- 시작점을 `0.0001`만큼 안쪽으로 밀거나, 격자 범위로 clamp해서 경계에 닿는 복셀을 버리지 않는다.
- 기본 Bresenham, DDA, 두께 선(Zingl의 anti-aliased thick line) 방식으로 바꾸지 않는다. 이유는 [algorithm-research.md](algorithm-research.md)에 있다.
- 성능을 이유로 ε, 상자 방문, 중복 제거 규칙을 바꾸지 않는다. 바꾸려면 테스트를 통과하고 사용자에게 알린다.
- MΛX 글의 코드를 복사하지 않는다(사이트에 "All rights reserved" 표기). 논문과 이 문서를 참고해 직접 구현한다.

## 7. 필수 테스트

**정답 판정기(oracle):** 선분 주변(끝점 경계 상자를 1칸씩 넓힌 범위)의 모든 복셀에 대해 닫힌 선분–닫힌 상자 교차를 직접 계산한다.

- 축마다 `d = 0`이면 `P0[a] ∈ [v, v+1]`인지 본다.
- `d ≠ 0`이면 `t` 구간 `[(v − P0)/d, (v + 1 − P0)/d]`(정렬)을 `[0, 1]`과 교차시킨다. 경계 비교는 모두 닫힌 비교(`≤`, `≥`)로 한다.
- 꼭짓점·모서리 경우를 정확히 판정하려면, 테스트 입력을 1/4이나 1/16 단위 좌표로 만든다. 그리고 판정기를 정수로 스케일한 유리수 비교(`long` 교차곱)로 구현한다. `double` 나눗셈을 쓰면 동률 판정이 흔들린다.

**입력 유형**(모두 포함):

1. 1/4 단위 무작위 끝점. 꼭짓점, 모서리, 면을 정확히 지나는 경우가 많이 생긴다.
2. 정수 끝점(꼭짓점에서 시작해 꼭짓점에서 끝남)
3. 한 축이나 두 축 성분이 0인 레이. 경계면 위나 모서리 위를 따라가는 레이를 포함한다.
4. 길이 0 레이. 내부, 면, 모서리, 꼭짓점 위의 점.
5. 무작위 실수 끝점
6. 원점에서 먼 좌표(±1000 이상)의 긴 레이(오차 누적 확인)
7. 음수 좌표와 음의 방향

**합격 기준:**

- 모든 입력에서 누락 0
- 1~4, 6(정확히 표현되는 좌표)에서는 정답과 완전히 일치(추가 0)
- 같은 복셀을 두 번 방문하지 않음
- 방문 순서: 첫 방문 시점의 t가 감소하지 않음(같은 상자 안의 순서는 무관)

## 8. 미결정 사항 — 에이전트가 임의로 정하지 않는다

- **복셀 형태 타겟의 자기 복셀:** 락온 포인트가 타겟의 중심 복셀 중점이면, 그 복셀은 꽉 찬 복셀이다. 레이는 타겟 바깥쪽 복셀을 지나 중심에 도달한다. 이 복셀들을 가림으로 판정하면 복셀 타겟은 항상 가려진다. 처리 방식은 타겟 복셀이 어디에 저장되는지에 따라 다르다. 정해질 때까지 임의로 구현하지 않는다.
  - 지형과 별도의 격자(자체 Transform)를 가진 오브젝트라면, 지형 격자 순회에는 나타나지 않아 문제가 없다.
  - 지형과 같은 격자에 들어 있다면, 타겟 오브젝트에 속한 복셀은 가림이 아니라 "타겟 도달"로 처리해야 한다. 그러려면 복셀마다 소속 오브젝트를 알 수 있어야 한다.
- **시작점이 복셀 안이나 표면에 있는 경우:** 캐릭터가 벽에 붙어 총구가 벽 복셀 안이나 표면에 들어가면 시작점에서 바로 가림으로 판정된다. 벽 너머로 쏘지 못하게 하는 동작으로 보이지만(AI 해석) 의도대로인지 확인한다. 사람형 타겟의 락온 포인트(배꼽·명치)는 보통 공중에 있어 문제가 되지 않는다.
- **유도탄 경로:** 이 판정은 총구에서 락온 포인트까지의 직선 시야다. 유도 에너지탄의 실제 비행 경로는 곡선이다. 비행 중 지형 충돌은 프레임마다 이동한 선분(이전 위치→현재 위치)에 같은 순회를 적용해 판정할 수 있다(AI 제안). 발사 가능 여부를 직선 시야로 판정할지는 게임 디자인 결정이다.
- **격자 범위 밖:** 로드되지 않았거나 월드 밖인 복셀을 빈 칸으로 볼지, 막힌 칸으로 볼지
- **필터링 대상:** 복셀 지형만인지, 캐릭터·오브젝트를 담은 공간 분할 격자도 같은 순회를 쓰는지
- **서버·클라이언트 결과 일치:** `double` 연산은 플랫폼 간 비트 단위로 같다는 보장이 없다. 서버 판정을 기준으로 할지, 좌표를 고정소수점 정수로 다룰지 정해야 한다. 고정소수점으로 바꾸면 이벤트 비교를 정수 교차곱으로 정확히 할 수 있다(AI 제안, 미구현).
- **ε 값:** `1e-9`(복셀 단위)는 검증에 쓴 값이다. 복셀 크기와 좌표 범위가 정해지면 다시 본다.
- **실행 위치, 성능 목표, Unity 버전:** 정해지지 않았다.

## 9. 근거 자료

- John Amanatides, Andrew Woo, "A Fast Voxel Traversal Algorithm for Ray Tracing", Eurographics 1987 — http://www.cse.yorku.ca/~amana/research/grid.pdf (라이선스 표기 없음)
- Max(mxcop), "Amanatides and Woo's fast Voxel Traversal" — https://m4xc.dev/articles/amanatides-and-woo/ (코드 복사 금지)
- 이벤트 점 경계 상자 방식, 정당성 논증, 검증 결과는 출처에 없는 AI 설계와 AI 검증이다. 위 두 자료는 이벤트 순회의 근거다.
