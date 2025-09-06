[中文版](README-zh.md)


<p align="center">
  <img src="./assets/nbdb128.png">
</p>

> NewBeeDB is an embedded vector database developed based on the code of [HNSWIndex.Net](https://github.com/Skaipi/HNSWIndex.Net). It can be conveniently used for cross-platform on-device AI application development. Meanwhile, it also provides a basic framework for developing more complex and large-scale vector applications.

## Key Improvements Over HNSWIndex.Net

During my cross-platform application development with Avalonia, I noticed a lack of embedded vector databases supporting Native AOT in the C# community. Therefore, I made extensive modifications to HNSWIndex.Net, resulting in the current version of NewBeeDB. The main improvements are as follows:

- NativeAOT Compatibility: Adapted to support Native AOT compilation.
- implified Type System: Streamlined the type system of HNSWIndex.Net by removing many generics. Currently, it only supports vector retrieval for float[], which covers most vector retrieval scenarios.
- Index Serialization & Deserialization: Added mechanisms for index serialization and deserialization. Indexes can be serialized into zip files and loaded from zip files.
- IBackend Interface: Introduced the IBackend interface and implemented SqliteBackend (requires referencing the `NewBeeDB.Backends` NuGet package). When `SqliteBackend` is configured, all add/remove operations on the index are saved to the hard disk in real time. You can also implement your own IBackend interface.

## Usage Examples

### Create Index, Insert, Delete, and Query

```csharp
HNSWIndex index = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute);
var points = HNSWPoint.Random(128, 100);
foreach(var p in points)
{
    index.Add(p);
}

index.Remove(points[2]);

var queryPoint = points[0];
var match = index.Query(queryPoint, 10);
Console.WriteLine($"Query Point: {queryPoint.Label}");
foreach (var m in match)
{
    Console.WriteLine($"{m.Point.Label} - {m.Distance}");
}
```

The definition of HNSWPoint is as follows:

```
public partial class HNSWPoint
{
    public float[] Data { get; set; } = Array.Empty<float>();
    public string Label { get; set; } = String.Empty;
    public int Id { get; internal set; } = -1;
}
```

In actual insertion scenarios, `Label` should serve as the unique key for each point (keys must be distinct across different points). `Data` represents the vector to be retrieved, with no restrictions on dimension (array length). However, all points within the same HNSWIndex must have the same dimension. The default value of Id is -1; once a point is inserted into the index, the system will automatically assign an Id to it.

### Example: Save Index to Zip File & Load Index from Zip File

```csharp
string zipFilePath = "hnsw_index.zip";
HNSWIndex index = new HNSWIndex(HNSWPoint.CosineMetricUnitCompute);
var points = HNSWPoint.Random(128, 100);
foreach (var p in points)
{
    index.Add(p);
}
index.SerializeToZipFile(zipFilePath, "demo", sliceMaxCount: 500000);

var loadedIndex = HNSWIndex.DeserializeFromZipFile(HNSWPoint.CosineMetricUnitCompute, zipFilePath, "demo");
var queryPoint = points[0];
var match = loadedIndex.Query(queryPoint, 10);
Console.WriteLine($"Query Point: {queryPoint.Label}");
foreach (var m in match)
{
    Console.WriteLine($"{m.Point.Label} - {m.Distance}");
}
```

In the example above, the index is stored as files named demo.xxxx (and related files) within a compressed file named hnsw_index.zip. Multiple HNSWIndex instances can be stored in a single zip file by using different name parameters. Due to the length limit of MemoryStream in C#, the index will be automatically split into chunks when it is extremely large. sliceMaxCount is the chunking parameter—for every sliceMaxCount points, a separate bucket file is created in the compressed package. For 512-dimensional float vectors, `sliceMaxCount: 500000` is a practical empirical value.

### SqliteBackend

Storing indexes using zip files consumes significant time and computing resources. In some cases, a backend that saves index modifications in real time is required. NewBeeDB provides the `IBackend` interface for custom backend implementations. The `NewBeeDB.Backends` NuGet package includes a built-in Sqlite Backend implementation.

```csharp
using var backend = new SqliteBackend("demo.db");
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
```

## Large-Scale Applications

A single HNSWIndex is suitable for datasets with fewer than 10 million entries. For larger-scale datasets, sharding is required—each shard functions as an independent HNSWIndex. You need to implement the relevant sharding logic yourself.