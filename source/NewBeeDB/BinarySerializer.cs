using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace NewBeeDB;

[StructLayout(LayoutKind.Explicit)]
public struct SliceDataInfo
{
    // 0 - body, 1 - item slice, 2 - node slice
    [FieldOffset(0)]
    public byte SliceType;
    [FieldOffset(4)]
    public int Bytes;

    public void Serialize(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8]; // SliceType(1) + 3字节填充 + Bytes(4) = 8字节
        buffer[0] = SliceType;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(4, 4), Bytes);
        stream.Write(buffer);
    }

    public static SliceDataInfo Deserialize(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        int read = 0;
        while (read < 8)
        {
            int n = stream.Read(buffer.Slice(read, 8 - read));
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading SliceDataInfo.");
            read += n;
        }

        SliceDataInfo info = new SliceDataInfo
        {
            SliceType = buffer[0],
            Bytes = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4))
        };
        return info;
    }
}

public class SliceData
{
    public SliceDataInfo Info { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public class BinarySerializer
{
    public static void SerializeInt32(Stream stream, int val)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, val);
        stream.Write(buffer);
    }

    public static int DeserializeInt32(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        int read = 0;
        while (read < 4)
        {
            int n = stream.Read(buffer.Slice(read, 4 - read));
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading integer.");
            read += n;
        }
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public static void SerializeInt64(Stream stream, long val)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, val);
        stream.Write(buffer);
    }

    public static long DeserializeInt64(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        int read = 0;
        while (read < 8)
        {
            int n = stream.Read(buffer.Slice(read, 8 - read));
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading long integer.");
            read += n;
        }
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public static void SerializeDouble(Stream stream, double val)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, val);
        stream.Write(buffer);
    }

    public static double DeserializeDouble(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        int read = 0;
        while (read < 8)
        {
            int n = stream.Read(buffer.Slice(read, 8 - read));
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading double.");
            read += n;
        }
        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    public static void SerializeList_Int32(Stream stream, List<int> list)
    {
        BinarySerializer.SerializeInt32(stream, list.Count);
        foreach (var item in list)
        {
            BinarySerializer.SerializeInt32(stream, item);
        }
    }

    public static List<int> DeserializeList_Int32(Stream stream)
    {
        int count = BinarySerializer.DeserializeInt32(stream);
        List<int> list = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(BinarySerializer.DeserializeInt32(stream));
        }
        return list;
    }

    public static void SerializeListOfLists_Int32(Stream stream, List<List<int>> listOfLists)
    {
        BinarySerializer.SerializeInt32(stream, listOfLists.Count);
        foreach (var list in listOfLists)
        {
            BinarySerializer.SerializeList_Int32(stream, list);
        }
    }

    public static List<List<int>> DeserializeListOfLists_Int32(Stream stream)
    {
        int count = BinarySerializer.DeserializeInt32(stream);
        List<List<int>> listOfLists = new List<List<int>>(count);
        for (int i = 0; i < count; i++)
        {
            listOfLists.Add(BinarySerializer.DeserializeList_Int32(stream));
        }
        return listOfLists;
    }

    public static void SerializeArray_Float(Stream stream, float[] data)
    {
        if (data == null || data.Length == 0)
        {
            BinarySerializer.SerializeInt32(stream, 0);
            return;
        }
        BinarySerializer.SerializeInt32(stream, data.Length);
        Span<byte> buffer = stackalloc byte[data.Length * 4];
        for (int i = 0; i < data.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(i * 4), data[i]);
        }
        stream.Write(buffer);
    }

    public static float[] DeserializeArray_Float(Stream stream)
    {
        int length = BinarySerializer.DeserializeInt32(stream);
        if (length == 0)
            return Array.Empty<float>();
        Span<byte> buffer = stackalloc byte[length * 4];
        int read = 0;
        while (read < length * 4)
        {
            int n = stream.Read(buffer.Slice(read, length * 4 - read));
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading float array.");
            read += n;
        }
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(i * 4));
        }
        return data;
    }

    public static void SerializeString(Stream stream, string str)
    {
        if (str == null)
        {
            BinarySerializer.SerializeInt32(stream, 0);
            return;
        }
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
        BinarySerializer.SerializeInt32(stream, bytes.Length);
        stream.Write(bytes);
    }

    public static string DeserializeString(Stream stream)
    {
        int length = BinarySerializer.DeserializeInt32(stream);
        if (length == 0)
            return string.Empty;
        byte[] bytes = new byte[length];
        int read = 0;
        while (read < length)
        {
            int n = stream.Read(bytes, read, length - read);
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading string.");
            read += n;
        }
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public static void SerializeList_Node(Stream stream, List<Node>? nodes)
    {
        if(nodes == null)
        {
            BinarySerializer.SerializeInt32(stream, 0);
            return;
        }

        BinarySerializer.SerializeInt32(stream, nodes.Count);
        foreach (var node in nodes)
        {
            node.Serialize(stream);
        }
    }

    public static List<Node> DeserializeList_Node(Stream stream)
    {
        int count = BinarySerializer.DeserializeInt32(stream);
        List<Node> nodes = new List<Node>(count);
        for (int i = 0; i < count; i++)
        {
            nodes.Add(Node.Deserialize(stream));
        }
        return nodes;
    }

    public static void SerializeHashSet_String(Stream stream, HashSet<string> keys)
    {
        SerializeInt32(stream, keys.Count);
        foreach (var key in keys)
        {
            SerializeString(stream, key);
        }
    }

    public static HashSet<string> DeserializeHashSet_String(Stream stream)
    {
        int count = DeserializeInt32(stream);
        HashSet<string> keys = new HashSet<string>(count);
        for (int i = 0; i < count; i++)
        {
            keys.Add(DeserializeString(stream));
        }
        return keys;
    }

    public static void SerializeDateTime(Stream stream, DateTime? time)
    {
        if (time == null)
            BinarySerializer.SerializeInt32(stream, 0);
        else
        {
            var timeVal = time.Value;
            BinarySerializer.SerializeInt32(stream, 1);
            int timeKind = (timeVal.Kind == DateTimeKind.Utc) ? 1 :
                (timeVal.Kind == DateTimeKind.Local) ? 2 : 0;
            BinarySerializer.SerializeInt32(stream, timeKind);
            BinarySerializer.SerializeInt64(stream, timeVal.ToUniversalTime().ToFileTimeUtc());
        }
    }

    public static DateTime? DeserializeDateTime(Stream stream)
    {
        DateTime? time = null;
        int hasTime = BinarySerializer.DeserializeInt32(stream);
        if (hasTime == 1)
        {
            int timeKind = BinarySerializer.DeserializeInt32(stream);
            DateTimeKind TimeKind = (timeKind == 1) ? DateTimeKind.Utc :
                (timeKind == 2) ? DateTimeKind.Local : DateTimeKind.Unspecified;
            long fileTime = BinarySerializer.DeserializeInt64(stream);
            time = DateTime.FromFileTimeUtc(fileTime);
            if (TimeKind == DateTimeKind.Local)
                time = time.Value.ToLocalTime();
            else if (TimeKind == DateTimeKind.Unspecified)
                time = DateTime.SpecifyKind(time.Value, DateTimeKind.Unspecified);
        }
        return time;
    }
}