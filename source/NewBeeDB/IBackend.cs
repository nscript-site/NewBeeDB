namespace NewBeeDB;

/// <summary>
/// HnswIndex backend interface
/// </summary>
public interface IBackend
{
    #region Save metadata
    public void SaveParameters(HNSWParameters? parameters);

    public void SaveEntryPoint(int entryPointId);

    public void SaveCapacity(int capacity);

    #endregion

    #region Actions on points and nodes

    /// <summary>
    /// 保存点。如果 newId 有值，则使用 newId 作为点的 Id，否则使用 point.Id 作为点的 Id。
    /// HNSWPoint 在进入索引时会自动分配 Id，通常不需要指定 newId。
    /// Save the point. If newId has a value, use newId as the point's Id; otherwise, 
    /// use point.Id as the point's Id.
    /// HNSWPoint will automatically assign an Id when entering the index, so usually 
    /// there is no need to specify newId.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="newId"></param>
    public void SavePoint(HNSWPoint point, int? newId = null);

    /// <summary>
    /// 移除点
    /// Remove point
    /// </summary>
    /// <param name="label"></param>
    public void RemovePoint(string label);

    /// <summary>
    /// 添加处理失败的点。有的点可能因为原始数据就有问题，无法计算出特征，这里记录这些点。本方法通常不在 HNSW 里
    /// 调用，由应用层调用。比如说，计算图像特征时，如果某个图像文件损坏，无法计算出特征，就可以调用本方法记录下来
    /// Add points for handling failures. Some points may fail to have features calculated due to 
    /// issues with the original data, and these points should be recorded here. This method is 
    /// usually not called within HNSW, but by the application layer. For example, when calculating
    /// image features, if an image file is corrupted and features cannot be calculated, this method 
    /// can be called to record such cases.
    /// </summary>
    /// <param name="label"></param>
    public void SaveBadPoint(string label);

    /// <summary>
    /// 当 HNSWIndex 插入新节点时会调用本方法保存节点信息
    /// When HNSWIndex inserts a new node, this method is called to save the node information
    /// </summary>
    /// <param name="node"></param>
    public void SaveNode(Node node);

    /// <summary>
    /// 更新一个节点的信息。当前 HNSWIndex 未调用，可以不实现。
    /// Update the information of a node. Currently, HNSWIndex does not call this method, so it can be
    /// left unimplemented.
    /// </summary>
    /// <param name="node"></param>
    public void UpdateNode(Node node);

    /// <summary>
    /// 当 HNSWIndex 插入/删除节点时，会将关联的点标记为脏节点，调用本方法批量更新节点信息。通常节点数量在数十个-数
    /// 百个之间。如果索引规模很大，也可能数量会更多。实现这个方法需要考虑到性能问题。
    /// When nodes are inserted into or deleted from the HNSWIndex, the associated points will be marked
    /// as dirty nodes, and this method is called to update node information in batches. Typically, the 
    /// number of nodes ranges from dozens to hundreds. If the index scale is large, the number may be 
    /// even greater.
    /// Performance considerations should be taken into account when implementing this method.
    /// </summary>
    /// <param name="nodes"></param>
    public void UpdateNodes(IEnumerable<Node> nodes);

    /// <summary>
    /// 移除节点。这个库里的 HNSWIndex 并不会真实删除 Node，只是将它的 Id 从索引里移除。因此，本方法暂时也没用，可以不实现。
    /// Remove node. The HNSWIndex in this library does not actually delete the Node; it only removes its Id 
    /// from the index. So, this method is currently unused and can be left unimplemented.
    /// </summary>
    /// <param name="nodeId"></param>
    public void RemoveNode(int nodeId);

    /// <summary>
    /// 添加一个废弃Id。
    /// 这个库的 HnswIndex 在删除节点时，仅仅将它从索引图里移除，并将它的 Id 记录在 RemovedIndices 里，供后面新增的节点使
    /// 用。
    /// Add a removed index.
    /// The HnswIndex in this library only removes the node from the index graph when deleting it, and records
    /// its Id in RemovedIndices for later use by newly added nodes.
    /// </summary>
    /// <param name="index"></param>
    public void AddRemovedIndex(int index);

    /// <summary>
    /// 移除一个废弃Id。
    /// Remove a removed index.
    /// </summary>
    /// <param name="index">The index to remove. Must be a valid index within the collection.</param>
    public void RemoveRemovedIndex(int index);

    #endregion

    /// <summary>
    /// Load hnsw index
    /// </summary>
    /// <param name="distFnc">distance function </param>
    /// <param name="onProgress">onProgress action</param>
    /// <param name="token">CancellationToken</param>
    /// <param name="attachAsIndexBackend">If this backend is set as the HNSW index backend</param>
    /// <returns></returns>
    public HNSWIndex? Load(Func<HNSWPoint, HNSWPoint, float> distFnc, 
        Action<double>? onProgress = null, 
        CancellationToken? token = null, 
        bool attachAsIndexBackend = true);

    public static bool ThrowExceptionOnTrySave { get; set; } = true;

    /// <summary>
    /// Whether Try Save is enabled. If enabled, the operation during TrySave execution will be performed; 
    /// otherwise, the corresponding operation will not be performed.
    /// When reading data from the backend, it is usually unnecessary to save the data, so this property 
    /// can be set to false. After the reading is completed, it can be set back to true.
    /// During implementation, the default value of IsTrySaveEnabled should be true.
    /// </summary>
    bool IsTrySaveEnabled { get; set; }

    /// <summary>
    /// If it is not null, this lock will be used when executing TrySave.
    /// </summary>
    object? TrySaveLock { get; }

    /// <summary>
    /// Attempt to perform a save operation. All backend data changes should be done in this action.
    /// This provides a unified exception handling and synchronization mechanism.
    /// </summary>
    /// <param name="action"></param>
    /// <returns>Whether the action call is successful</returns>
    public bool TrySave(Action action)
    {
        if (IsTrySaveEnabled == false)
            return false;

        bool flag = false;
        try
        {
            if(TrySaveLock != null)
            {
                lock (TrySaveLock)
                {
                    action();
                }
            }
            else
            {
                action();
            }

            flag = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Backend operation failed: {ex.Message}");

            if (ThrowExceptionOnTrySave == true)
                throw;
        }

        return flag;
    }
}
