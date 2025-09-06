using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NewBeeDB;

/// <summary>
/// Class storing the data containers for HNSW index.
/// All lock related members are ommitted from serialization 
/// and should be initialized in deserialization constructor.
/// </summary>
public class GraphData
{
    internal IBackend? Backend { get; set; }

    internal event EventHandler<ReallocateEventArgs>? Reallocated;

    internal object indexLock = new object();

    internal List<Node> Nodes { get; private set; }

    internal ConcurrentDictionary<int, HNSWPoint> Items { get; private set; }

    private object removedIndexesLock = new object();

    internal Queue<int> RemovedIndexes { get; private set; }

    internal object NeighbourhoodBitmapLock = new object();

    internal List<bool> NeighbourhoodBitmap { get; private set; }

    internal object entryPointLock = new object();

    internal int _entryPointId = -1;

    internal int EntryPointId
    {
        get { return _entryPointId; }
        set
        {
            if(_entryPointId != value)
            {
                _entryPointId = value;
                Backend?.TrySave(() => Backend.SaveEntryPoint(value));
            }
        }
    }

    internal Node EntryPoint => Nodes[EntryPointId];

    internal int _capacity;
    internal int Capacity
    {
        get { return _capacity; }
        set
        {
            if(_capacity != value)
            {
                _capacity = value;
                Backend?.TrySave(() => Backend.SaveCapacity(value));
            }
        }
    }

    private object rngLock = new object();

    private Random rng;

    private double distRate;

    private int maxEdges;

    private Func<HNSWPoint, HNSWPoint, float> distanceFnc;

    /// <summary>
    /// Constructor for the graph data.
    /// </summary>
    public GraphData(Func<HNSWPoint, HNSWPoint, float> distance, HNSWParameters parameters)
    {
        distanceFnc = distance;
        rng = parameters.RandomSeed < 0 ? new Random() : new Random(parameters.RandomSeed);
        distRate = parameters.DistributionRate;
        maxEdges = parameters.MaxEdges;
        Capacity = parameters.CollectionSize;

        RemovedIndexes = new Queue<int>();
        Nodes = new List<Node>(parameters.CollectionSize);
        NeighbourhoodBitmap = new List<bool>(parameters.CollectionSize);
        Items = new ConcurrentDictionary<int, HNSWPoint>(65536, parameters.CollectionSize); // 2^16 amount of locks
    }

    /// <summary>
    /// Constructor for the graph data from serialization snapshot.
    /// </summary>
    public GraphData(GraphDataSnapshot snapshot, Func<HNSWPoint, HNSWPoint, float> distance, HNSWParameters parameters)
    {
        distanceFnc = distance;
        rng = parameters.RandomSeed < 0 ? new Random() : new Random(parameters.RandomSeed);
        distRate = parameters.DistributionRate;
        maxEdges = parameters.MaxEdges;

        Nodes = snapshot.Nodes ?? new List<Node>(parameters.CollectionSize);
        Items = snapshot.Items ?? new ConcurrentDictionary<int, HNSWPoint>(65536, parameters.CollectionSize);
        RemovedIndexes = snapshot.RemovedIndexes ?? new Queue<int>();
        EntryPointId = snapshot.EntryPointId;
        Capacity = snapshot.Capacity;

        NeighbourhoodBitmap = new List<bool>(Capacity);
        for (int i = 0; i < Nodes.Count; i++)
            NeighbourhoodBitmap.Add(false);
    }

    /// <summary>
    /// Add new item to the graph.
    /// </summary>
    internal int AddItem(HNSWPoint item)
    {
        // Search for empty spot first
        int vacantId = -1;
        lock (removedIndexesLock)
        {
            if (RemovedIndexes.Count > 0)
            {
                vacantId = RemovedIndexes.Dequeue();
                Backend?.TrySave(
                    () =>
                    {
                        Backend.RemoveRemovedIndex(vacantId);
                    }
                );
            }
        }

        void SetId(int id, HNSWPoint item)
        {
            item.Id = id;
            Backend?.TrySave(
            () =>
            {
                Backend.SavePoint(item);
            }
        );
        }

        if (vacantId >= 0)
        {
            Nodes[vacantId] = NewNode(vacantId);
            Items.TryAdd(vacantId, item);
            SetId(vacantId, item);
            return vacantId;
        }

        // Allocate new spot
        vacantId = Nodes.Count;
        Nodes.Add(NewNode(vacantId));
        NeighbourhoodBitmap.Add(false);
        Items.TryAdd(vacantId, item);
        SetId(vacantId, item);
        if (Nodes.Count > Capacity)
        {
            Capacity = Nodes.Capacity + Capacity/2;
            Reallocated?.Invoke(this, new ReallocateEventArgs(Capacity));
        }
        return vacantId;
    }

