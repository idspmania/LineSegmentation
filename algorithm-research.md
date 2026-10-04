# 복셀 레이 line segmentation 알고리즘 조사

> [voxel-ray-supercover.md](voxel-ray-supercover.md)의 배경 자료다. 구현 규칙은 그 문서를 따른다.
> - 작성: 2026-10-04 · 근거: Force Masters Game Design 위키(vault 커밋 `5c2c7e3`)의 출처 요약 7개, 개념·시스템·종합 페이지, Zingl 페이지 원본
> - 표기: **[출처]** 수집 자료에 있는 내용 · **[AI 분석]** 출처에 없는 AI 해석 · **[사용자 결정]**

## 결론

**제안 "Zingl의 Anti-aliased thick line보다 Amanatides–Woo를 개선하는 편이 더 확실하다"에 동의한다.** [AI 분석]

- Amanatides–Woo는 실수 끝점의 3D 레이를 그대로 받는다. 레이 순서대로 경계를 넘는 시점을 정확히 구하므로 "레이가 닿는 복셀"을 기하학적으로 정확하게 정의할 수 있다.
- Zingl 두께 선은 2D 정수 끝점 그리기 알고리즘이다. 닿는 복셀을 포함하려면 두께를 키워야 하고, 그러면 닿지 않는 복셀까지 대량으로 들어간다. 3D 버전도 없다.
- 개선 방식으로는 **이벤트 점 경계 상자 순회**를 채택한다. 원형 Amanatides–Woo의 이벤트 순회를 그대로 쓰되, 각 이벤트 점에서 그 점이 닿는 복셀 전부를 방문한다. 정확한 정답 판정기와 비교해 누락 0, 추가 0을 확인했다(검증 상세는 구현 지침 3절).

## 요구 사항 — [사용자 결정]

- 레이에는 두께가 없다. 레이가 지나가거나 닿기만 하는 모든 복셀을 체크한다. 꼭짓점, 모서리, 면에 정확히 닿으면 인접 복셀이 모두 포함되어야 한다.
- 사전 필터링이므로 누락은 허용하지 않고, 후보가 조금 많아지는 것은 허용한다.

## 후보 비교

| 방식 | 차원 | 끝점 | 닿는 복셀 모두 포함 | 판단 |
|---|---|---|---|---|
| 기본 Bresenham(2D·3D) | 2D·3D | 정수 | ❌ 한 단계에 대각선으로 이동해, 선이 실제로 통과하는 칸도 건너뛴다 | 제외 |
| DDA, 서브픽셀 DDA·Bresenham | 2D | 실수 가능 | ❌ 주축 한 칸 단위 샘플링이라 누락 가능 | 제외 |
| Zingl anti-aliased thick line | 2D | 정수 | △ 두께를 충분히 키우면 포함은 가능. 후보가 과도하고 3D 버전이 없음 | 제외 |
| Amanatides–Woo 원형 | 3D | 실수 | △ 내부를 통과하는 복셀은 모두 방문. 꼭짓점·모서리·경계면에 닿기만 하는 복셀은 누락 | 단독 사용 불가 |
| Amanatides–Woo + 동률 축 조합 확장(위키의 이전 AI 설계) | 3D | 실수 | ✅ 설계상 충족, 미검증 | 아래 방식으로 대체 |
| **Amanatides–Woo 이벤트 + 이벤트 점 경계 상자** | 3D | 실수 | ✅ 정답 판정기와 완전 일치(검증) | **채택** |

## Zingl anti-aliased thick line을 쓰지 않는 이유

[출처] Alois Zingl의 페이지에 있는 `plotLineWidth(int x0, int y0, int x1, int y1, float wd)`는 정수 끝점 2D 선을 `wd` 픽셀 두께로 그린다. 오차 누적값을 선 길이로 나눠 각 픽셀과 선 사이의 거리를 근사하고, 그 거리로 안티앨리어싱 밝기를 정한다. 같은 페이지의 3D 예제는 두께나 안티앨리어싱이 없는 기본 3D Bresenham뿐이다.

[AI 분석]

- **차원:** 3D 복셀에 쓰려면 알고리즘을 직접 3D로 일반화해야 한다. 출처에 없는 설계라 검증 부담이 Amanatides–Woo 개선보다 크다.
- **끝점:** 정수 끝점 전제다. 실제 레이의 시작점과 타겟은 실수 좌표다.
- **포함 조건이 거리 근사다:** "선과 한 점이라도 공유하는 복셀"을 거리로 보장하려면, 복셀 중심과 선의 거리 기준을 복셀 대각선의 절반(3D에서 √3/2 ≈ 0.87칸) 이상으로 잡아야 한다. 그러면 선에 닿지 않는 복셀까지 많이 포함된다. 거리 근사와 픽셀 단위 루프 종료 조건 때문에 경계 사례에서 누락이 없다고 증명하기도 어렵다.
- **불필요한 출력:** 안티앨리어싱 밝기(커버리지)는 가림 판정에 필요 없다.

## Amanatides–Woo 원형

