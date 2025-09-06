namespace NewBeeDB;

public class HNSWIndexSnapshotMetaData
{
    public HNSWParameters? Parameters { get; set; }

    public int EntryPointId = -1;

    public int Capacity;

    public void Serialize(Stream stream)
    {
        BinarySerializer.SerializeInt32(stream, EntryPointId);
        BinarySerializer.SerializeInt32(stream, Capacity);
        if (Parameters == null)
            BinarySerializer.SerializeInt32(stream, 0);
        else
        {
            BinarySerializer.SerializeInt32(stream, 1);
            Parameters.Serialize(stream);
        }
    }

    public static HNSWIndexSnapshotMetaData Deserialize(Stream stream)
    {
        var snapshot = new HNSWIndexSnapshotMetaData();
        snapshot.EntryPointId = BinarySerializer.DeserializeInt32(stream);
        snapshot.Capacity = BinarySerializer.DeserializeInt32(stream);
        int hasParams = BinarySerializer.DeserializeInt32(stream);
        if (hasParams == 1)
            snapshot.Parameters = HNSWParameters.Deserialize(stream);
        return snapshot;
    }
}
