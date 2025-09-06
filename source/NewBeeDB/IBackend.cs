namespace NewBeeDB;

/// <summary>
/// 后端序列化接口
/// </summary>
public interface IBackend
{
    #region 保存元数据
    public void SaveParameters(HNSWParameters? parameters);

    public void SaveEntryPoint(int entryPointId);

    public void SaveCapacity(int capacity);

    #endregion

    #region 保存点和节点

    public void SavePoint(HNSWPoint point, int? newId = null);

    public void RemovePoint(string label);

    /// <summary>
    /// 添加处理失败的点。有的点可能因为原始数据就有问题，无法计算出特征，这里记录这些点。本方法不在 HNSW 里调用，由上层调用。
    /// </summary>
    /// <param name="label"></param>
    public void SaveBadPoint(string label);

    public void SaveNode(Node node);

    public void UpdateNode(Node node);

    public void UpdateNodes(IEnumerable<Node> nodes);

    public void RemoveNode(int nodeId);

    public void AddRemovedIndex(int index);

    public void RemoveRemovedIndex(int index);

    #endregion

    // 加载图数据
    public HNSWIndex? Load(Func<HNSWPoint, HNSWPoint, float> distFnc, Action<double>? onProgress = null, CancellationToken? token = null);

    public static bool ThrowExceptionOnTryDo { get; set; } = true;

    /// <summary>
    /// Try Save 是否启用。如果启用，执行 TrySave 时的操作，否则，则不执行对应操作。
    /// 当从后端读取数据时，通常不需要保存数据，因此可以将此属性设为 false。读取完毕后，可以设回 true。
    /// 实现时，IsTrySaveEnabled 默认值应该为 true。
    /// </summary>
    bool IsTrySaveEnabled { get; set; }

    /// <summary>
    /// 如果不为 null，则在执行 TrySave 时，使用此锁。
    /// </summary>
    object? TrySaveLock { get; }

    /// <summary>
    /// 尝试执行某个保存操作
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
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

            if (ThrowExceptionOnTryDo == true)
                throw;
        }

        return flag;
    }
}
