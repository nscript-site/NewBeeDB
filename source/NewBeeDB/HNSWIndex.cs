using System.IO;

namespace NewBeeDB;

public class HNSWIndex
{
    // Delegates are not serializable and should be set after deserialization
    private Func<HNSWPoint, HNSWPoint, float> distanceFnc;

    internal readonly HNSWParameters parameters;

    internal readonly GraphData data;

    private readonly GraphConnector connector;

    private readonly GraphNavigator navigator;

    public object SyncRoot = new object();

    /// <summary>
    /// 后端，用于在添加新节点时，实时将节点信息写入外部存储。
    /// </summary>
    internal IBackend? Backend { get; set; }

    /// <summary>
    /// Construct KNN search graph with arbitrary distance function
    /// </summary>
    public HNSWIndex(Func<HNSWPoint, HNSWPoint, float> distFnc, HNSWParameters? hnswParameters = null, IBackend? backend = null)
    {
        Backend = backend;
        hnswParameters ??= new HNSWParameters();
        distanceFnc = distFnc;
        parameters = hnswParameters;

        data = new GraphData(distFnc, hnswParameters) { Backend = backend };
        navigator = new GraphNavigator(data);
        connector = new GraphConnector(data, navigator, hnswParameters);

        data.Reallocated += OnDataResized;

        Backend?.TrySave(() => {
            Backend.SaveParameters(parameters);
            Backend.SaveEntryPoint(data.EntryPointId);
            Backend.SaveCapacity(data.Capacity);
        });
    }

    /// <summary>
    /// Construct KNN search graph from serialized snapshot.
    /// </summary>
    public HNSWIndex(Func<HNSWPoint, HNSWPoint, float> distFnc, HNSWIndexSnapshot snapshot, IBackend? backend = null)
    {
        if (snapshot.Parameters is null)
            throw new ArgumentNullException(nameof(snapshot.Parameters), "Parameters cannot be null during deserialization.");

        if (snapshot.DataSnapshot is null)
            throw new ArgumentNullException(nameof(snapshot.DataSnapshot), "Data cannot be null during deserialization.");

        distanceFnc = distFnc;
        parameters = snapshot.Parameters;

        backend?.IsTrySaveEnabled = false;
        data = new GraphData(snapshot.DataSnapshot, distFnc, snapshot.Parameters) { Backend = backend };
        backend?.IsTrySaveEnabled = true;

        navigator = new GraphNavigator(data);
        connector = new GraphConnector(data, navigator, parameters);

        data.Reallocated += OnDataResized;

        this.Backend = backend;
    }

    /// <summary>
    /// Add new item with given label to the graph.
    /// </summary>
    protected int AddCore(HNSWPoint item)
    {
        var itemId = -1;
        lock (data.indexLock)
        {
            itemId = data.AddItem(item);
        }

        lock (data.Nodes[itemId].OutEdgesLock)
        {
            connector.ConnectNewNode(itemId);
        }
        return itemId;
    }

    /// <summary>
    /// Add collection of items to the graph
    /// </summary>
    protected void AddCore(IList<HNSWPoint> items, int maxThreads = 4)
    {
        int threads = Math.Min(maxThreads, Environment.ProcessorCount);
        threads = Math.Min(threads, items.Count);
        threads = Math.Max(threads, 1);

        Parallel.For(0, threads, (i) =>
        {
            for(int j = i; j < items.Count; j += threads)
            {
                AddCore(items[j]);
                //Console.WriteLine($"[HNSWIndex] Thread {i} added {items[j].Label} to the graph.");
            }
        });

        //Console.WriteLine($"[HNSWIndex] Added {items.Count} items to the graph using {threads} threads.");
    }

    public void Add(HNSWPoint item)
    {
        lock(SyncRoot)
        {
            AddCore(item);
        }
    }

    public void MockAdd(HNSWPoint item)
    {
        var itemId = -1;
        lock (data.indexLock)
        {
            itemId = data.AddItem(item);
            Node n = data.Nodes[itemId];
            Generate(n.InEdges);
            Generate(n.OutEdges);
        }

        void Generate(List<List<int>> edges)
        {
            while(edges.Count <= 5)
            {
                edges.Add(new List<int>());
            }

            foreach(var edge in edges)
            {
                while(edge.Count < 5)
                {
                    edge.Add(-1); // Mock connection
                }
            }
        }
    }