    /// <summary>
    /// Remove item from lookup table and mark its index as free.
    /// It is still possible to access the node with this index, until new item takes its place.
    /// </summary>
    internal void RemoveItem(int itemId)
    {
        if(Items.TryRemove(itemId, out HNSWPoint? p))
        {
            Backend?.TrySave(
                () =>
                {
                    Backend.RemovePoint(p.Label);
                }
            );
        }

        lock (removedIndexesLock)
        {
            RemovedIndexes.Enqueue(itemId);
        }

        Backend?.TrySave(
            () =>
            {
                Backend.AddRemovedIndex(itemId);
            }
        );
    }

    /// <summary>
    /// Try to move the role of entry point to neighbor at given layer
    /// </summary>
    internal bool TryReplaceEntryPoint(int layer)
    {
        if (EntryPoint.OutEdges[layer].Count > 0)
        {
            var neighbourId = EntryPoint.OutEdges[layer].MaxBy(id => Nodes[id].OutEdges.Count);
            EntryPointId = neighbourId;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get the maximum layer of the graph.
    /// </summary>
    internal int GetTopLayer()
    {
        return Nodes[EntryPointId].MaxLayer;
    }

    /// <summary>
    /// Constriction function for new node structure.
    /// </summary>
    private Node NewNode(int index)
    {
        float random = 0;
        lock (rngLock)
        {
            random = rng.NextSingle();
        }

        int topLayer = (int)(-Math.Log(random) * distRate);

        var outEdges = new List<List<int>>(topLayer + 1);
        var inEdges = new List<List<int>>(topLayer + 1);

        for (int layer = 0; layer <= topLayer; layer++)
        {
            int maxEdges = MaxEdges(layer);
            outEdges.Add(new List<int>(maxEdges + 1));
            inEdges.Add(new List<int>(maxEdges + 1));
        }

        var node = new Node
        {
            Id = index,
            OutEdges = outEdges,
            InEdges = inEdges,
        };

        Backend?.TrySave(
            () =>
            {
                Backend.SaveNode(node);
            }
        );

        return node;
    }

    /// <summary>
    /// Wait until all neighbours of the node at given layer are free
    /// Then lock all those neighbours until processing is done
    /// </summary>
    internal void LockNodeNeighbourhood(Node node, int layer)
    {
        lock (NeighbourhoodBitmapLock)
        {
            while (NeighbourhoodIsBusy(node, layer))
            {
                Monitor.Wait(NeighbourhoodBitmapLock);
            }

            NeighbourhoodBitmap[node.Id] = true;
            foreach (var neighbourId in node.OutEdges[layer])
                NeighbourhoodBitmap[neighbourId] = true;
            foreach (var neighbourId in node.InEdges[layer])
                NeighbourhoodBitmap[neighbourId] = true;
        }
    }

    /// <summary>
    /// Free all nodes locked with LockNodeNeighbourhood method
    /// </summary>
    internal void UnlockNodeNeighbourhood(Node node, int layer)
    {
        lock (NeighbourhoodBitmapLock)
        {
            NeighbourhoodBitmap[node.Id] = false;
            foreach (var neighbourId in node.OutEdges[layer])
                NeighbourhoodBitmap[neighbourId] = false;
            foreach (var neighbourId in node.InEdges[layer])
                NeighbourhoodBitmap[neighbourId] = false;
            Monitor.PulseAll(NeighbourhoodBitmapLock);
        }
    }

    /// <summary>
    /// Check if the node and its neighbours are busy at given layer.
    /// This method is used to acquire lock for region of the graph associated with the node.
    /// </summary>
    internal bool NeighbourhoodIsBusy(Node node, int layer)
    {
        bool result = NeighbourhoodBitmap[node.Id];

        try
        {
            foreach (var neighbourId in node.OutEdges[layer])
            {
                result |= NeighbourhoodBitmap[neighbourId];
            }
            foreach (var neighbourId in node.InEdges[layer])
            {
                result |= NeighbourhoodBitmap[neighbourId];
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception)
        {
            throw;
        }
        return result;
    }

    /// <summary>
    /// Get maximum number of edges at given layer.
    /// </summary>
    internal int MaxEdges(int layer)
    {
        return layer == 0 ? maxEdges * 2 : maxEdges;
    }

    /// <summary>
    /// Wrapper for distance function working on indexes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float Distance(int a, int b)
    {
        return distanceFnc(Items[a], Items[b]);
    }
}