using System.Numerics;

namespace NewBeeDB;

internal class GraphNavigator
{
    private static Func<int, bool> noFilter = _ => true;

    private VisitedListPool pool;
    private GraphData data;
    private IComparer<NodeDistance> fartherFirst;
    private IComparer<NodeDistance> closerFirst;

    internal GraphNavigator(GraphData graphData)
    {
        data = graphData;
        pool = new VisitedListPool(1, graphData.Capacity);
        fartherFirst = new DistanceComparer();
        closerFirst = new ReverseDistanceComparer();
    }

    internal Node FindEntryPoint<T>(int dstLayer, DistanceCalculator<T> dsfloat)
    {
        var bestPeer = data.EntryPoint;
        var currDist = dsfloat.From(bestPeer.Id);

        for (int level = bestPeer.MaxLayer; level > dstLayer; level--)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                lock (data.Nodes[bestPeer.Id].OutEdgesLock)
                {
                    List<int> connections = bestPeer.OutEdges[level];
                    int size = connections.Count;

                    for (int i = 0; i < size; i++)
                    {
                        int cand = connections[i];
                        var d = dsfloat.From(cand);
                        if (d < currDist)
                        {
                            currDist = d;
                            bestPeer = data.Nodes[cand];
                            changed = true;
                        }
                    }
                }
            }
        }

        return bestPeer;
    }

    internal List<NodeDistance> SearchLayer<T>(int entryPointId, int layer, int k, DistanceCalculator<T> distanceCalculator, Func<int, bool>? filterFnc = null)
    {
        filterFnc ??= noFilter;
        var topCandidates = new BinaryHeap<NodeDistance>(new List<NodeDistance>(k), fartherFirst);
        var candidates = new BinaryHeap<NodeDistance>(new List<NodeDistance>(k * 2), closerFirst); // Guess that k*2 space is usually enough

        var entry = new NodeDistance { Dist = distanceCalculator.From(entryPointId), Id = entryPointId };
        // TODO: Make it max value of float
        var farthestResultDist = entry.Dist;

        if (filterFnc(entryPointId))
        {
            topCandidates.Push(entry);
            farthestResultDist = entry.Dist;
        }

        candidates.Push(entry);
        var visitedList = pool.GetFreeVisitedList();
        visitedList.Add(entryPointId);

        // run bfs
        while (candidates.Buffer.Count > 0)
        {
            // get next candidate to check and expand
            var closestCandidate = candidates.Buffer[0];
            if (closestCandidate.Dist > farthestResultDist && topCandidates.Count >= k)
            {
                break;
            }
            candidates.Pop(); // Delay heap reordering in case of early break 

            // expand candidate
            lock (data.Nodes[closestCandidate.Id].OutEdgesLock)
            {
                var neighboursIds = data.Nodes[closestCandidate.Id].OutEdges[layer];

                for (int i = 0; i < neighboursIds.Count; ++i)
                {
                    int neighbourId = neighboursIds[i];
                    if (visitedList.Contains(neighbourId)) continue;

                    var neighbourDistance = distanceCalculator.From(neighbourId);

                    // enqueue perspective neighbours to expansion list
                    if (topCandidates.Count < k || neighbourDistance < farthestResultDist)
                    {
                        var selectedCandidate = new NodeDistance { Dist = neighbourDistance, Id = neighbourId };
                        candidates.Push(selectedCandidate);

                        if (filterFnc(selectedCandidate.Id))
                            topCandidates.Push(selectedCandidate);

                        if (topCandidates.Count > k)
                            topCandidates.Pop();

                        if (topCandidates.Count > 0)
                            farthestResultDist = topCandidates.Buffer[0].Dist;
                    }

                    // update visited list
                    visitedList.Add(neighbourId);
                }
            }
        }

        pool.ReleaseVisitedList(visitedList);

        return topCandidates.Buffer;
    }

    internal void OnReallocate(int newCapacity)
    {
        pool.Resize(newCapacity);
    }
}