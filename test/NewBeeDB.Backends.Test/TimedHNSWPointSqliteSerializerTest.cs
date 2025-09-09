namespace NewBeeDB.Backends.Test;

[TestClass]
public class TimedHNSWPointSqliteSerializerTest
{
    [TestMethod]
    public void Serialize_And_Deserialize_WithCreatedTime_ShouldBeEqual()
    {
        int id = 456;
        var serializer = new TimedHNSWPointSqliteSerializer();
        var now = DateTime.UtcNow;
        var point = new TimedHNSWPoint
        {
            Label = "timed-label",
            Data = new float[] { 1.5f, 2.5f },
            CreatedTime = now
        };

        using var ms = new MemoryStream();
        serializer.SerializePoint(ms, point, id);

        ms.Position = 0;
        var result = serializer.DeserializePoint(point.Label, ms) as TimedHNSWPoint;

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        Assert.AreEqual(point.Label, result.Label);
        CollectionAssert.AreEqual(point.Data, result.Data);
        Assert.AreEqual(point.CreatedTime.Value.ToFileTimeUtc(), result.CreatedTime.Value.ToFileTimeUtc());
    }

    [TestMethod]
    public void Serialize_And_Deserialize_WithoutCreatedTime_ShouldBeNull()
    {
        int id = 456;
        var serializer = new TimedHNSWPointSqliteSerializer();
        var point = new TimedHNSWPoint
        {
            Label = "no-time",
            Data = new float[] { 7.7f, 8.8f },
            CreatedTime = null
        };

        using var ms = new MemoryStream();
        serializer.SerializePoint(ms, point, id);

        ms.Position = 0;
        var result = serializer.DeserializePoint(point.Label, ms) as TimedHNSWPoint;

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        Assert.AreEqual(point.Label, result.Label);
        CollectionAssert.AreEqual(point.Data, result.Data);
        Assert.IsNull(result.CreatedTime);
    }
}
