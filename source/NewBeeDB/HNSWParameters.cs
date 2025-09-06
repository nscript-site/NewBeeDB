namespace NewBeeDB;

public partial class HNSWParameters
{
    /// <summary>
    /// Number of outgoing edges from nodes. Number of edges on layer 0 might not obey this limit.
    /// </summary>
    public int MaxEdges { get; set; } = 16;

    /// <summary>
    /// Rate parameter for exponential distribution.
    /// </summary>
    public double DistributionRate { get; set; } = 1 / Math.Log(16);

    /// <summary>
    /// The minimal number of nodes obtained by knn search. If provided k exceeds this value, the search result will be trimmed to k. Improves recall for small k.
    /// </summary>
    public int MinNN { get; set; } = 5;

    /// <summary>
    /// Maximum number of nodes taken as candidates for neighbour check during insertion
    /// </summary>
    public int MaxCandidates { get; set; } = 1000;

    /// <summary>
    /// Expected amount of nodes in the graph.
    /// </summary>
    public int CollectionSize { get; set; } = 65536;

    /// <summary>
    /// Seed for RNG. Values below 0 are taken as no seed.
    /// </summary>
    public int RandomSeed { get; set; } = 31337;

    public bool Equals(HNSWParameters obj)
    {
        if (MaxEdges != obj.MaxEdges) return false;
        if (DistributionRate != obj.DistributionRate) return false;
        if (MinNN != obj.MinNN) return false;
        if (MaxCandidates != obj.MaxCandidates) return false;
        if (CollectionSize != obj.CollectionSize) return false;
        if (RandomSeed != obj.RandomSeed) return false;

        return true;
    }

    public void Serialize(Stream stream)
    {
        BinarySerializer.SerializeInt32(stream, MaxEdges);
        BinarySerializer.SerializeDouble(stream, DistributionRate);
        BinarySerializer.SerializeInt32(stream, MinNN);
        BinarySerializer.SerializeInt32(stream, MaxCandidates);
        BinarySerializer.SerializeInt32(stream, CollectionSize);
        BinarySerializer.SerializeInt32(stream, RandomSeed);
    }

    public static HNSWParameters Deserialize(Stream stream)
    {
        var parameters = new HNSWParameters();
        parameters.MaxEdges = BinarySerializer.DeserializeInt32(stream);
        parameters.DistributionRate = BinarySerializer.DeserializeDouble(stream);
        parameters.MinNN = BinarySerializer.DeserializeInt32(stream);
        parameters.MaxCandidates = BinarySerializer.DeserializeInt32(stream);
        parameters.CollectionSize = BinarySerializer.DeserializeInt32(stream);
        parameters.RandomSeed = BinarySerializer.DeserializeInt32(stream);
        return parameters;
    }
}