using NewBeeDB.Backends;
using System.Data.Common;
using System.Diagnostics;

namespace NewBeeDB.Tools;

internal class LoadIndex
{
    public string? Path { get; set; }

    public void Run()
    {
        if(string.IsNullOrEmpty(Path))
        {
            Console.WriteLine("Path is required.");
            return;
        }

        Stopwatch sw = Stopwatch.StartNew();
        var backend = new SqliteBackend(Path);
        var index = backend.Load(HNSWPoint.CosineMetricUnitCompute);
        sw.Stop();
        if (index == null) 
        {
            Console.WriteLine("Failed to load index.");
            return;
        }

        var snapshot = HNSWIndexSnapshot.CreateFrom(index);
        var data = snapshot.DataSnapshot;
        var p = snapshot.Parameters;
        Console.WriteLine($"Index loaded from {Path}, Elapsed Time: {sw.Elapsed.TotalSeconds.ToString("0.0000")} s");

        if(p != null)
        {
            Console.WriteLine($"Parameters:");
            Console.WriteLine($"  M: {p.MinNN}");
            Console.WriteLine($"  EfConstruction: {p.DistributionRate}");
            Console.WriteLine($"  EfSearch: {p.RandomSeed}");
            Console.WriteLine($"  MaxLevel: {p.CollectionSize}");
        }
        else
        {
            Console.WriteLine("Parameters is null.");
        }

        if (data == null)
        {
            Console.WriteLine("DataSnapshot is null.");
        }
        else
        {
            Console.WriteLine($"DataSnapshot loaded:");
            Console.WriteLine($"  Nodes count: {data.Nodes?.Count ?? 0}");
            Console.WriteLine($"  Items count: {data.Items?.Count ?? 0}");
            Console.WriteLine($"  RemovedIndexes count: {data.RemovedIndexes?.Count ?? 0}");
        }

        if(data?.Nodes?.Count > 0 && data.Items?.Count > 0)
        {
            var firstNode = data.Nodes[0];
            Console.WriteLine($"First node:");
            Console.WriteLine($"  Id: {firstNode.Id}");

            if (data.Items.TryGetValue(firstNode.Id, out HNSWPoint? firstItem) == true)
            {
                Console.WriteLine($"  Label: {firstItem.Label}");
                Console.WriteLine($"  Dim: {firstItem.Data.Length}");
                Console.WriteLine($"  Data: {string.Join(", ", firstItem.Data.Take(10))} ...");
            }
            else
            {
                Console.WriteLine("  First item is null or has no vector.");
            }
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
