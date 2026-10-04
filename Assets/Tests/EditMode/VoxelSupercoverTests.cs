using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Mathematics;
using Kind = VoxelRay.Tests.SupercoverChecks.Category;

namespace VoxelRay.Tests
{
    // Docs/voxel-ray-supercover.md 7절의 필수 테스트.
    public class VoxelSupercoverTests
    {
        // 문서 7절 입력 유형별 케이스 수(합계 약 24,000건, 문서의 C# 검증 규모)
        [TestCase(Kind.QuarterGrid, 10000)]
        [TestCase(Kind.IntegerEndpoints, 3000)]
        [TestCase(Kind.AxisParallel, 3000)]
        [TestCase(Kind.ZeroLength, 1000)]
        [TestCase(Kind.RandomReal, 4000)]
        [TestCase(Kind.FarLong, 600)]
        [TestCase(Kind.FarLongReal, 400)]
        [TestCase(Kind.Negative, 2000)]
        public void MatchesOracle(Kind category, int count)
        {
            var report = SupercoverChecks.Run(category, count, 20261004 + (int)category);
            UnityEngine.Debug.Log($"[VoxelSupercover] {category}: {report}");
            Assert.That(report.Passed(SupercoverChecks.IsExact(category)), Is.True, report.ToString());
        }

        // 손으로 셀 수 있는 경우: 닿는 복셀 수가 정확해야 한다.
        [TestCase(0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 1, TestName = "Point inside voxel → 1")]
        [TestCase(1.0, 0.5, 0.5, 1.0, 0.5, 0.5, 2, TestName = "Point on face → 2")]
        [TestCase(1.0, 1.0, 0.5, 1.0, 1.0, 0.5, 4, TestName = "Point on edge → 4")]
        [TestCase(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 8, TestName = "Point on vertex → 8")]
        [TestCase(0.5, 0.5, 0.5, 1.5, 1.5, 1.5, 8, TestName = "3D diagonal through vertex → 8")]
        [TestCase(0.5, 0.5, 0.5, 1.5, 1.5, 0.5, 4, TestName = "2D diagonal through edge → 4")]
        [TestCase(0.5, 0.5, 0.5, 3.5, 0.5, 0.5, 4, TestName = "Axis-aligned through interiors → 4")]
        [TestCase(0.0, 0.5, 0.5, 3.0, 0.5, 0.5, 5, TestName = "Axis-aligned, endpoints on faces → 5")]
        [TestCase(0.5, 1.0, 0.5, 2.5, 1.0, 0.5, 6, TestName = "Along a face → 2 per cell")]
        [TestCase(0.0, 0.0, 0.5, 0.0, 0.0, 2.5, 12, TestName = "Along an edge → 4 per cell")]
        [TestCase(-0.5, -0.5, -0.5, -1.5, -1.5, -1.5, 8, TestName = "Negative diagonal through vertex → 8")]
        public void CountsTouchedVoxels(double x0, double y0, double z0, double x1, double y1, double z1, int expected)
        {
            var p0 = new double3(x0, y0, z0);
            var p1 = new double3(x1, y1, z1);
            var visited = Collect(p0, p1);
            Assert.That(visited.Count, Is.EqualTo(expected), string.Join(" ", visited));

            var report = new SupercoverChecks.Report();
            SupercoverChecks.Check(p0, p1, true, report);
            Assert.That(report.Passed(true), Is.True, report.ToString());
        }

        [Test]
        public void RejectsNonFiniteAndOutOfRangeInput()
        {
            var ok = new double3(0.5, 0.5, 0.5);
            var bad = new[]
            {
                new double3(double.NaN, 0, 0),
                new double3(0, double.PositiveInfinity, 0),
                new double3(0, 0, double.NegativeInfinity),
                new double3(VoxelSupercover.MaxCoordinate * 2, 0, 0),
            };
            foreach (var b in bad)
            {
                var c = new VoxelListCollector(new List<int3>());
                Assert.Throws<ArgumentException>(() => VoxelSupercover.Traverse(ok, b, ref c), SupercoverChecks.Format(b));
                Assert.Throws<ArgumentException>(() => VoxelSupercover.Traverse(b, ok, ref c), SupercoverChecks.Format(b));
            }
        }

