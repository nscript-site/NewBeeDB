namespace NewBeeDB;

public class KNNResult : IComparable<KNNResult>
{
    public int Id { get; private set; }
    public HNSWPoint Point { get; private set; }
    public float Distance { get; private set; }

    internal KNNResult(int id, HNSWPoint label, float distance)
    {
        Id = id;
        Point = label;
        Distance = distance;
    }

    public int CompareTo(KNNResult? other)
    {
        if (other == null) return 1;
        return Distance.CompareTo(other.Distance);
    }
}
