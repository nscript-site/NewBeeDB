namespace NewBeeDB;

internal class HNSWIndexSnapshotWithSlices
{
    public required HNSWIndexSnapshot Snapshot { get; init; }

    public List<HNSWIndexSlice> Slices { get; set; } = new List<HNSWIndexSlice>();

    public void Merge()
    {
        if (Snapshot.DataSnapshot == null)
            return;

        int nodesCountInSlices = 0;
        int itemsCountInSlices = 0;

        foreach(var item in Slices)
        {
            nodesCountInSlices += item.Nodes.Count;
            itemsCountInSlices += item.Items.Count;
        }

        var items = Snapshot.DataSnapshot.Items ??= new System.Collections.Concurrent.ConcurrentDictionary<int, HNSWPoint>(4, Math.Max(4, itemsCountInSlices));
        var nodes = Snapshot.DataSnapshot.Nodes ??= new List<Node>(Math.Max(4, nodesCountInSlices));

        if(nodes.Capacity < nodesCountInSlices)
        {
            nodes.Capacity = nodesCountInSlices;
        }

        foreach (var item in Slices)
        {
            nodes.AddRange(item.Nodes);
            if(item.Items.Count > 0)
            {
                foreach (var kvp in item.Items)
                {
                    items[kvp.Key] = kvp.Value; // Overwrite if exists
                }
            }
        }

        Slices.Clear();
    }

    public static HNSWIndexSnapshotWithSlices CreateFrom(HNSWIndex index, int sliceMaxCount)
    {
        var snapshot = HNSWIndexSnapshot.CreateFrom(index);
        var slices = new List<HNSWIndexSlice>();
        bool needSlice = sliceMaxCount > 0 && snapshot.DataSnapshot != null && snapshot.DataSnapshot.Items?.Count > sliceMaxCount;

        GraphDataSnapshot data = snapshot.DataSnapshot!;

        if (needSlice)
        {
            if(data.Items != null)
            {
                // chunk 在编程、数据库或文件处理中，指 “数据块”，即按一定规则划分的、具有独立意义的信息单元。
                // 例：
                //    文件传输时，数据会被分成多个 chunks 逐段发送。
                //    在区块链技术中，每个区块（block）可包含多个交易 chunk。
                foreach (var chunk in data.Items.Chunk(sliceMaxCount))
                {
                    var slice = new HNSWIndexSlice(chunk.Length, 0);
                    slice.Items.AddRange(chunk);
                    slices.Add(slice);
                }
            }

            if(data.Nodes != null)
            {
                foreach(var chunk in data.Nodes.Chunk(sliceMaxCount))
                {
                    var slice = new HNSWIndexSlice(chunk.Length, 0);
                    slice.Nodes.AddRange(chunk);
                    slices.Add(slice);
                }
            }

            // 清理原始数据快照。不能用 clear，因为清理后会导致原始数据快照中的引用失效。
            data.Items = null;
            data.Nodes = null;
        }

        return new HNSWIndexSnapshotWithSlices
        {
            Snapshot = snapshot,
            Slices = slices
        };
    }
}