        [Test]
        public void StopsImmediatelyWhenVisitorReturnsTrue()
        {
            var p0 = new double3(0.25, 0.5, -3.75);
            var p1 = new double3(7.5, -2.0, 4.0);
            var full = Collect(p0, p1);
            Assert.That(full.Count, Is.GreaterThan(5));

            for (int stopAt = 1; stopAt <= full.Count; stopAt++)
            {
                var v = new StopAfter { Limit = stopAt, Visited = new List<int3>() };
                bool stopped = VoxelSupercover.Traverse(p0, p1, ref v);
                Assert.That(stopped, Is.True);
                Assert.That(v.Visited, Is.EqualTo(full.GetRange(0, stopAt)));
            }

            var all = new StopAfter { Limit = int.MaxValue, Visited = new List<int3>() };
            Assert.That(VoxelSupercover.Traverse(p0, p1, ref all), Is.False);
        }

        // 성능 비교(합격 기준 아님). 에디터의 Code Optimization이 Debug면 절대값은 실제 빌드보다 크게 나온다.
        [Test, Category("Performance")]
        public void Benchmark_ReferenceVsOptimized()
        {
            var rnd = new System.Random(7);
            const int n = 20000;
            var a = new double3[n];
            var b = new double3[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = new double3(rnd.NextDouble() * 100 - 50, rnd.NextDouble() * 100 - 50, rnd.NextDouble() * 100 - 50);
                var dir = math.normalize(new double3(rnd.NextDouble() * 2 - 1, rnd.NextDouble() * 2 - 1, rnd.NextDouble() * 2 - 1));
                b[i] = a[i] + dir * (rnd.NextDouble() * 30);
            }

            Measure(a, b, false, out _, out _); // JIT 예열
            Measure(a, b, true, out _, out _);
            double refNs = double.MaxValue, optNs = double.MaxValue;
            int refHash = 0, optHash = 0, voxels = 0;
            for (int rep = 0; rep < 5; rep++)
            {
                refNs = Math.Min(refNs, Measure(a, b, true, out refHash, out voxels));
                optNs = Math.Min(optNs, Measure(a, b, false, out optHash, out _));
            }

            UnityEngine.Debug.Log($"[VoxelSupercover] benchmark {n} rays, {voxels} voxels " +
                                  $"(code optimization: {UnityEditor.Compilation.CompilationPipeline.codeOptimization}): " +
                                  $"reference {refNs / n:F0} ns/ray, optimized {optNs / n:F0} ns/ray, speedup ×{refNs / optNs:F2}");
            Assert.That(optHash, Is.EqualTo(refHash), "optimized and reference must visit the same voxels in the same order");
        }

        static double Measure(double3[] a, double3[] b, bool reference, out int hash, out int count)
        {
            var v = new HashingVisitor();
            var sw = Stopwatch.StartNew();
            if (reference) for (int i = 0; i < a.Length; i++) ReferenceSupercover.Traverse(a[i], b[i], ref v);
            else for (int i = 0; i < a.Length; i++) VoxelSupercover.Traverse(a[i], b[i], ref v);
            sw.Stop();
            hash = v.Hash;
            count = v.Count;
            return sw.ElapsedTicks * 1e9 / Stopwatch.Frequency;
        }

        static List<int3> Collect(double3 p0, double3 p1)
        {
            var list = new List<int3>();
            var c = new VoxelListCollector(list);
            VoxelSupercover.Traverse(p0, p1, ref c);
            return list;
        }

        struct StopAfter : IVoxelVisitor
        {
            public int Limit;
            public List<int3> Visited;

            public bool Visit(int3 voxel)
            {
                Visited.Add(voxel);
                return Visited.Count >= Limit;
            }
        }

        struct HashingVisitor : IVoxelVisitor
        {
            public int Count, Hash;

            public bool Visit(int3 v)
            {
                Count++;
                Hash = Hash * 31 + v.x * 7 + v.y * 13 + v.z;
                return false;
            }
        }
    }
}
