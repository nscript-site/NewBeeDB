namespace NewBeeDB.Backends.Test;

[TestClass]
public class SqliteBackendTest
{
    #region 派生类用于测试 protected 方法 
    public class TestableSqliteBackend : SqliteBackend
    {
        public TestableSqliteBackend(SqliteBackend backend)
            : base(":memory:") // 只用于测试，实际复用连接
        {
            this.HNSWPointSqliteSerializer = backend.HNSWPointSqliteSerializer;
            // 反射复制连接字段
            var connField = typeof(SqliteBackend).GetField("conn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            connField.SetValue(this, connField.GetValue(backend));
        }

        public List<Node> CallGetAllNodes() => base.GetAllNodes();
        public List<KeyValuePair<int, HNSWPoint>> CallGetAllPoints() => base.GetAllPoints();
        public Queue<int> CallGetAllRemovedIndexes() => base.GetAllRemovedIndexes();
    }
    #endregion

    private SqliteBackend CreateBackend()
    {
        // 使用内存数据库，避免文件残留
        return new SqliteBackend(":memory:");
    }

    private HNSWPoint CreatePoint(string label = "p1", float[]? data = null)
    {
        return new HNSWPoint
        {
            Label = label,
            Data = data ?? new float[] { 1.0f, 2.0f }
        };
    }

    private Node CreateNode(int id = 1)
    {
        return new Node(id, new List<List<int>> { new List<int> { 1, 2 } }, new List<List<int>> { new List<int> { 3 } });
    }

    [TestMethod]
    public void AddPoint_And_RemovePoint_Works()
    {
        using var backend = CreateBackend();
        var point = CreatePoint("test1", new float[] { 1, 2, 3 });
        backend.SavePoint(point);

        // 验证插入
        using (var checkCmd = backend.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM hnsw_default_points WHERE Label = @label";
            checkCmd.Parameters.AddWithValue("@label", "test1");
            var count = (long)checkCmd.ExecuteScalar();
            Assert.AreEqual(1, count);
        }

        backend.RemovePoint("test1");

        // 验证删除
        using (var checkCmd = backend.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM hnsw_default_points WHERE Label = @label";
            checkCmd.Parameters.AddWithValue("@label", "test1");
            var count = (long)checkCmd.ExecuteScalar();
            Assert.AreEqual(0, count);
        }
    }

    [TestMethod]
    public void AddNode_And_RemoveNode_Works()
    {
        using var backend = CreateBackend();
        var node = CreateNode(123);
        backend.SaveNode(node);

        // 验证插入
        using (var checkCmd = backend.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM hnsw_default_nodes WHERE Id = @id";
            checkCmd.Parameters.AddWithValue("@id", 123);
            var count = (long)checkCmd.ExecuteScalar();
            Assert.AreEqual(1, count);
        }

        backend.RemoveNode(123);

        // 验证删除
        using (var checkCmd = backend.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM hnsw_default_nodes WHERE Id = @id";
            checkCmd.Parameters.AddWithValue("@id", 123);
            var count = (long)checkCmd.ExecuteScalar();
            Assert.AreEqual(0, count);
        }
    }

    [TestMethod]
    public void UpdateNode_Works()
    {
        using var backend = CreateBackend();
        var node = CreateNode(1);
        backend.SaveNode(node);
        backend.UpdateNode(node);

        // 验证更新（这里只能验证存在，具体内容需反序列化）
        using (var checkCmd = backend.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM hnsw_default_nodes WHERE Id = @id";
            checkCmd.Parameters.AddWithValue("@id", 1);
            var count = (long)checkCmd.ExecuteScalar();
            Assert.AreEqual(1, count);
        }
    }

    [TestMethod]
    public void UpdateNodes_Works()
    {
        using var backend = CreateBackend();
        var node1 = CreateNode(1);
        var node2 = CreateNode(2);
        backend.SaveNode(node1);
        backend.SaveNode(node2);

        backend.UpdateNodes(new[] { node1, node2 });

        // 验证批量更新（这里只能验证存在，具体内容需反序列化）
        using (var checkCmd = backend.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM hnsw_default_nodes WHERE Id IN (1,2)";
            var count = (long)checkCmd.ExecuteScalar();
            Assert.AreEqual(2, count);
        }
    }

    [TestMethod]
    public void SaveParameters_And_GetConfig_Works()
    {
        using var backend = CreateBackend();
        var parameters = new HNSWParameters
        {
            MaxEdges = 16,
            DistributionRate = 1.5,
            MinNN = 5,
            MaxCandidates = 50,
            CollectionSize = 1000,
            RandomSeed = 42
        };
        backend.SaveParameters(parameters);

        var loaded = backend.GetConfig(NewBeeDB.Backends.SqliteBackend.Key_Parameters, stream =>
        {
            int flag = BinarySerializer.DeserializeInt32(stream);
            if (flag == 0) return null;
            return HNSWParameters.Deserialize(stream);
        });

        Assert.IsNotNull(loaded);
        Assert.AreEqual(parameters.MaxEdges, loaded.MaxEdges);
        Assert.AreEqual(parameters.DistributionRate, loaded.DistributionRate);
        Assert.AreEqual(parameters.MinNN, loaded.MinNN);
        Assert.AreEqual(parameters.MaxCandidates, loaded.MaxCandidates);
        Assert.AreEqual(parameters.CollectionSize, loaded.CollectionSize);
        Assert.AreEqual(parameters.RandomSeed, loaded.RandomSeed);
    }

