using System.Numerics;
using System.Runtime.CompilerServices;

namespace NewBeeDB;

internal struct DistanceComparer : IComparer<NodeDistance>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(NodeDistance x, NodeDistance y)
    {
        if (x.Dist < y.Dist) return -1;
        if (x.Dist > y.Dist) return 1;
        return x.Dist.CompareTo(y.Dist);
    }
}

internal struct ReverseDistanceComparer : IComparer<NodeDistance>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(NodeDistance x, NodeDistance y)
    {
        if (x.Dist > y.Dist) return -1;
        if (x.Dist < y.Dist) return 1;
        return y.Dist.CompareTo(x.Dist);
    }
}