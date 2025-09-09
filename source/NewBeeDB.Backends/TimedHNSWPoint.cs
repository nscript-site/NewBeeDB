using System.Drawing;

namespace NewBeeDB.Backends;

public class TimedHNSWPoint : HNSWPoint
{
    public DateTime? CreatedTime { get; set; } = null;

    public override void Serialize(Stream stream)
    {
        base.Serialize(stream);
        BinarySerializer.SerializeDateTime(stream, this.CreatedTime);
    }

    public override void DeserializeFrom(Stream stream)
    {
        base.DeserializeFrom(stream);
        this.CreatedTime = BinarySerializer.DeserializeDateTime(stream);
    }

    public override bool Equals(HNSWPoint p)
    {
        if(p is TimedHNSWPoint tp)
        {
            if (this.CreatedTime != tp.CreatedTime)
                return false;
        }
        else
            return false;
        
        return base.Equals(p);
    }

    public static TimedHNSWPoint Deserialize(int id, string label, float[] data, DateTime? createTime)
    {
        return new TimedHNSWPoint() { Id = id, Label = label, Data = data, CreatedTime = createTime };
    }
}

public class TimedHNSWPointSqliteSerializer : IHNSWPointSqliteSerializer
{
    public HNSWPoint CreateBadPoint(string label)
    {
        return new TimedHNSWPoint
        {
            Label = label,
            Data = Array.Empty<float>(),
        };
    }

    public HNSWPoint DeserializePoint(string label, Stream stream)
    {
        int id = BinarySerializer.DeserializeInt32(stream);
        var data = BinarySerializer.DeserializeArray_Float(stream);
        var time = BinarySerializer.DeserializeDateTime(stream);
        return TimedHNSWPoint.Deserialize(id, label, data,time);
    }

    public void SerializePoint(Stream stream, HNSWPoint point, int? newId = null)
    {
        int id = newId ?? point.Id;
        if (point is TimedHNSWPoint tp)
        {
            // no need to serialize Label, because in SQLite Label is stored as primary key
            BinarySerializer.SerializeInt32(stream, id);
            BinarySerializer.SerializeArray_Float(stream, point.Data);
            BinarySerializer.SerializeDateTime(stream, tp.CreatedTime);
        }
        else
        {
            throw new ArgumentException("Point must be of type TimedHNSWPoint", nameof(point));
        }
    }
}
