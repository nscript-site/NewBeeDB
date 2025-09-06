# English Instruction 

> NewBeeDB is an embedded vector database developed based on the code of [HNSWIndex.Net](https://github.com/Skaipi/HNSWIndex.Net). It can be conveniently used for cross-platform on-device AI application development. Meanwhile, it also provides a basic framework for developing more complex and large-scale vector applications.

## Key Improvements Over HNSWIndex.Net

During my cross-platform application development with Avalonia, I noticed a lack of embedded databases supporting Native AOT in the C# community. Therefore, I made extensive modifications to HNSWIndex.Net, resulting in the current version of NewBeeDB. The main improvements are as follows:

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

# 中文说明


> NewBeeDB 是在 [HNSWIndex.Net](https://github.com/Skaipi/HNSWIndex.Net) 的代码基础上开发的嵌入式向量数据库，可以很方便的用于跨平台的端侧 AI 应用开发。同时，它也为开发更复杂、更大规模向量应用，提供了基础框架。

## 对 HNSWIndex.Net 的主要改进

我在用 Avalonia 开发跨平台应用过程中，发现 C# 社区缺乏支持 Native AOT 的嵌入式数据库，于是在 HNSWIndex.Net 的基础上，魔改了一番，形成现在的 NewBeeDB。主要工作如下：

- 适配 NativeAOT;
- 简化 HNSWIndex.Net 的类型系统，去掉了很多泛型，目前仅支持 float[] 的向量检索，这适用于大多数向量检索场景；
- 添加了索引的序列化和反序列化机制，可以将索引序列化为 zip 文件，也可以从 zip 文件中加载索引；
- 添加了 IBackend 接口，实现了 SqliteBackend(需要引用 `NewBeeDB.Backends` 这个 nuget 包)。如果设置了 SqliteBackend，您对索引的增减操作，可以实时保存在硬盘上。您也可以实现自己的 IBackend 接口。

## 使用示例

### 创建索引、插入、删除与查询

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

HNSWPoint 点的定义如下:

```
public partial class HNSWPoint
{
    public float[] Data { get; set; } = Array.Empty<float>();
    public string Label { get; set; } = String.Empty;
    public int Id { get; internal set; } = -1;
}
```

实际插入时，Label 应该是该点的 key，不同点 key 应不一样。Data 为需要检索的向量，没有维度(数组长度)限制，但是，同一个 HNSWIndex 里所有点的维度应该一样。Id 默认值为 -1，当插入索引后，系统会自动为该点分配一个 Id。

### 索引存储为 zip 文件及从 zip 文件中加载示例

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

上例中，索引会存储为名为 hnsw_index.zip 的压缩包里的 demo.xxxx 等相关文件。使用不同的 name，可以将多个 HNSWIndex 索引存储在一个压缩包里。由于 c# 里 MemoryStream 有长度限制，当索引特别大时，会自动分片存储。sliceMaxCount 是分片参数，每 sliceMaxCount 个点，会存储为压缩包里单独的桶文件。对于 512 维的 float 向量，`sliceMaxCount: 500000` 是一个合适的经验参数。

### SqliteBackend

用 zip 文件来存储，非常消耗时间和计算资源。有时，我们需要一个后端，实时存储索引的修改。NewBeeDB 提供了 `IBackend` 接口，可以自定义后端。`NewBeeDB.Backends` 这个 nuget 包提供了一个 Sqlite Backend 实现。

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

## 大型应用

单个 HNSWIndex 适合 1000万以下的数据。如果需要支持更大规模的数据，需要进行分片处理，每个片是单独的1个 HNSWIndex。需要自行实现相关逻辑。