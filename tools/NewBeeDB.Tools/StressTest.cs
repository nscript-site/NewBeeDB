using NewBeeDB.Backends;
using System.Diagnostics;

namespace NewBeeDB.Tools;

internal class StressTest
{
    public int Count { get; set; } = 10000;
    public int Dimension { get; set; } = 512;
    public int BackendMode { get; set; } = 1;  // 0: none,  1 : InMemory, 2: InFile

    private SqliteBackend? CreateBackend()
    {
        // 使用内存数据库，避免文件残留
        if(BackendMode == 1) return new SqliteBackend(":memory:");
        else if(BackendMode == 2) return new SqliteBackend($"hnsw_stress_test_{DateTime.Now.ToFileTimeUtc()}.db");
        else return null;
    }

    public void Run()
    {
        int dimension = Dimension > 0 ? Dimension : 32;

        Console.WriteLine($"[RUN Stress]: Count - {Count}, Dimension - {dimension}, BackendMode - {BackendMode}");

        // 1. 创建 Backend 为 SqliteBackend 的 HNSWIndex, 记为 hnsw1
        using var backend = CreateBackend();
        var parameters = new HNSWParameters() { CollectionSize = Count };

        Console.WriteLine($"Generate HNSWIndex Begin");
        Stopwatch sw = Stopwatch.StartNew();
        // 余弦距离
        Func<HNSWPoint, HNSWPoint, float> distFnc = HNSWPoint.CosineMetricUnitCompute;
        var hnsw1 = new HNSWIndex(distFnc, parameters, backend);

        // 2. 生成随机数据
        var points = HNSWPoint.Random(dimension, 100, true);

        HNSWPoint NextPoint()
        {
            if(points.Count == 0)
                points = HNSWPoint.Random(dimension, 100, true);

            int idx = points.Count - 1;
            var p = points[idx];
            points.RemoveAt(idx);
            return p;
        }

        int num = 0;
        var swBatch = Stopwatch.StartNew();
        for (int i = 0; i < Count; i++)
        {
            var p = NextPoint();
            num++;
            hnsw1.Add(p);
            if (num % 100 == 0)
            {
                swBatch.Stop();
                Console.WriteLine($"Add {num}/{Count} points, Elapsed Time: {swBatch.Elapsed.TotalSeconds.ToString("0.0000")} s");
                swBatch.Restart();
            }
        }

        sw.Stop();
        Console.WriteLine($"Generate HNSWIndex Finished, total - {hnsw1.Count}, Elapsed Time: {sw.Elapsed}");

        if (backend != null)
        {
            Console.WriteLine($"Sqlite excute count: {backend.GetExcuteCount()}, Update nodes times: {backend.GetUpdateNodeCount()}");
        }
    }
}
