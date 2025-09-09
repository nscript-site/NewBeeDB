using System.Numerics;

namespace NewBeeDB;

public partial class HNSWPoint
{
    public float[] Data { get; set; } = Array.Empty<float>();

    public string Label { get; set; } = String.Empty;

    public int Id { get; protected internal set; } = -1;

    public bool IsEmpty => Data.Length == 0;

    public static unsafe float CosineMetricUnitCompute(HNSWPoint a, HNSWPoint b)
    {
        return Metrics.CosineMetric.UnitCompute(a.Data, b.Data);
    }

    public static float Magnitude(float[] vector)
    {
        float magnitude = 0.0f;
        int step = Vector<float>.Count;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }
        return (float)Math.Sqrt(magnitude);
    }

    public static void Normalize(float[] vector)
    {
        float normFactor = 1f / Magnitude(vector);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] *= normFactor;
        }
    }

    public static List<HNSWPoint> Random(int vectorSize, int vectorsCount, bool normalize = true, int? startId = null, Func<HNSWPoint>? onCreate = null)
    {
        int seed = (int)DateTime.Now.ToFileTimeUtc();
        var random = new Random(seed);
        var vectors = new List<HNSWPoint>();

        for (int i = 0; i < vectorsCount; i++)
        {
            var vector = new float[vectorSize];
            for (int d = 0; d < vectorSize; d++)
                vector[d] = random.NextSingle();
            if(normalize == true)
                Normalize(vector);

            string label = startId.HasValue ? (startId.Value + i).ToString() : Guid.NewGuid().ToString("N");

            HNSWPoint newPoint = onCreate == null ? new HNSWPoint() : onCreate();
            newPoint.Data = vector;
            newPoint.Label = label;

            vectors.Add(newPoint);
        }

        return vectors;
    }

    public virtual void Serialize(Stream stream)
    {
        BinarySerializer.SerializeInt32(stream, Id);
        BinarySerializer.SerializeString(stream, Label);
        BinarySerializer.SerializeArray_Float(stream, Data);
    }

    public virtual void DeserializeFrom(Stream stream)
    {
        Id = BinarySerializer.DeserializeInt32(stream);
        Label = BinarySerializer.DeserializeString(stream);
        Data = BinarySerializer.DeserializeArray_Float(stream);
    }

    public static HNSWPoint Deserialize(Stream stream, Func<HNSWPoint>? onCreate = null)
    {
        var point = onCreate == null ? new HNSWPoint(): onCreate();
        point.DeserializeFrom(stream);
        return point;
    }

    public static HNSWPoint Deserialize(int id, string label, float[] data)
    {
        return new HNSWPoint() { Id = id, Label = label, Data = data };
    }

    public virtual bool Equals(HNSWPoint p)
    {
        if (this.Label != p.Label || this.Id != p.Id) return false;
        if (this.Data.Length != p.Data.Length) return false;
        for (int i = 0; i < this.Data.Length; i++)
            if (this.Data[i] != p.Data[i]) return false;
        return true;
    }
}
