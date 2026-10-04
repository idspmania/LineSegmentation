using System;
using System.Collections.Generic;
using System.Numerics;
using Unity.Mathematics;

namespace VoxelRay.Tests
{
    // 정답 판정기(문서 7절). 닫힌 선분과 닫힌 복셀 [v, v+1]^3의 교차를 유리수로 정확히 계산한다.
    // double은 이진 유리수이므로 끝점을 공통 2^s 배율의 정수로 바꾸면 모든 비교가 정수 교차곱이 된다.
    // 1/4 격자 입력뿐 아니라 임의의 double 입력도 정확하다.
    public static class SupercoverOracle
    {
        // 정확한 유리수 num/den (den > 0)
        public readonly struct Fraction
        {
            public readonly BigInteger Num, Den;
            public Fraction(BigInteger num, BigInteger den) { Num = num; Den = den; }
            public static bool LessOrEqual(in Fraction a, in Fraction b) => a.Num * b.Den <= b.Num * a.Den;
            public override string ToString() => $"{Num}/{Den} (~{(double)Num / (double)Den:R})";
        }

        // 선분과 한 점이라도 공유하는 모든 복셀과, 그 복셀에 처음 닿는 t(정확한 값).
        public static Dictionary<int3, Fraction> Solve(double3 p0, double3 p1)
        {
            Decompose(p0.x, out var m0x, out var e0x); Decompose(p0.y, out var m0y, out var e0y); Decompose(p0.z, out var m0z, out var e0z);
            Decompose(p1.x, out var m1x, out var e1x); Decompose(p1.y, out var m1y, out var e1y); Decompose(p1.z, out var m1z, out var e1z);
            int s = Math.Max(0, -Math.Min(Math.Min(Math.Min(e0x, e0y), Math.Min(e0z, e1x)), Math.Min(e1y, e1z)));

            var a0 = new[] { m0x << (e0x + s), m0y << (e0y + s), m0z << (e0z + s) };
            var a1 = new[] { m1x << (e1x + s), m1y << (e1y + s), m1z << (e1z + s) };
            var dd = new[] { a1[0] - a0[0], a1[1] - a0[1], a1[2] - a0[2] };
            BigInteger scale = BigInteger.One << s;

            var result = new Dictionary<int3, Fraction>();
            foreach (int3 v in Candidates(p0, p1))
            {
                if (Intersect(v, a0, dd, scale, out Fraction tFirst))
                    result.Add(v, tFirst);
            }
            return result;
        }

        static bool Intersect(int3 v, BigInteger[] a0, BigInteger[] d, BigInteger scale, out Fraction tFirst)
        {
            // t 구간을 [0, 1]에서 시작해 축마다 좁힌다. 모든 경계 비교는 닫힌 비교.
            BigInteger ln = 0, ld = 1, hn = 1, hd = 1;
            for (int a = 0; a < 3; a++)
            {
                BigInteger lo = v[a] * scale, hi = (v[a] + 1) * scale;
                if (d[a].IsZero)
                {
                    if (a0[a] < lo || a0[a] > hi) { tFirst = default; return false; }
                    continue;
                }

                BigInteger n0, n1, den;
                if (d[a].Sign > 0) { n0 = lo - a0[a]; n1 = hi - a0[a]; den = d[a]; }
                else { n0 = a0[a] - hi; n1 = a0[a] - lo; den = -d[a]; }

                if (n0 * ld > ln * den) { ln = n0; ld = den; }
                if (n1 * hd < hn * den) { hn = n1; hd = den; }
            }

            tFirst = new Fraction(ln, ld);
            return ln * hd <= hn * ld;
        }

        // 정답 복셀을 빠짐없이 포함하는 후보 집합. 선분을 축별 간격 0.5 이하로 표본화하고,
        // 각 표본에서 ±0.25(+여유) 안의 좌표를 포함할 수 있는 복셀을 모두 넣는다.
        // 후보 생성은 순회 알고리즘과 독립이다(ε도, 이벤트도 쓰지 않는다).
        static HashSet<int3> Candidates(double3 p0, double3 p1)
        {
            double3 d = p1 - p0;
            double maxAbs = math.cmax(math.abs(d));
            int n = Math.Max(1, (int)Math.Ceiling(maxAbs / 0.5));
            const double r = 0.25 + 1e-6;

            var set = new HashSet<int3>();
            for (int i = 0; i <= n; i++)
            {
                double3 c = p0 + d * (i / (double)n);
                int3 lo = new int3((int)Math.Ceiling(c.x - r - 1), (int)Math.Ceiling(c.y - r - 1), (int)Math.Ceiling(c.z - r - 1));
                int3 hi = new int3((int)Math.Floor(c.x + r), (int)Math.Floor(c.y + r), (int)Math.Floor(c.z + r));
                for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                for (int z = lo.z; z <= hi.z; z++)
                    set.Add(new int3(x, y, z));
            }
            return set;
        }

        // v = mantissa · 2^exponent (정확)
        static void Decompose(double v, out BigInteger mantissa, out int exponent)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("finite input required");
            long bits = BitConverter.DoubleToInt64Bits(v);
            bool negative = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long mant = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else mant |= 1L << 52;
            exp -= 1075;
            if (mant == 0) { mantissa = BigInteger.Zero; exponent = 0; return; }
            while ((mant & 1) == 0) { mant >>= 1; exp++; }
            mantissa = negative ? -mant : mant;
            exponent = exp;
        }
    }
}
