namespace NewBeeDB;

internal struct DistanceCalculator<T>
{
    private readonly Func<int, T, float> Distance;

    public T Destination { get; }

    public DistanceCalculator(Func<int, T, float> distance, T destination)
    {
        Distance = distance;
        Destination = destination;
    }

    public float From(int source)
    {
        return Distance(source, Destination);
    }
}
