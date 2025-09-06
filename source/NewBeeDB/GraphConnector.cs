namespace NewBeeDB;

internal class GraphConnector
{
    private GraphData data;
    private GraphNavigator navigator;
    private HNSWParameters parameters;

    private IBackend? backend;

    internal GraphConnector(GraphData graphData, GraphNavigator graphNavigator, HNSWParameters hnswParams)
    {
        data = graphData;
        navigator = graphNavigator;
        parameters = hnswParams;
        backend = data.Backend;
    }

    internal void ConnectNewNode(int nodeId)
    {
        // If this is new ep we keep lock for entire Add Operation
        Monitor.Enter(data.entryPointLock);
        if (data.EntryPointId < 0)
        {
            data.EntryPointId = nodeId;
            Monitor.Exit(data.entryPointLock);
            return;
        }

        var currNode = data.Nodes[nodeId];

        // If we have backend, we need to track all changes and save them later
        DirtyNodes? dirtyNodes = null;
        if (backend != null)
        {
            dirtyNodes = new DirtyNodes();
            dirtyNodes.Add(currNode);
        }

        if (currNode.MaxLayer > data.GetTopLayer())
        {
            AddNewConnections(currNode, dirtyNodes);
            data.EntryPointId = nodeId;
            Monitor.Exit(data.entryPointLock);
        }
        else
        {
            Monitor.Exit(data.entryPointLock);
            AddNewConnections(currNode, dirtyNodes);
        }

        // Save all affected nodes
        if (dirtyNodes != null && dirtyNodes.Count > 0)
        {
            backend?.UpdateNodes(dirtyNodes.GetNodes());
        }
    }

    internal void RemoveConnectionsAtLayer(Node removedNode, int layer, DirtyNodes? dirtyNodesReceiver = null)
    {
        if (removedNode.Id == data.EntryPointId)
        {
            var replacementFound = data.TryReplaceEntryPoint(layer);
            if (!replacementFound && layer == 0)
            {
                if (data.Nodes.Count > 0) throw new InvalidOperationException("Delete on isolated enry point");
                data.EntryPointId = -1;
            }
        }

        WipeRelationsWithNode(removedNode, layer, dirtyNodesReceiver);

        var candidates = removedNode.OutEdges[layer];
        for (int i = 0; i < removedNode.InEdges[layer].Count; i++)
        {
            var activeNodeId = removedNode.InEdges[layer][i];
            var activeNode = data.Nodes[activeNodeId];
            var activeNeighbours = activeNode.OutEdges[layer];

            lock (activeNode.OutEdgesLock)
                activeNode.OutEdges[layer].Remove(removedNode.Id);

            dirtyNodesReceiver?.Add(activeNode);

            // Select candidates for active node
            var localCandidates = new List<NodeDistance>();
            for (int j = 0; j < candidates.Count; j++)
            {
                var candidateId = candidates[j];
                if (candidateId == activeNodeId || activeNeighbours.Contains(candidateId))
                    continue;

                localCandidates.Add(new NodeDistance { Id = candidateId, Dist = data.Distance(candidateId, activeNodeId) });
            }

            var candidatesHeap = new BinaryHeap<NodeDistance>(localCandidates, Heuristic.CloserFirst);
            while (candidatesHeap.Count > 0 && activeNeighbours.Count < data.MaxEdges(layer))
            {
                var candidate = candidatesHeap.Pop();
                if (activeNeighbours.TrueForAll((n) => data.Distance(candidate.Id, n) > candidate.Dist))
                {
                    lock (activeNode.OutEdgesLock)
                        activeNode.OutEdges[layer].Add(candidate.Id);

                    var candidateNode = data.Nodes[candidate.Id];

                    lock (candidateNode.InEdgesLock)
                        candidateNode.InEdges[layer].Add(activeNodeId);
                    
                    dirtyNodesReceiver?.Add(candidateNode);
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="currNode"></param>
    /// <param name="dirtyNodesReceiver">脏节点接收器。如果不为空，则将新增的脏节点存储在这里</param>
    private void AddNewConnections(Node currNode, DirtyNodes? dirtyNodesReceiver = null)
    {
        var distCalculator = new DistanceCalculator<int>(data.Distance, currNode.Id);
        var bestPeer = navigator.FindEntryPoint(currNode.MaxLayer, distCalculator);

        for (int layer = Math.Min(currNode.MaxLayer, data.GetTopLayer()); layer >= 0; --layer)
        {
            var topCandidates = navigator.SearchLayer(bestPeer.Id, layer, parameters.MaxCandidates, distCalculator);
            var bestNeighboursIds = Heuristic.DefaultHeuristic(topCandidates, data.Distance, data.MaxEdges(layer));

            for (int i = 0; i < bestNeighboursIds.Count; ++i)
            {
                int newNeighbourId = bestNeighboursIds[i];
                var newNeighbour = data.Nodes[newNeighbourId];
                Connect(currNode, newNeighbour, layer, dirtyNodesReceiver);
                Connect(newNeighbour, currNode, layer, dirtyNodesReceiver);
            }
        }
    }

    private void Connect(Node node, Node neighbour, int layer, DirtyNodes? dirtyNodesReceiver = null)
    {
        dirtyNodesReceiver?.Add(node);
        dirtyNodesReceiver?.Add(neighbour);

        lock (node.OutEdgesLock)
        {
            // Try simple addition
            node.OutEdges[layer].Add(neighbour.Id);
            lock (neighbour.InEdgesLock)
            {
                neighbour.InEdges[layer].Add(node.Id);
            }
            // Connections exceeded limit from parameters
            if (node.OutEdges[layer].Count > data.MaxEdges(layer))
            {
                WipeRelationsWithNode(node, layer, dirtyNodesReceiver);
                RecomputeConnections(node, node.OutEdges[layer], layer);
                SetRelationsWithNode(node, layer, dirtyNodesReceiver);
            }
        }
    }

    private void RecomputeConnections(Node node, List<int> candidates, int layer)
    {
        var candidatesDistances = new List<NodeDistance>(candidates.Count);
        foreach (var neighbourId in candidates)
            candidatesDistances.Add(new NodeDistance { Dist = data.Distance(neighbourId, node.Id), Id = neighbourId });
        var newNeighbours = Heuristic.DefaultHeuristic(candidatesDistances, data.Distance, data.MaxEdges(layer));
        node.OutEdges[layer] = newNeighbours;
    }

    private void WipeRelationsWithNode(Node node, int layer, DirtyNodes? dirtyNodesReceiver = null)
    {
        lock (node.OutEdgesLock)
        {
            foreach (var neighbourId in node.OutEdges[layer])
            {
                var neighbour = data.Nodes[neighbourId];
                lock (neighbour.InEdgesLock)
                {
                    neighbour.InEdges[layer].Remove(node.Id);
                }
                dirtyNodesReceiver?.Add(neighbour);
            }
        }
    }

    private void SetRelationsWithNode(Node node, int layer, DirtyNodes? dirtyNodesReceiver = null)
    {
        lock (node.OutEdgesLock)
        {
            foreach (var neighbourId in node.OutEdges[layer])
            {
                var neighbour = data.Nodes[neighbourId];
                lock (neighbour.InEdgesLock)
                {
                    neighbour.InEdges[layer].Add(node.Id);
                }
                dirtyNodesReceiver?.Add(neighbour);
            }
        }
    }
}
