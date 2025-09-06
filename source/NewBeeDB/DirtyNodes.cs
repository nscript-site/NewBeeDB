namespace NewBeeDB;

public class DirtyNodes
{
    private Dictionary<int, Node> Nodes { get; } = new Dictionary<int, Node>();

    public bool Add(Node node)
    {
        return Nodes.TryAdd(node.Id, node);
    }

    public int Count => Nodes.Count;

    public List<Node> GetNodes() => Nodes.Values.ToList();
}
