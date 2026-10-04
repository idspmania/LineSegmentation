using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;

namespace VoxelRay.Tests
{
    // 문서 7절의 입력 유형 생성기와 합격 기준 검사. NUnit 테스트와 오프라인 하네스가 함께 쓴다.
    public static class SupercoverChecks
    {
        public enum Category
        {
            QuarterGrid,       // 1. 1/4 단위 무작위 끝점
            IntegerEndpoints,  // 2. 정수 끝점(꼭짓점 → 꼭짓점)
            AxisParallel,      // 3. 한 축이나 두 축 성분이 0(경계면·모서리 위를 따라가는 레이 포함)
            ZeroLength,        // 4. 길이 0(내부, 면, 모서리, 꼭짓점)
            RandomReal,        // 5. 무작위 실수 끝점
            FarLong,           // 6. 원점에서 먼(±1000 이상) 긴 레이, 1/4 단위
            FarLongReal,       // 6'. 원점에서 먼 긴 레이, 무작위 실수
            Negative,          // 7. 음수 좌표와 음의 방향
        }

        // 정확히 표현되는 좌표라서 정답과 완전히 일치해야 하는 유형(문서 7절 합격 기준)
        public static bool IsExact(Category c) => c != Category.RandomReal && c != Category.FarLongReal;

        public sealed class Report
        {
            public int Cases, Visits, Misses, Extras, Duplicates, OrderViolations, ReferenceMismatches;
            public readonly List<string> Failures = new List<string>();

            public bool Passed(bool exact) =>
                Misses == 0 && Duplicates == 0 && ReferenceMismatches == 0 && (!exact || (Extras == 0 && OrderViolations == 0));

            public override string ToString() =>
                $"cases={Cases} visits={Visits} misses={Misses} extras={Extras} duplicates={Duplicates} " +
                $"orderViolations={OrderViolations} referenceMismatches={ReferenceMismatches}" +
                (Failures.Count > 0 ? "\n  " + string.Join("\n  ", Failures) : "");
        }

        public static IEnumerable<(double3 p0, double3 p1)> Generate(Category c, int count, int seed)
        {
            var rnd = new System.Random(seed);
            double Q(double range) => rnd.Next(-(int)(range * 4), (int)(range * 4) + 1) / 4.0;   // 1/4 격자
            double R(double range) => (rnd.NextDouble() * 2 - 1) * range;
            double3 QV(double range) => new double3(Q(range), Q(range), Q(range));

            for (int i = 0; i < count; i++)
            {
                switch (c)
                {
                    case Category.QuarterGrid:
                    {
                        double range = i % 4 == 0 ? 8 : 3;
                        yield return (QV(range), QV(range));
                        break;
                    }
                    case Category.IntegerEndpoints:
                    {
                        int3 a = new int3(rnd.Next(-5, 6), rnd.Next(-5, 6), rnd.Next(-5, 6));
                        int3 b = new int3(rnd.Next(-5, 6), rnd.Next(-5, 6), rnd.Next(-5, 6));
                        yield return ((double3)a, (double3)b);
                        break;
                    }
                    case Category.AxisParallel:
                    {
                        double3 a = QV(4), b = QV(4);
                        int mask = rnd.Next(1, 7);                    // 0이 될 축 집합(1~2축, 3축은 길이 0 유형)
                        bool onBoundary = rnd.Next(2) == 0;           // 고정 좌표를 정수 경계 위에 둔다
                        for (int axis = 0; axis < 3; axis++)
                        {
                            if ((mask & (1 << axis)) == 0) continue;
                            if (onBoundary) a[axis] = math.round(a[axis]);
                            b[axis] = a[axis];
                        }
                        yield return (a, b);
                        break;
                    }
                    case Category.ZeroLength:
                    {
                        // 1/2 격자면 내부(.5), 면/모서리/꼭짓점(정수 축 1~3개)이 고르게 나온다.
                        double3 p = new double3(rnd.Next(-6, 7) / 2.0, rnd.Next(-6, 7) / 2.0, rnd.Next(-6, 7) / 2.0);
                        if (i % 3 == 0) p = QV(3);
                        yield return (p, p);
                        break;
                    }
                    case Category.RandomReal:
                    {
                        double range = i % 4 == 0 ? 30 : 6;
                        yield return (new double3(R(range), R(range), R(range)), new double3(R(range), R(range), R(range)));
                        break;
                    }
                    case Category.FarLong:
                    {
                        double3 center = new double3(Sign(rnd), Sign(rnd), Sign(rnd)) * (1000 + rnd.Next(0, 200));
                        if (i % 50 == 0)
                            // 원점을 가로지르는 아주 긴 레이(수천 칸)
                            yield return (-center + QV(2), center + QV(2));
                        else
                            yield return (center + QV(40), center + QV(40));
                        break;
                    }
                    case Category.FarLongReal:
                    {
                        double3 center = new double3(Sign(rnd), Sign(rnd), Sign(rnd)) * (1000 + rnd.NextDouble() * 200);
                        yield return (center + new double3(R(40), R(40), R(40)), center + new double3(R(40), R(40), R(40)));
                        break;
                    }
                    case Category.Negative:
                    {
                        double3 a = -math.abs(QV(10)) - 0.25, b = -math.abs(QV(10)) - 0.25;
                        if (math.csum(a) < math.csum(b)) { var t = a; a = b; b = t; } // 대체로 음의 방향
                        yield return (a, b);
                        break;
                    }
                }
            }
        }

