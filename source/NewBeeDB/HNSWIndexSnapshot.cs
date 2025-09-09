using System.Runtime.InteropServices;

namespace NewBeeDB;

/// <summary>
/// Wrapper for HNSWIndex for serialization.
/// </summary>
public partial class HNSWIndexSnapshot
{
    [StructLayout(LayoutKind.Explicit)]
    public struct HNSWHeaderV1
    {
        [FieldOffset(0)]
        public int Version;

        public HNSWHeaderV1()
        {
            Version = 1;
        }
    }

    public HNSWParameters? Parameters { get; set; }

    public GraphDataSnapshot? DataSnapshot { get; set; }

    public HNSWIndexSnapshot() { }

    public HNSWIndexSnapshot(HNSWParameters parameters, GraphData data)
    {
        Parameters = parameters;
        DataSnapshot = new GraphDataSnapshot(data);
    }

    public void Serialize(Stream stream)
    {
        SerializeHeader(stream);

        if (Parameters == null)
        {
            BinarySerializer.SerializeInt32(stream, 0);
        }
        else
        {
            BinarySerializer.SerializeInt32(stream, 1);
            Parameters.Serialize(stream);
        }

        if(DataSnapshot == null)
        {
            BinarySerializer.SerializeInt32(stream, 0);
        }
        else
        {
            BinarySerializer.SerializeInt32(stream, 1);
            DataSnapshot.Serialize(stream);
        }
    }

    public static HNSWIndexSnapshot Deserialize(Stream stream, Func<HNSWPoint>? onCreate)
    {
        bool isValid = DeserializeHeader(stream, out int version);
        if (isValid == false) throw new InvalidDataException(nameof(stream));

        var snapshot = new HNSWIndexSnapshot();
        int hasParameters = BinarySerializer.DeserializeInt32(stream);
        if (hasParameters == 1)
        {
            snapshot.Parameters = HNSWParameters.Deserialize(stream);
        }
        int hasDataSnapshot = BinarySerializer.DeserializeInt32(stream);
        if (hasDataSnapshot == 1)
        {
            snapshot.DataSnapshot = GraphDataSnapshot.Deserialize(stream, onCreate);
        }
        return snapshot;
    }

    const string Header = "HNSWDATA";

    private static readonly byte[] HeaderBytes = System.Text.Encoding.ASCII.GetBytes(Header);

    public static unsafe void SerializeHeader(Stream stream)
    {
        //头部: 64 字节
        Span<byte> header = stackalloc byte[64];
        header.Fill(0);
        for (int i = 0; i < HeaderBytes.Length; i++)
            header[i] = HeaderBytes[i];

        HNSWHeaderV1 h = new HNSWHeaderV1();
        byte* p = (byte*)&h;
        
        fixed(byte* p0 = header)
        {
            // 从 32 字节写起
            HNSWHeaderV1* pH = (HNSWHeaderV1*)(p0 + 32);
            *pH = new HNSWHeaderV1();
        }

        stream.Write(header);
    }

    public static bool DeserializeHeader(Stream s, out int version)
    {
        Span<byte> header = stackalloc byte[64];
        s.ReadExactly(header);
        Span<byte> versionSpan = header.Slice(32, 4);
        version = BitConverter.ToInt32(versionSpan);
        return header.StartsWith(new Span<byte>(HeaderBytes));
    }

    public static HNSWIndexSnapshot CreateFrom(HNSWIndex index)
    {
        return new HNSWIndexSnapshot(index.parameters, index.data);
    }

    public bool Equals(HNSWIndexSnapshot snap)
    {
        var p1 = this.Parameters ?? new HNSWParameters();
        var p2 = snap.Parameters ?? new HNSWParameters();

        if (p1.Equals(p2) == false) return false;

        var ds1 = this.DataSnapshot ?? new GraphDataSnapshot();
        var ds2 = snap.DataSnapshot ?? new GraphDataSnapshot();

        if (ds1.Equals(ds2) == false) return false;

        return true;
    }
}