using System.Collections.Concurrent;

namespace NewBeeDB;

/// <summary>
/// Wrapper for GraphData for serialization.
/// </summary>
public partial class GraphDataSnapshot
{
    public List<Node>? Nodes { get; set; }

    public ConcurrentDictionary<int, HNSWPoint>? Items { get; set; }

    public Queue<int>? RemovedIndexes { get; set; }

    public int EntryPointId = -1;

    public int Capacity;

    public GraphDataSnapshot() { }

    public GraphDataSnapshot(GraphData data)
    {
        Nodes = data.Nodes;
        Items = data.Items;
        RemovedIndexes = data.RemovedIndexes;
        EntryPointId = data.EntryPointId;
        Capacity = data.Capacity;
    }

    public void Serialize(Stream stream)
    {
        BinarySerializer.SerializeInt32(stream, EntryPointId);
        BinarySerializer.SerializeInt32(stream, Capacity);
        SerializeItems(stream);
        SerializeNodes(stream);
        SerializeRemovedIndexes(stream);
    }

    public static GraphDataSnapshot Deserialize(Stream stream)
    {
        var snapshot = new GraphDataSnapshot();
        snapshot.EntryPointId = BinarySerializer.DeserializeInt32(stream);
        snapshot.Capacity = BinarySerializer.DeserializeInt32(stream);
        snapshot.Items = DeserializeItems(stream);
        snapshot.Nodes = DeserializeNodes(stream);
        snapshot.RemovedIndexes = DeserializeRemovedIndexes(stream);
        return snapshot;
    }

    public void SerializeItems(Stream stream)
    {
        int count = Items?.Count ?? 0;
        BinarySerializer.SerializeInt32(stream, count);
        if(Items != null)
        {
            foreach (var kp in Items)
            {
                BinarySerializer.SerializeInt32(stream, kp.Key);
                kp.Value.Serialize(stream);
            }
        }
    }

    public static ConcurrentDictionary<int, HNSWPoint> DeserializeItems(Stream stream)
    {
        int count = BinarySerializer.DeserializeInt32(stream);
        var items = new ConcurrentDictionary<int, HNSWPoint>(4,count);
        for (int i = 0; i < count; i++)
        {
            int index = BinarySerializer.DeserializeInt32(stream);
            var point = HNSWPoint.Deserialize(stream);
            items[index] = point;
        }
        return items;
    }

    public void SerializeNodes(Stream stream)
    {
        int count = Nodes?.Count ?? 0;
        BinarySerializer.SerializeInt32(stream, count);
        if (Nodes != null)
        {
            foreach (var node in Nodes)
            {
                node.Serialize(stream);
            }
        }
    }

    public static List<Node> DeserializeNodes(Stream stream)
    {
        int count = BinarySerializer.DeserializeInt32(stream);
        var nodes = new List<Node>(count);
        for (int i = 0; i < count; i++)
        {
            var node = Node.Deserialize(stream);
            nodes.Add(node);
        }
        return nodes;
    }

    public void SerializeRemovedIndexes(Stream stream)
    {
        int count = RemovedIndexes?.Count ?? 0;
        BinarySerializer.SerializeInt32(stream, count);
        if (RemovedIndexes != null)
        {
            foreach (var index in RemovedIndexes)
            {
                BinarySerializer.SerializeInt32(stream, index);
            }
        }
    }

    public static Queue<int> DeserializeRemovedIndexes(Stream stream)
    {
        int count = BinarySerializer.DeserializeInt32(stream);
        var removedIndexes = new Queue<int>(count);
        for (int i = 0; i < count; i++)
        {
            int index = BinarySerializer.DeserializeInt32(stream);
            removedIndexes.Enqueue(index);
        }
        return removedIndexes;
    }

    public bool Equals(GraphDataSnapshot snap)
    {
        if (this.Capacity != snap.Capacity) return false;

        var nodes1 = this.Nodes ?? new List<Node>();
        var nodes2 = snap.Nodes ?? new List<Node>();

        if (nodes1.Count != nodes2.Count) return false;
        for (int i = 0; i < nodes1.Count; i++)
            if (nodes1[i].Equals(nodes2[i]) == false) return false;

        var dict1 = Items ?? new ConcurrentDictionary<int, HNSWPoint>();
        var dict2 = snap.Items ?? new ConcurrentDictionary<int, HNSWPoint>();

        if (dict1.Count != dict2.Count) return false;
        foreach(var kv in dict1)
        {
            if (dict2.TryGetValue(kv.Key, out HNSWPoint? v) == false) return false;
            if (v == null || kv.Value.Equals(v) == false) return false;
        }

        var removes1 = RemovedIndexes?.ToList() ?? new List<int>();
        var removes2 = snap.RemovedIndexes?.ToList() ?? new List<int>();

        if (removes1.Count != removes2.Count) return false;

        removes1.Sort();
        removes2.Sort();
        for (int i = 0; i < removes1.Count; i++)
        {
            if(removes2[i] != removes1[i]) return false;
        }
        
        return true;
    }
}