        static double Sign(System.Random rnd) => rnd.Next(2) == 0 ? -1 : 1;

        public static Report Run(Category c, int count, int seed)
        {
            var report = new Report();
            var visited = new List<int3>(256);
            var reference = new List<int3>(256);
            foreach (var (p0, p1) in Generate(c, count, seed))
                Check(p0, p1, IsExact(c), report, visited, reference);
            return report;
        }

        public static void Check(double3 p0, double3 p1, bool exact, Report report, List<int3> visited = null, List<int3> reference = null)
        {
            visited = visited ?? new List<int3>();
            reference = reference ?? new List<int3>();
            visited.Clear();
            reference.Clear();

            var collector = new VoxelListCollector(visited);
            VoxelSupercover.Traverse(p0, p1, ref collector);
            var refCollector = new VoxelListCollector(reference);
            ReferenceSupercover.Traverse(p0, p1, ref refCollector);

            report.Cases++;
            report.Visits += visited.Count;
            var truth = SupercoverOracle.Solve(p0, p1);

            int misses = 0, extras = 0, duplicates = 0, order = 0;
            bool refMismatch = !SameSequence(visited, reference);

            var seen = new HashSet<int3>();
            SupercoverOracle.Fraction last = new SupercoverOracle.Fraction(0, 1);
            foreach (int3 v in visited)
            {
                if (!seen.Add(v)) { duplicates++; continue; }
                if (!truth.TryGetValue(v, out var tFirst)) { extras++; continue; }
                // 방문 순서: 첫 방문 시점의 t가 감소하지 않음(같은 상자 안의 복셀은 첫 t가 같다)
                if (exact && !SupercoverOracle.Fraction.LessOrEqual(last, tFirst)) order++;
                last = tFirst;
            }
            foreach (int3 v in truth.Keys)
                if (!seen.Contains(v)) misses++;

            report.Misses += misses;
            report.Extras += extras;
            report.Duplicates += duplicates;
            report.OrderViolations += order;
            if (refMismatch) report.ReferenceMismatches++;

            bool failed = misses > 0 || duplicates > 0 || refMismatch || (exact && (extras > 0 || order > 0));
            if (failed && report.Failures.Count < 10)
                report.Failures.Add(Describe(p0, p1, visited, truth, misses, extras, duplicates, order, refMismatch));
        }

        static bool SameSequence(List<int3> a, List<int3> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        public static string Format(double3 p) => $"({p.x:R}, {p.y:R}, {p.z:R})";

        static string Describe(double3 p0, double3 p1, List<int3> visited, Dictionary<int3, SupercoverOracle.Fraction> truth,
            int misses, int extras, int duplicates, int order, bool refMismatch)
        {
            var sb = new StringBuilder();
            sb.Append($"p0={Format(p0)} p1={Format(p1)} misses={misses} extras={extras} dup={duplicates} order={order} refMismatch={refMismatch}");
            var visitedSet = new HashSet<int3>(visited);
            int shown = 0;
            foreach (var kv in truth)
                if (!visitedSet.Contains(kv.Key) && shown++ < 4) sb.Append($" missing={kv.Key} t={kv.Value}");
            shown = 0;
            foreach (int3 v in visitedSet)
                if (!truth.ContainsKey(v) && shown++ < 4) sb.Append($" extra={v}");
            return sb.ToString();
        }
    }
}
