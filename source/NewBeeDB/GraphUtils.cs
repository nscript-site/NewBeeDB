namespace NewBeeDB;

public class GraphUtils
{
    [Obsolete("BuildRemovedIndexes 已弃用，该实现机制有错误", true)]
    public static Queue<int> BuildRemovedIndexes(List<Node> nodes)
    {
        if(nodes.Count == 0)
            return new Queue<int>();

        var arr = new int[nodes.Count];
        var maxId = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            int id = nodes[i].Id;
            arr[i] = id;
            if (id > maxId) maxId = id;
        }

        if(maxId + 1 - nodes.Count <= 0)
            return new Queue<int>();

        var map = new byte[maxId + 1];
        for (int i = 0; i < nodes.Count; i++)
        {
            map[arr[i]] = 1;
        }

        var queue = new Queue<int>(Math.Max(4,maxId - nodes.Count));
        for (int i = 0; i < map.Length; i++)
        {
            if (map[i] == 0)
                queue.Enqueue(i);
        }

        return queue;
    }
}