[출처] 논문(1987)과 MΛX 해설 기준:

- 레이 `u + t·v`를 복셀 하나씩 걸치는 t 구간으로 나눠 레이 순서대로 방문한다. 우선 축이 없다.
- 초기화: 시작 복셀, 축별 `step`(±1), 다음 경계를 넘는 t인 `tMax`, 한 칸 폭의 t인 `tDelta`
- 루프: `tMax`가 가장 작은 축으로 한 칸 이동하고 `tMax += tDelta`. 이웃으로 넘어갈 때 실수 비교 2번과 덧셈 1번이 든다.
- 원래 목적은 정밀 교차 판정 대상을 줄이는 intersection culling이다. 사전 필터링과 용도가 같다.
- 논문은 정수 연산 버전을 향후 과제로만 언급한다.

[AI 분석] 요구 사항에 비춘 한계:

- `<` 비교로 축 하나만 고른다. 레이가 3D 모서리나 꼭짓점을 정확히 지나면 그 점에만 닿는 이웃 복셀을 방문하지 않는다.
- 시작·끝점이 경계 위에 있거나 레이가 경계면을 따라가면 반대편 복셀을 방문하지 않는다.
- `tMax += tDelta` 누적은 긴 레이에서 오차가 쌓인다.
- MΛX 구현의 추가 문제: 방문 수 상한 `MAX_STEPS = 128`, 방향 성분이 0일 때 0/0 = NaN 가능성, 진입점 `0.0001` 보정과 clamp로 인한 경계 복셀 누락, 첫 꽉 찬 복셀에서 반환하는 구조

## 위키의 이전 확장 설계에서 바꾼 점 — [AI 분석]

위키에는 "동시에 넘는 축 집합 T의 공집합이 아닌 모든 부분집합 S에 대해 이웃을 방문"하는 확장이 있었다. 시작점의 경계 축 B와 경계면 위 축 Z를 따로 처리하는 방식이다. 이번 문서는 이를 **이벤트 점 경계 상자**로 바꿨다.

- 동률 축 부분집합, 시작 경계 축, 경계면 위 축, 끝점 경계를 하나의 규칙("점이 닿는 복셀 전부")으로 처리한다. 특수 경우 분기가 없어 구현 실수가 줄어든다.
- 이전 설계는 시작점 이웃을 `floor(P0) − 1` 쪽으로만 더했다. 그래서 좌표 변환 오차로 경계 바로 아래(예: 2.9999999)에 놓인 점에서는 반대쪽을 더한다. 새 방식은 축별 범위를 `[ceil(x − ε) − 1, floor(x + ε)]`로 잡아 양쪽을 대칭으로 처리한다.
- 이벤트 t를 누적하지 않고 다시 계산해 긴 레이의 오차 누적을 없앴다.
- 정당성 논증이 짧다: 이벤트 사이 구간이 닿는 복셀은 구간 끝 이벤트 점이 닿는 복셀에 포함된다.
- 이전 설계는 검증되지 않았다. 새 방식은 정답 판정기와 비교 검증했다.

## 아직 조사하지 않은 것

- supercover를 다룬 외부 자료(Red Blob Games의 2D supercover 글, Eugen Dedu의 Bresenham 기반 supercover)는 위키에 수집되지 않았다.
- 정수·고정소수점 버전(서버·클라이언트 결과 일치용)은 조사하지 않았다.
- 실제 Unity 프로젝트에서의 성능 측정은 하지 않았다.

## 출처

| 자료 | URL | 라이선스·사용 |
|---|---|---|
| Amanatides, Woo, "A Fast Voxel Traversal Algorithm for Ray Tracing" (1987) | http://www.cse.yorku.ca/~amana/research/grid.pdf | 표기 없음. 알고리즘만 참고 |
| Max(mxcop), "Amanatides and Woo's fast Voxel Traversal" | https://m4xc.dev/articles/amanatides-and-woo/ | "All rights reserved"(AI 별도 확인). 코드 복사 금지 |
| Alois Zingl, "The Beauty of Bresenham's Algorithm" | http://members.chello.at/~easyfilter/bresenham.html | "Copyright © Alois Zingl", 코드 사용 조건 표기 없음 |
| Wikipedia, "Bresenham's line algorithm" | https://en.wikipedia.org/wiki/Bresenham%27s_line_algorithm | 클리핑에 표기 없음 |
| badlogic, line-rasterization `algorithms.js` | https://github.com/badlogic/line-rasterization/blob/main/algorithms.js | 표기 없음 |
| Robótica, "Bresenham 3D" | https://sites.google.com/site/proyectosroboticos/bresenham/bresenham-3d | 저자·라이선스 표기 없음 |
| Kyle Sloka-Frey, "Let's Build a 3D Graphics Engine: Rasterizing Line Segments and Circles" (Tuts+, 2013) | https://code.tutsplus.com/lets-build-a-3d-graphics-engine-rasterizing-line-segments-and-circles--gamedev-8414t | 표기 없음. 수집된 코드 손상 |
