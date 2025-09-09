using NewBeeDB.Backends;
using System.Diagnostics;
using System.IO.Compression;

namespace NewBeeDB.Demo;

internal class Program
{
    static void Main(string[] args)
    {
        //TimedHNSWPointExample();

        BaseExample();

        //ZipExample();

        //BackendExample();

        //int total = 1000;
        //if (args.Length > 0)
        //{
        //    if (int.TryParse(args[0], out int val))
        //    {
        //        total = Math.Max(val, 100);
        //    }
        //}

        //TestAndVerifySqliteBackend(total, 100);

        //FuncTestInMemory(200);
        //FuncTestInZipFile(1000, 300);


        //PressTest(total, true, 500000);
    }

    private static void BaseExample()
    {
        HNSWIndex index = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute);
        var points = HNSWPoint.Random(128, 100);
        foreach(var p in points)
        {
            index.Add(p);
        }
        index.Remove(points[2]);
        var queryPoint = points[0];

        // query by point
        var match = index.Query(queryPoint, 10);
        Console.WriteLine($"Query Point: {queryPoint.Label}");
        foreach (var m in match)
        {
            Console.WriteLine($"{m.Point.Label} - {m.Distance}");
        }

        Console.WriteLine();

        // query by vector
        match = index.Query(queryPoint.Data, 10);
        Console.WriteLine($"Query Point: {queryPoint.Label}");
        foreach (var m in match)
        {
            Console.WriteLine($"{m.Point.Label} - {m.Distance}");
        }
    }

    private static void TimedHNSWPointExample()
    {
        // generate random TimedHNSWPoints
        DateTime now = DateTime.Now;
        TimedHNSWPoint GeneratePoint()
        {
            now += TimeSpan.FromMinutes(1);
            return new TimedHNSWPoint() { CreatedTime = now };
        }

        var points = HNSWPoint.Random(128, 20, onCreate: GeneratePoint);

        // use SqliteBackend with TimedHNSWPointSqliteSerializer, it can serialize/deserialize TimedHNSWPoint
        var sqliteBackend = new SqliteBackend(":memory:", hnswPointSerializer: new TimedHNSWPointSqliteSerializer());

        // print points and their CreatedTime
        HNSWIndex hnsw = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute, backend: sqliteBackend);
        Console.WriteLine("Adding points:");
        foreach (var p in points)
        {
            Console.WriteLine($"Point: {p.Label}, CreatedTime: {(p as TimedHNSWPoint)!.CreatedTime!.Value.ToFileTimeUtc()}");
            hnsw.Add(p);
        }

        Console.WriteLine();

        // query
        var queryPoint = points[0];
        var match = hnsw.Query(queryPoint, 10);
        Console.WriteLine($"Query Point: {queryPoint.Label}");
        Console.WriteLine();
        Console.WriteLine("Matched Points:");
        foreach (var m in match)
        {
            var tp = m.Point as TimedHNSWPoint;
            if(tp == null) Console.WriteLine($"{m.Point.Label} - {m.Distance}");
            else
            {
                Console.WriteLine($"{tp.Label},{tp.CreatedTime!.Value.ToFileTimeUtc()} - {m.Distance}");
            }
        }

        // load from sqlite backend
        var loadedHnswFromSqlite = sqliteBackend.Load(HNSWPoint.CosineMetricUnitCompute);

        // compare hnsw and loadedHnswFromSqlite
        Console.WriteLine($"hnsw == loadedHnswFromSqlite: {hnsw.Equals(loadedHnswFromSqlite)}");

        // serialize to zip file
        hnsw.SerializeToZipFile("timed_hnsw_index.zip", "timed_demo", sliceMaxCount: 500000);

        // load from zip file
        var loadedHnswFromZipFile = HNSWIndex.DeserializeFromZipFile<TimedHNSWPoint>(HNSWPoint.CosineMetricUnitCompute, "timed_hnsw_index.zip", "timed_demo");

        // compare hnsw and loadedHnswFromZipFile
        Console.WriteLine($"hnsw == loadedHnswFromZipFile: {hnsw.Equals(loadedHnswFromZipFile)}");
    }

    private static void ZipExample()
    {
        string zipFilePath = "hnsw_index.zip";
        HNSWIndex hnsw = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute);
        var points = HNSWPoint.Random(128, 100);
        foreach (var p in points)
        {
            hnsw.Add(p);
        }
        hnsw.SerializeToZipFile(zipFilePath, "demo", sliceMaxCount: 500000);

        var hnsw2 = HNSWIndex.DeserializeFromZipFile(HNSWPoint.CosineMetricUnitCompute, zipFilePath, "demo");

        Console.WriteLine($"hnsw == hnsw2: { hnsw.Equals(hnsw2)}");

        var queryPoint = points[0];
        var match = hnsw2.Query(queryPoint, 10);
        Console.WriteLine($"Query Point: {queryPoint.Label}");
        foreach (var m in match)
        {
            Console.WriteLine($"{m.Point.Label} - {m.Distance}");
        }
    }

    private static void BackendExample()
    {
        using var backend = new SqliteBackend(":memory:");
        var parameters = new HNSWParameters();
        var hnsw = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute, parameters, backend);
        var points = HNSWPoint.Random(32, 1000, true);
        foreach (var p in points)
        {
            hnsw.Add(p);
        }
        
        Console.WriteLine($"HNSWIndex generated, total - {hnsw.Count}");

        var hnsw2 = backend.Load(HNSWPoint.CosineMetricUnitCompute);

        if(hnsw2 == null)
        {
            Console.WriteLine($"Load from backend failed");
            return;
        }

        Console.WriteLine($"hnsw == hnsw2: { hnsw.Equals(hnsw2)}");

        var queryPoint = points[0];
        var match = hnsw2.Query(queryPoint, 10);
        Console.WriteLine($"Query Point: {queryPoint.Label}");
        foreach (var m in match)
        {
            Console.WriteLine($"{m.Point.Label} - {m.Distance}");
        }
    }

    private static SqliteBackend CreateBackend()
    {
        // 使用内存数据库，避免文件残留
        return new SqliteBackend(":memory:");
    }

    /// <summary>
    /// 创建索引并验证
    /// </summary>
    internal static void TestAndVerifySqliteBackend(int count = 1000, int removeCount = 10)
    {
        Console.WriteLine($"[RUN TestSqliteBackend]: count - {count}, removeCount - {removeCount}");

        // 1. 创建 Backend 为 SqliteBackend 的 HNSWIndex, 记为 hnsw1
        using var backend = CreateBackend();
        var parameters = new HNSWParameters();

        Console.WriteLine($"Generate HNSWIndex Begin");
        Stopwatch sw = Stopwatch.StartNew();
        // 余弦距离
        Func<HNSWPoint, HNSWPoint, float> distFnc = HNSWPoint.CosineMetricUnitCompute;
        var hnsw1 = new HNSWIndex(distFnc, parameters, backend);

        // 2. 生成 1000 条随机数据并插入
        var points = HNSWPoint.Random(32, count, true);

        int num = 0;
        foreach (var p in points)
        {
            num++;
            hnsw1.Add(p);
            if(num % 100 == 0)
                Console.WriteLine($"Add {num}/{count} points");
        }

        sw.Stop();
        Console.WriteLine($"Generate HNSWIndex Finished, total - {hnsw1.Count}, sqlite excute count: {backend.GetExcuteCount()}, sqlite update nodes count: {backend.GetUpdateNodeCount()}");
        Console.WriteLine($"Elapsed Time: {sw.Elapsed}");

        // 3. 从 SqliteBackend 中 Load 新的 HNSWIndex，记为 hnsw2
        var hnsw2 = backend.Load(distFnc);

        Console.WriteLine($"hnsw1 == hnsw2: { hnsw1.Equals(hnsw2)}");


        // 5. 从 hnsw1 中随机删除 10 条记录
        var rnd = new Random(123);
        var allIndexes = Enumerable.Range(0, hnsw1.Count).ToList();
        var removeIndexes = allIndexes.OrderBy(_ => rnd.Next()).Take(removeCount).ToList();
        hnsw1.Remove(removeIndexes);

        // 6. 从 SqliteBackend 中 Load 新的 HNSWIndex，记为 hnsw3
        var hnsw3 = backend.Load(distFnc);

        // 7. 判断 hnsw1 和 hnsw3 是否相等
        Console.WriteLine($"hnsw1 == hnsw3: {hnsw1.Equals(hnsw3)}");
    }

    internal static void StressTest(int total = 1000000, bool serialize2ZipFile = false, int sliceMaxCountWhenSave2ZipFile = 100000)
    {
        Console.WriteLine($"[RUN PRESS TEST]: total - {total}, serialize2ZipFile - {serialize2ZipFile}, sliceMaxCountWhenSave2ZipFile - {sliceMaxCountWhenSave2ZipFile}");
        Console.WriteLine($"[{DateTime.Now.ToString()}] Generate data ...");
        HNSWIndex index = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute);

        void Serialize(int num)
        {
            Console.WriteLine($"[{DateTime.Now.ToString()}] Serialize items begin");
            Stopwatch sw = Stopwatch.StartNew();
            var indexData = index.Serialize();
            sw.Stop();
            Console.WriteLine($"Serialize {num} items in {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"[{DateTime.Now.ToString()}] Serializeitems to {indexData.Length} bytes");
        }

        void Serialize2Zip(int num)
        {
            Console.WriteLine($"[{DateTime.Now.ToString()}] Serialize items begin");
            var zipFilePath = $"hnsw_index_{num}.zip";
            Stopwatch sw = Stopwatch.StartNew();
            index.SerializeToZipFile(zipFilePath, "clip", sliceMaxCount: sliceMaxCountWhenSave2ZipFile);
            sw.Stop();
            Console.WriteLine($"[{DateTime.Now.ToString()}] Serialize items finished");
            Console.WriteLine($"Serialize {num} items in {sw.ElapsedMilliseconds} ms");
        }

        Random random = new Random(total);
        for (int i = 0; i < total; i++)
        {
            var feature = new float[512];
            for(int d = 0; d < feature.Length; d++)
            {
                feature[d] = random.NextSingle();
            }
            index.MockAdd(new HNSWPoint { Label = $"test{i}", Data = feature });
        }

        if(serialize2ZipFile == false)
            Serialize(total);
        else
            Serialize2Zip(total);

        Console.WriteLine($"[{DateTime.Now.ToString()}] Serialized all items finished");
    }

    internal static void FuncTestInMemory(int total = 1000000)
    {
        Console.WriteLine($"[RUN TEST]: total - {total}");
        byte[]? data = null;
        FuncTest(total, (index) => data = index.Serialize(),
            () => HNSWIndex.Deserialize(HNSWPoint.CosineMetricUnitCompute, data ?? new byte[0]));
    }

    internal static void FuncTestInZipFile(int total = 1000000, int sliceMaxCount = 10000)
    {
        Console.WriteLine($"[RUN TEST]: total - {total}");
        string zipFilePath = $"hnsw_index_{total}.zip";
        FuncTest(total, (index) => index.SerializeToZipFile(zipFilePath, sliceMaxCount: sliceMaxCount),
            () => HNSWIndex.DeserializeFromZipFile(HNSWPoint.CosineMetricUnitCompute, zipFilePath));
    }

    internal static void FuncTest(int total, Action<HNSWIndex> serializeFunc, Func<HNSWIndex?> deserializeFunc, bool nomalize = true)
    {
        Console.WriteLine($"[RUN TEST]: total - {total}, nomalize - {nomalize}");

        var vectors = HNSWPoint.Random(128, total, nomalize);
        var vectors2 = HNSWPoint.Random(128, 1, nomalize);

        Console.WriteLine($"{total} points generated");

        var index = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute);

        index.BatchAdd(vectors, 100);

        Console.WriteLine($"index generated");

        Console.WriteLine($"index deserialized");

        serializeFunc(index);

        Console.WriteLine($"index serialized");

        var originalResults = index.Query(vectors2[0], 5);
        Console.WriteLine("originalResults:");
        Console.WriteLine(index.Count);
        Console.WriteLine(originalResults[0].Distance);
        Console.WriteLine(originalResults[2].Distance);
        Console.WriteLine(originalResults[0].Point.Data[0]);
        Console.WriteLine(originalResults[0].Point.Label);

        var decodedIndex = deserializeFunc();
        if (decodedIndex != null)
        {
            bool equals = Equals(decodedIndex, index);
            Console.WriteLine($"Equals: {equals}");
            var decodeResults = decodedIndex.Query(vectors2[0], 5);
            Console.WriteLine("decodeResults:");
            Console.WriteLine(decodedIndex.Count);
            Console.WriteLine(decodeResults[0].Distance);
            Console.WriteLine(decodeResults[2].Distance);
            Console.WriteLine(decodeResults[0].Point.Data[0]);
            Console.WriteLine(decodeResults[0].Point.Label);
        }
    }
}
