using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewBeeDB.Backends;

public interface IHNSWPointSqliteSerializer
{
    HNSWPoint DeserializePoint(string label, Stream stream);
    void SerializePoint(Stream stream, HNSWPoint point, int? newId = null);
    HNSWPoint CreateBadPoint(string label);
}

public class HNSWPointSqliteSerializer
    : IHNSWPointSqliteSerializer
{
    public HNSWPoint DeserializePoint(string label, Stream stream)
    {
        int id = BinarySerializer.DeserializeInt32(stream);
        var data = BinarySerializer.DeserializeArray_Float(stream);
        return HNSWPoint.Deserialize(id, label, data);
    }

    public void SerializePoint(Stream stream, HNSWPoint point, int? newId = null)
    {
        int id = newId ?? point.Id;
        BinarySerializer.SerializeInt32(stream, id);
        BinarySerializer.SerializeArray_Float(stream, point.Data);
    }

    public HNSWPoint CreateBadPoint(string label)
    {
        return new HNSWPoint() { Data = Array.Empty<float>(), Label = label };
    }
}