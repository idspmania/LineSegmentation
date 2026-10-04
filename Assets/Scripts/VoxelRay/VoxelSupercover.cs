using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelRay
{
    public interface IVoxelVisitor
    {
        // true를 반환하면 순회를 즉시 멈춘다.
        bool Visit(int3 voxel);
    }

    // 이벤트 점 경계 상자 순회(Event-Box Supercover). Docs/voxel-ray-supercover.md 4~5절 구현.
    //
    // 문서의 참조 구현과 방문 순서까지 같은 결과를 낸다(테스트로 확인). 바꾼 점:
    // - 비유한값(NaN/∞)이나 int 범위를 넘는 좌표는 ArgumentException. 참조 구현은 ∞ 끝점에서
    //   무한 루프에 빠지고(t가 계속 0), NaN 끝점에서 엉뚱한 복셀을 방문한다.
    // - int3/double3 인덱서 대신 축별 스칼라 변수(인덱서는 fixed 포인터 + 범위 검사).
    // - 넘는 축의 범위는 경계값 k에서 바로 [k-1, k]. ceil(k-ε)-1 = k-1, floor(k+ε) = k와 같다.
    // - 방향 성분이 0인 축의 범위는 한 번만 계산한다(p0 + t·0 = p0).
    // - floor/ceil은 범위 검사를 거친 값에 대한 절삭 변환으로 계산한다.
    // - 직전 상자 포함 검사를 축별로 끌어올렸고, 넘는 축만 2칸인 흔한 상자는 루프 없이 방문한다.
    // ε, 상자 방문, 직전 상자 중복 제거 규칙은 문서 그대로다.
    public static class VoxelSupercover
    {
        // 복셀 좌표 단위. 계산 오차보다 크게, 복셀 크기보다 훨씬 작게 잡는다.
        public const double Epsilon = 1e-9;

        // 복셀 인덱스(±1 이웃 포함)가 int에 들어가는 좌표 한계. 정밀도 한계가 아니다:
        // ε = 1e-9가 의미 있으려면 좌표는 대략 ±1e5 안이어야 한다(문서 5절, 8절).
        public const double MaxCoordinate = 1 << 30;

        // 닫힌 선분 p0–p1(복셀 좌표)과 한 점이라도 공유하는 모든 닫힌 복셀 [i, i+1]^3을
        // p0에서 p1 방향 순서로 방문한다. 방문자가 멈추면 true를 반환한다.
        public static bool Traverse<T>(double3 p0, double3 p1, ref T visitor) where T : struct, IVoxelVisitor
        {
            if (!InRange(p0) || !InRange(p1))
                throw new ArgumentException("Segment endpoints must be finite and within ±MaxCoordinate voxels.");

            double ox = p0.x, oy = p0.y, oz = p0.z;
            double dx = p1.x - ox, dy = p1.y - oy, dz = p1.z - oz;

            // 축별 이벤트 상태: step, 다음 경계 k, 그 경계를 넘는 t. d = 0인 축은 t = +∞로 두고 나누지 않는다.
            int sx, kx, sy, ky, sz, kz;
            double tx, ty, tz;
            if (dx > 0) { sx = 1; kx = FloorToInt(ox) + 1; tx = (kx - ox) / dx; }
            else if (dx < 0) { sx = -1; kx = CeilToInt(ox) - 1; tx = (kx - ox) / dx; }
            else { sx = 0; kx = 0; tx = double.PositiveInfinity; }
            if (dy > 0) { sy = 1; ky = FloorToInt(oy) + 1; ty = (ky - oy) / dy; }
            else if (dy < 0) { sy = -1; ky = CeilToInt(oy) - 1; ty = (ky - oy) / dy; }
            else { sy = 0; ky = 0; ty = double.PositiveInfinity; }
            if (dz > 0) { sz = 1; kz = FloorToInt(oz) + 1; tz = (kz - oz) / dz; }
            else if (dz < 0) { sz = -1; kz = CeilToInt(oz) - 1; tz = (kz - oz) / dz; }
            else { sz = 0; kz = 0; tz = double.PositiveInfinity; }

            Box cur;
            cur.lx = Lo(ox); cur.hx = Hi(ox);
            cur.ly = Lo(oy); cur.hy = Hi(oy);
            cur.lz = Lo(oz); cur.hz = Hi(oz);

            // 방향 성분이 0인 축은 모든 점에서 시작점과 같은 범위다.
            int cly = cur.ly, chy = cur.hy, clz = cur.lz, chz = cur.hz, clx = cur.lx, chx = cur.hx;

            Box prev = Box.Empty;
            if (VisitBox(ref cur, ref prev, ref visitor)) return true;

            // 이벤트마다 넘는 축은 [k−1, k] 두 칸이고, 다른 두 축은 대개 한 칸이다. 그 흔한 경우는
            // VisitPair로 루프 없이 방문한다(VisitBox와 같은 순서, 같은 결과).
            while (true)
            {
                // argmin(tNext). 동률이면 x, y, z 순(참조 구현과 같은 선택).
                if (tx <= ty && tx <= tz)
                {
                    double t = tx;
                    if (t > 1.0) break;
                    cur.lx = kx - 1; cur.hx = kx; // 넘는 축은 경계 위에 정확히 있다
                    if (sy != 0) { double y = oy + t * dy; cur.ly = Lo(y); cur.hy = Hi(y); } else { cur.ly = cly; cur.hy = chy; }
                    if (sz != 0) { double z = oz + t * dz; cur.lz = Lo(z); cur.hz = Hi(z); } else { cur.lz = clz; cur.hz = chz; }
                    if (cur.ly == cur.hy && cur.lz == cur.hz)
                    {
                        if (VisitPair(new int3(kx - 1, cur.ly, cur.lz), new int3(kx, cur.ly, cur.lz), ref cur, ref prev, ref visitor)) return true;
                    }
                    else if (VisitBox(ref cur, ref prev, ref visitor)) return true;
                    kx += sx;
                    tx = (kx - ox) / dx; // 누적하지 않고 매번 다시 계산
                }
                else if (ty <= tz)
                {
                    double t = ty;
                    if (t > 1.0) break;
                    cur.ly = ky - 1; cur.hy = ky;
                    if (sx != 0) { double x = ox + t * dx; cur.lx = Lo(x); cur.hx = Hi(x); } else { cur.lx = clx; cur.hx = chx; }
                    if (sz != 0) { double z = oz + t * dz; cur.lz = Lo(z); cur.hz = Hi(z); } else { cur.lz = clz; cur.hz = chz; }
                    if (cur.lx == cur.hx && cur.lz == cur.hz)
                    {
                        if (VisitPair(new int3(cur.lx, ky - 1, cur.lz), new int3(cur.lx, ky, cur.lz), ref cur, ref prev, ref visitor)) return true;
                    }
                    else if (VisitBox(ref cur, ref prev, ref visitor)) return true;
                    ky += sy;
                    ty = (ky - oy) / dy;
                }
                else
                {
                    double t = tz;
                    if (t > 1.0) break;
                    cur.lz = kz - 1; cur.hz = kz;
                    if (sx != 0) { double x = ox + t * dx; cur.lx = Lo(x); cur.hx = Hi(x); } else { cur.lx = clx; cur.hx = chx; }
                    if (sy != 0) { double y = oy + t * dy; cur.ly = Lo(y); cur.hy = Hi(y); } else { cur.ly = cly; cur.hy = chy; }
                    if (cur.lx == cur.hx && cur.ly == cur.hy)
                    {
                        if (VisitPair(new int3(cur.lx, cur.ly, kz - 1), new int3(cur.lx, cur.ly, kz), ref cur, ref prev, ref visitor)) return true;
                    }
                    else if (VisitBox(ref cur, ref prev, ref visitor)) return true;
                    kz += sz;
                    tz = (kz - oz) / dz;
                }
            }

            cur.lx = Lo(p1.x); cur.hx = Hi(p1.x);
            cur.ly = Lo(p1.y); cur.hy = Hi(p1.y);
            cur.lz = Lo(p1.z); cur.hz = Hi(p1.z);
            return VisitBox(ref cur, ref prev, ref visitor);
        }

        // 월드 좌표 → 복셀 좌표. float를 double로 바꾼 뒤 빼고 나눈다.
        public static double3 WorldToVoxel(UnityEngine.Vector3 world, double3 gridOrigin, double voxelSize)
            => (new double3(world.x, world.y, world.z) - gridOrigin) / voxelSize;

        // 축별 점이 닿는 칸 범위 [ceil(x − ε) − 1, floor(x + ε)].
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Lo(double x) => CeilToInt(x - Epsilon) - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Hi(double x) => FloorToInt(x + Epsilon);

        // |x| < 2^31에서 (int)Math.Floor / (int)Math.Ceiling과 같다. 호출부의 값은 InRange로 보장된다.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FloorToInt(double x) { int i = (int)x; return x < i ? i - 1 : i; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CeilToInt(double x) { int i = (int)x; return x > i ? i + 1 : i; }

        // NaN은 모든 비교가 false라서 여기서 걸러진다.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool InRange(double3 p)
            => p.x >= -MaxCoordinate && p.x <= MaxCoordinate
            && p.y >= -MaxCoordinate && p.y <= MaxCoordinate
            && p.z >= -MaxCoordinate && p.z <= MaxCoordinate;

        // 점이 닿는 복셀 상자(축마다 1~2칸, 최대 8개) 중 직전 상자에 없던 것을 방문한다.
        // 한 복셀과 선분의 교집합은 볼록하므로 직전 상자와만 비교하면 중복이 없다.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool VisitBox<T>(ref Box cur, ref Box prev, ref T visitor) where T : struct, IVoxelVisitor
        {
            for (int x = cur.lx; x <= cur.hx; x++)
            {
                bool inX = x >= prev.lx && x <= prev.hx;
                for (int y = cur.ly; y <= cur.hy; y++)
                {
                    bool inXY = inX && y >= prev.ly && y <= prev.hy;
                    for (int z = cur.lz; z <= cur.hz; z++)
                    {
                        if (inXY && z >= prev.lz && z <= prev.hz) continue;
                        if (visitor.Visit(new int3(x, y, z))) return true;
                    }
                }
            }
            prev = cur;
            return false;
        }

        // 한 축으로만 두 칸인 상자 cur = {a, b} (a가 루프 순서상 먼저). VisitBox의 특수화.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool VisitPair<T>(int3 a, int3 b, ref Box cur, ref Box prev, ref T visitor) where T : struct, IVoxelVisitor
        {
            if (!prev.Contains(a) && visitor.Visit(a)) return true;
            if (!prev.Contains(b) && visitor.Visit(b)) return true;
            prev = cur;
            return false;
        }

        struct Box
        {
            public int lx, hx, ly, hy, lz, hz;

            // lo > hi인 빈 상자
            public static Box Empty => new Box { lx = 1, hx = 0, ly = 1, hy = 0, lz = 1, hz = 0 };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(int3 v)
                => v.x >= lx && v.x <= hx && v.y >= ly && v.y <= hy && v.z >= lz && v.z <= hz;
        }
    }
}
