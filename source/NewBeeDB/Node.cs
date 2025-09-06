namespace NewBeeDB;

public partial class Node
{
    public int Id;

    public object OutEdgesLock { get; } = new();

    public object InEdgesLock { get; } = new();

    public List<List<int>> OutEdges { get; set; } = default!;

    public List<List<int>> InEdges { get; set; } = default!;

    public int MaxLayer => OutEdges.Count - 1;

    public void Serialize(Stream stream)
    {
        BinarySerializer.SerializeInt32(stream, Id);
        lock (OutEdgesLock)
        {
            BinarySerializer.SerializeListOfLists_Int32(stream, OutEdges);
        }
        lock(InEdgesLock)
        {
            BinarySerializer.SerializeListOfLists_Int32(stream, InEdges);
        }
    }

    public Node()
    {
        InEdges = new List<List<int>>();
        OutEdges = new List<List<int>>();
    }

    public Node(int id, List<List<int>> outEdges, List<List<int>> inEdges)
    {
        Id = id;
        OutEdges = outEdges;
        InEdges = inEdges;
    }

    public static Node Deserialize(Stream stream)
    {
        var node = new Node();
        node.Id = BinarySerializer.DeserializeInt32(stream);
        node.OutEdges = BinarySerializer.DeserializeListOfLists_Int32(stream);
        node.InEdges = BinarySerializer.DeserializeListOfLists_Int32(stream);
        return node;
    }

    public bool Equals(Node node)
    {
        if (this.Id != node.Id) return false;
        if (this.OutEdges.Count != node.OutEdges.Count) return false;
        if (this.InEdges.Count != node.InEdges.Count) return false;
        
        for(int i = 0; i < this.OutEdges.Count; i++)
        {
            var l1 = this.OutEdges[i];
            var l2 = node.OutEdges[i];
            if (l1.Count != l2.Count) return false;
            for(int j = 0; j < l1.Count; j++)
            {
                var ll1 = l1[j];
                var ll2 = l2[j];
                if (ll1 != ll2) return false;
            }
        }

        for (int i = 0; i < this.InEdges.Count; i++)
        {
            var l1 = this.InEdges[i];
            var l2 = node.InEdges[i];
            if (l1.Count != l2.Count) return false;
            for (int j = 0; j < l1.Count; j++)
            {
                var ll1 = l1[j];
                var ll2 = l2[j];
                if (ll1 != ll2) return false;
            }
        }

        return true;
    }
}