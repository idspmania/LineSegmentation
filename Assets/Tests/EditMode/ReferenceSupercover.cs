using System;
using Unity.Mathematics;

namespace VoxelRay.Tests
{
    // Docs/voxel-ray-supercover.md 5절의 C# 참조 구현을 그대로 옮겼다(클래스 이름과 네임스페이스만 다름).
    // 최적화 구현이 같은 순서로 같은 복셀을 방문하는지 비교하고, 성능을 견주는 기준으로만 쓴다.
    public static class ReferenceSupercover
    {
        public const double Epsilon = 1e-9;

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
}