    public void BatchAdd(IList<HNSWPoint> items, int batchSize = 100, int maxThreads = 4)
    {
        if (items.Count == 0) return;
        int total = items.Count;
        int batches = (total + batchSize - 1) / batchSize; // 向上取整

        lock(SyncRoot)
        {
            var batchItems = new List<HNSWPoint>(batchSize);
            for (int i = 0; i < batches; i++)
            {
                batchItems.Clear();
                int start = i * batchSize;
                int end = Math.Min(start + batchSize, total);
                for(int j = start; j < end; j++)
                {
                    batchItems.Add(items[j]);
                }
                AddCore(batchItems);
            }
        }
    }

    /// <summary>
    /// Remove item with given index from graph structure
    /// </summary>
    public void Remove(int itemIndex)
    {
        var item = data.Nodes[itemIndex];
        if (item == null) return;

        // If we have backend, we need to track all changes and save them later
        DirtyNodes? dirtyNodes = null;
        if (Backend != null)
        {
            dirtyNodes = new DirtyNodes();
            dirtyNodes.Add(item);
        }

        for (int layer = item.MaxLayer; layer >= 0; layer--)
        {
            data.LockNodeNeighbourhood(item, layer);
            connector.RemoveConnectionsAtLayer(item, layer, dirtyNodes);
            if (layer == 0) data.RemoveItem(itemIndex);
            data.UnlockNodeNeighbourhood(item, layer);
        }

        if (dirtyNodes != null && dirtyNodes.Count > 0)
        {
            Backend?.UpdateNodes(dirtyNodes.GetNodes());
        }
    }

    public void Remove(HNSWPoint p)
    {
        int itemIndex = p.Id;
        Remove(itemIndex);
    }

    /// <summary>
    /// Remove collection of items associated with indexes
    /// </summary>
    public void Remove(List<int> indexes)
    {
        Parallel.For(0, indexes.Count, (i) =>
        {
            Remove(indexes[i]);
        });
    }

    public int Count
    {
        get { return data.Items.Count; }
    }

    /// <summary>
    /// Get list of items inserted into the graph structure
    /// </summary>
    public List<HNSWPoint> Items()
    {
        return data.Items.Values.ToList();
    }

    /// <summary>
    /// Directly access graph structure at given layer
    /// </summary>
    public GraphLayer GetGraphLayer(int layer)
    {
        return new GraphLayer(data.Nodes, layer);
    }

    /// <summary>
    /// Get K nearest neighbours of query point. 
    /// Optionally probide filter function to ignore certain labels.
    /// Layer parameters indicates at which layer search should be performed (0 - base layer)
    /// </summary>
    public List<KNNResult> Query(HNSWPoint query, int k, Func<HNSWPoint, bool>? filterFnc = null, int layer = 0)
    {
        if (data.Nodes.Count - data.RemovedIndexes.Count <= 0) return new List<KNNResult>();

        Func<int, bool> indexFilter = _ => true;
        if (filterFnc is not null)
            indexFilter = (index) => filterFnc(data.Items[index]);


        float queryDistance(int nodeId, HNSWPoint label)
        {
            return distanceFnc(data.Items[nodeId], label);
        }

        var neighboursAmount = Math.Max(parameters.MinNN, k);
        var distCalculator = new DistanceCalculator<HNSWPoint>(queryDistance, query);
        var ep = navigator.FindEntryPoint(layer, distCalculator);
        var topCandidates = navigator.SearchLayer(ep.Id, layer, neighboursAmount, distCalculator, indexFilter);

        if (k < neighboursAmount)
        {
            return topCandidates.OrderBy(c => c.Dist).Take(k).ToList().ConvertAll(c => new KNNResult(c.Id, data.Items[c.Id], c.Dist));
        }
        var list = topCandidates.ConvertAll(c => new KNNResult(c.Id, data.Items[c.Id], c.Dist));
        list.Sort();
        return list;
    }

    /// <summary>
    /// Get statistical information about graph structure
    /// </summary>
    public HNSWInfo GetInfo()
    {
        return new HNSWInfo(data.Nodes, data.RemovedIndexes, data.GetTopLayer());
    }