    [TestMethod]
    public void SaveEntryPoint_And_GetConfig_Works()
    {
        using var backend = CreateBackend();
        int entryPointId = 12345;
        backend.SaveEntryPoint(entryPointId);

        var loaded = backend.GetConfig(NewBeeDB.Backends.SqliteBackend.Key_EntryPoint, stream =>
            BinarySerializer.DeserializeInt32(stream)
        );

        Assert.AreEqual(entryPointId, loaded);
    }

    [TestMethod]
    public void SaveCapacity_And_GetConfig_Works()
    {
        using var backend = CreateBackend();

        var loaded1 = backend.GetConfig(NewBeeDB.Backends.SqliteBackend.Key_Capacity, (Stream stream) => { return (int?)null; }
        );

        Assert.AreEqual(null, loaded1);

        var loaded2 = backend.GetConfig<int?>(NewBeeDB.Backends.SqliteBackend.Key_Capacity, stream =>
            BinarySerializer.DeserializeInt32(stream)
        );

        Assert.AreEqual(null, loaded2);
    }

    [TestMethod]
    public void GetAllNodes_Works()
    {
        using var backend = new SqliteBackend(":memory:");
        var node1 = new Node(1, new List<List<int>> { new List<int> { 2 } }, new List<List<int>> { new List<int> { 3 } });
        var node2 = new Node(2, new List<List<int>> { new List<int> { 1 } }, new List<List<int>> { new List<int> { 4 } });
        backend.SaveNode(node1);
        backend.SaveNode(node2);

        // 通过派生类暴露 protected 方法
        var testBackend = new TestableSqliteBackend(backend);
        var nodes = testBackend.CallGetAllNodes();

        Assert.AreEqual(2, nodes.Count);
        Assert.IsTrue(nodes.Exists(n => n.Id == 1));
        Assert.IsTrue(nodes.Exists(n => n.Id == 2));
    }

    [TestMethod]
    public void GetAllPoints_Works()
    {
        using var backend = new SqliteBackend(":memory:");
        var point1 = new HNSWPoint { Label = "p1", Data = new float[] { 1, 2 } };
        var point2 = new HNSWPoint { Label = "p2", Data = new float[] { 3, 4 } };
        backend.SavePoint(point1, 0);
        backend.SavePoint(point2, 1);

        // 通过派生类暴露 protected 方法
        var testBackend = new TestableSqliteBackend(backend);
        var points = testBackend.CallGetAllPoints();

        Assert.AreEqual(2, points.Count);
        Assert.IsTrue(points.Exists(p => p.Value.Label == "p1"));
        Assert.IsTrue(points.Exists(p => p.Value.Label == "p2"));
    }

    [TestMethod]
    public void AddRemovedIndex_And_GetAllRemovedIndexes_Works()
    {
        using var backend = CreateBackend();
        backend.AddRemovedIndex(10);
        backend.AddRemovedIndex(20);
        backend.AddRemovedIndex(10); // 重复添加

        // 通过 protected 方法获取所有已移除索引
        var testBackend = new TestableSqliteBackend(backend);
        var removedIndexes = testBackend.CallGetAllRemovedIndexes();

        Assert.AreEqual(2, removedIndexes.Count);
        Assert.IsTrue(removedIndexes.Contains(10));
        Assert.IsTrue(removedIndexes.Contains(20));
    }

    [TestMethod]
    public void RemoveRemovedIndex_And_GetAllRemovedIndexes_Works()
    {
        using var backend = CreateBackend();
        backend.AddRemovedIndex(100);
        backend.AddRemovedIndex(200);

        backend.RemoveRemovedIndex(100);

        var testBackend = new TestableSqliteBackend(backend);
        var removedIndexes = testBackend.CallGetAllRemovedIndexes();

        Assert.AreEqual(1, removedIndexes.Count);
        Assert.IsFalse(removedIndexes.Contains(100));
        Assert.IsTrue(removedIndexes.Contains(200));
    }

    [TestMethod]
    public void Load_Works_With_FullAndPartialData()
    {
        // 1. 创建 Backend 为 SqliteBackend 的 HNSWIndex, 记为 hnsw1
        using var backend = CreateBackend();
        var parameters = new HNSWParameters();

        // 余弦距离
        Func<HNSWPoint, HNSWPoint, float> distFnc = HNSWPoint.CosineMetricUnitCompute;
        var hnsw1 = new HNSWIndex(distFnc, parameters, backend);

        // 2. 生成 100 条随机数据并插入
        var points = HNSWPoint.Random(32, 100, true);
        foreach (var p in points)
            hnsw1.Add(p);

        // 3. 从 SqliteBackend 中 Load 新的 HNSWIndex，记为 hnsw2
        var hnsw2 = backend.Load(distFnc);

        // 4. 判断 hnsw1 和 hnsw2 是否相等
        Assert.IsNotNull(hnsw2);
        Assert.IsTrue(hnsw1.Equals(hnsw2), "Loaded index should be equal to the original after full insert.");

        // 5. 从 hnsw1 中随机删除 10 条记录
        var rnd = new Random(123);
        var allIndexes = Enumerable.Range(0, hnsw1.Count).ToList();
        var removeIndexes = allIndexes.OrderBy(_ => rnd.Next()).Take(10).ToList();
        hnsw1.Remove(removeIndexes);

        // 6. 从 SqliteBackend 中 Load 新的 HNSWIndex，记为 hnsw3
        var hnsw3 = backend.Load(distFnc);

        // 7. 判断 hnsw1 和 hnsw3 是否相等
        Assert.IsNotNull(hnsw3);
        Assert.IsTrue(hnsw1.Equals(hnsw3), "Loaded index should be equal to the original after deletions.");
    }
}
