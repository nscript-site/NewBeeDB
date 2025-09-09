using static NewBeeDB.Backends.Test.SqliteBackendTest;

namespace NewBeeDB.Backends.Test;

[TestClass]
public class TimedHNSWPointSqliteBackendTest
{
    private SqliteBackend CreateBackend()
    {
        // 使用内存数据库，避免文件残留
        return new SqliteBackend(":memory:", hnswPointSerializer: new TimedHNSWPointSqliteSerializer());
    }

    [TestMethod]
    public void GetAllPoints_Works()
    {
        using var backend = CreateBackend();
        var point1 = new TimedHNSWPoint { Label = "p1", Data = new float[] { 1, 2 }, CreatedTime = DateTime.Now };
        var point2 = new TimedHNSWPoint { Label = "p2", Data = new float[] { 3, 4 } };
        backend.SavePoint(point1, 0);
        backend.SavePoint(point2, 1);

        // 通过派生类暴露 protected 方法
        var testBackend = new TestableSqliteBackend(backend);
        var points = testBackend.CallGetAllPoints();

        var p1 = points.Find(p => p.Value.Label == "p1").Value as TimedHNSWPoint;
        Assert.IsNotNull(p1);
        Assert.AreEqual(point1.CreatedTime.Value.ToFileTimeUtc(), p1.CreatedTime!.Value.ToFileTimeUtc());

        Assert.AreEqual(2, points.Count);
        Assert.IsTrue(points.Exists(p => p.Value.Label == "p1"));
        Assert.IsTrue(points.Exists(p => p.Value.Label == "p2"));
    }
}
