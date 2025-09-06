namespace NewBeeDB;

public class HNSWIndexSlice
{
    public List<Node> Nodes { get; set; } = default!;

    public List<KeyValuePair<int, HNSWPoint>> Items { get; set; } = default!;

    public HNSWIndexSlice()
    {
        Nodes = new List<Node>();
        Items = new List<KeyValuePair<int, HNSWPoint>>();
    }

    public HNSWIndexSlice(int nodesCapacity, int itemsCapacity)
    {
        Nodes = new List<Node>(nodesCapacity);
        Items = new List<KeyValuePair<int, HNSWPoint>>(itemsCapacity);
    }

    public void Serialize(Stream stream)
    {
        BinarySerializer.SerializeInt32(stream, Nodes.Count);
        foreach (var node in Nodes)
        {
            node.Serialize(stream);
        }
        BinarySerializer.SerializeInt32(stream, Items.Count);
        foreach (var item in Items)
        {
            BinarySerializer.SerializeInt32(stream, item.Key);
            item.Value.Serialize(stream);
        }
    }

    /// <summary>
    /// 只反序列化节点数量
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static int DeserializeNodeCount(Stream stream)
    {
        return BinarySerializer.DeserializeInt32(stream);
    }

    public static HNSWIndexSlice Deserialize(Stream stream)
    {
        var slice = new HNSWIndexSlice();
        int nodeCount = BinarySerializer.DeserializeInt32(stream);
        slice.Nodes.Capacity = Math.Max(slice.Nodes.Capacity, nodeCount);
        for (int i = 0; i < nodeCount; i++)
        {
            var node = Node.Deserialize(stream);
            slice.Nodes.Add(node);
        }
        int itemCount = BinarySerializer.DeserializeInt32(stream);
        slice.Items.Capacity = Math.Max(slice.Items.Capacity, itemCount);
        for (int i = 0; i < itemCount; i++)
        {
            int index = BinarySerializer.DeserializeInt32(stream);
            var point = HNSWPoint.Deserialize(stream);
            slice.Items.Add(new KeyValuePair<int, HNSWPoint>(index, point));
        }
        return slice;
    }
}
