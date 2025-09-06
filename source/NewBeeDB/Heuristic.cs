using System.Numerics;

namespace NewBeeDB;

internal static class Heuristic
{
    internal static IComparer<NodeDistance> FartherFirst = new DistanceComparer();
    internal static IComparer<NodeDistance> CloserFirst = new ReverseDistanceComparer();

    internal static List<int> DefaultHeuristic(List<NodeDistance> candidates, Func<int, int, float> distanceFnc, int maxEdges)
    {
        if (candidates.Count < maxEdges)
        {
            return candidates.ConvertAll(x => x.Id);
        }

        var resultList = new List<NodeDistance>(maxEdges + 1);
        var candidatesHeap = new BinaryHeap<NodeDistance>(candidates, CloserFirst);

        while (candidatesHeap.Count > 0)
        {
            if (resultList.Count >= maxEdges)
                break;

            var currentCandidate = candidatesHeap.Pop();
            var candidateDist = currentCandidate.Dist;

            // Candidate is closer to designated point than any other already connected point
            if (resultList.TrueForAll(connectedNode => distanceFnc(connectedNode.Id, currentCandidate.Id) > candidateDist))
            {
                resultList.Add(currentCandidate);
            }
        }

        return resultList.ConvertAll(x => x.Id);
    }
}