    /// <summary>
    /// Serialize the graph snapshot image to a file.
    /// </summary>
    public void Serialize(string filePath, int sliceMaxCount = 500000)
    {
        using(FileStream fs = new FileStream(filePath, FileMode.Create))
        {
            Serialize(fs);
        }
    }

    public void Serialize(Stream stream)
    {
        lock(SyncRoot)
        {
            var snapshot = new HNSWIndexSnapshot(parameters, data);
            snapshot.Serialize(stream);
        }
    }

    public byte[] Serialize()
    {
        using(MemoryStream ms = new MemoryStream())
        {
            Serialize(ms);
            return ms.ToArray();
        }
    }

    /// <summary>
    /// 序列化到压缩文件。如果数据量比较大，则分片存储（需要控制每片序列化后的尺寸不超过 2GB），如果是 512维度的向量，sliceMaxCount 可以设置为 500000。
    /// </summary>
    /// <param name="zipFilePath">压缩文件的路径</param>
    /// <param name="indexName">索引在zip文件中存储的索引名称</param>
    /// <param name="sliceMaxCount"></param>
    public void SerializeToZipFile(string zipFilePath, string indexName = "default", int sliceMaxCount = 500000)
    {
        lock (SyncRoot)
        {
            HNSWIndexZipFileSerializer.Serialize(this, zipFilePath, indexName, sliceMaxCount);
        }
    }

    /// <summary>
    /// 从压缩文件中反序列化 HNSWIndex。
    /// </summary>
    /// <param name="distFnc"></param>
    /// <param name="zipFilePath"></param>
    /// <param name="indexName">索引名称</param>
    /// <returns></returns>
    public static HNSWIndex? DeserializeFromZipFile(Func<HNSWPoint, HNSWPoint, float> distFnc, string zipFilePath, string indexName = "default")
    {
        var slices = HNSWIndexZipFileSerializer.Deserialize(zipFilePath,indexName);
        if (slices == null) return null;
        slices.Merge();
        return new HNSWIndex(distFnc, slices.Snapshot);
    }

    public static HNSWIndex? DeserializeFromZipFile<T>(Func<HNSWPoint, HNSWPoint, float> distFnc, string zipFilePath, string indexName = "default") where T:HNSWPoint,new()
    {
        var slices = HNSWIndexZipFileSerializer.Deserialize(zipFilePath, indexName, ()=> new T());
        if (slices == null) return null;
        slices.Merge();
        return new HNSWIndex(distFnc, slices.Snapshot);
    }

    /// <summary>
    /// Reconstruct the graph from a serialized snapshot image.
    /// </summary>
    public static HNSWIndex? Deserialize(Func<HNSWPoint, HNSWPoint, float> distFnc, string filePath, Func<HNSWPoint>? onCreate = null)
    {
        using(FileStream fs = new FileStream(filePath, FileMode.Open))
        {
            return Deserialize(distFnc, fs, onCreate);
        }
    }

    public static HNSWIndex? Deserialize(Func<HNSWPoint, HNSWPoint, float> distFnc, byte[] buff, Func<HNSWPoint>? onCreate = null)
    {
        using (MemoryStream ms = new MemoryStream(buff))
            return Deserialize(distFnc, ms, onCreate);
    }

    /// <summary>
    /// 有的 stream 可能没有长度信息，所以可以传入 length 参数。
    /// </summary>
    /// <param name="distFnc"></param>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static HNSWIndex? Deserialize(Func<HNSWPoint, HNSWPoint, float> distFnc, Stream stream, Func<HNSWPoint>? onCreate = null)
    {
        var snapshot = HNSWIndexSnapshot.Deserialize(stream, onCreate);
        return snapshot == null ? null : new HNSWIndex(distFnc, snapshot);
    }

    private void OnDataResized(object? sender, ReallocateEventArgs e)
    {
        navigator.OnReallocate(e.NewCapacity);
    }

    /// <summary>
    /// 判断两个 HNSWIndex 是否相等。如果索引比较大，本方法将非常慢。注意，这里并不比较 Backend。
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(HNSWIndex? other)
    {
        if(other == null) return false;

        var s1 = HNSWIndexSnapshot.CreateFrom(this);
        var s2 = HNSWIndexSnapshot.CreateFrom(other);
        return s1.Equals(s2);
    }
}
