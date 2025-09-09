
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;

namespace NewBeeDB.Backends;

/// <summary>
/// sqlite 后端
/// </summary>
public class SqliteBackend : IBackend, IDisposable
{
    private bool disposedValue;

    private SqliteConnection conn;

    private string tablePrefix = "hnsw_default";
    private readonly string tableNameOfNodes;
    private readonly string tableNameOfPoints;
    private readonly string tableNameOfConfig;
    private readonly string tableNameOfRemovedIndexes;

    public IHNSWPointSqliteSerializer HNSWPointSqliteSerializer { get; protected set; }

    public SqliteBackend(string dbPath, string indexTableNamePrefix = "hnsw_default", bool readOnly = false,
        IHNSWPointSqliteSerializer? hnswPointSerializer = null)
    {
        HNSWPointSqliteSerializer = hnswPointSerializer ?? new HNSWPointSqliteSerializer();

        var builder = new SqliteConnectionStringBuilder();
        builder.DataSource = dbPath;                  // 数据库路径（含空格也无需手动加引号）
        builder.Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate;
        builder.Pooling = true;                          // 启用连接池（默认true，减少连接开销）

        conn = new SqliteConnection(builder.ToString());
        conn.Open();

        tablePrefix = indexTableNamePrefix;
        tableNameOfNodes = $"{tablePrefix}_nodes";
        tableNameOfPoints = $"{tablePrefix}_points";
        tableNameOfConfig = $"{tablePrefix}_config";
        tableNameOfRemovedIndexes = $"{tablePrefix}_removed_indexes";

        // 如果 indexTableName 不存在，则创建它
        using var cmd = conn.CreateCommand();
        SetDBMode(cmd);
        CreateTableOfConfigIfNotExists(cmd);
        CreateTableOfNodesIfNotExists(cmd);
        CreateTableOfPointsIfNotExists(cmd);
        CreateTableOfRemovedIndexesIfNotExists(cmd);
    }

    public SqliteCommand CreateCommand()
    {
        return conn.CreateCommand();
    }

    private long ExcuteCount = 0;
    private long UpdateNodeCount = 0;

    public long GetExcuteCount()
    {
        return ExcuteCount;
    }

    public long GetUpdateNodeCount()
    {
        return UpdateNodeCount;
    }

    private void SetDBMode(SqliteCommand cmd)
    {
        cmd.CommandText = "PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA synchronous = NORMAL;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA temp_store = MEMORY;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA cache_size = 10000;";
        cmd.ExecuteNonQuery();
    }

    #region 如果表格不存在，则创建它

    private int CreateTableOfNodesIfNotExists(SqliteCommand cmd)
    {
        ExcuteCount++;
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS [{tableNameOfPoints}] (
                [Label] TEXT PRIMARY KEY,
                [Data] BLOB
            );
        ";
        return cmd.ExecuteNonQuery();
    }

    private int CreateTableOfPointsIfNotExists(SqliteCommand cmd)
    {
        ExcuteCount++;
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS [{tableNameOfNodes}] (
                [Id] INTEGER PRIMARY KEY, 
                [Data] BLOB
            );
        ";
        return cmd.ExecuteNonQuery();
    }

    private int CreateTableOfConfigIfNotExists(SqliteCommand cmd)
    {
        ExcuteCount++;
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS [{tableNameOfConfig}] (
                [Key] TEXT PRIMARY KEY, 
                [Value] BLOB
            );
        ";
        return cmd.ExecuteNonQuery();
    }

    private int CreateTableOfRemovedIndexesIfNotExists(SqliteCommand cmd)
    {
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS [{tableNameOfRemovedIndexes}] (
                [Index] INTEGER PRIMARY KEY
            );
        ";
        return cmd.ExecuteNonQuery();
    }

    #endregion

    #region IBackend Members

    public object TrySaveLock { get; protected set; } = new object();

    public bool IsTrySaveEnabled { get; set; } = true;

    #region 保存元数据

    public const string Key_Parameters = "Parameters";
    public const string Key_EntryPoint = "EntryPoint";
    public const string Key_Capacity = "Capacity";

    public void SaveParameters(HNSWParameters? parameters)
    {
        using (var ms = new MemoryStream())
        {
            if (parameters == null)
            {
                BinarySerializer.SerializeInt32(ms, 0);
            }
            else
            {
                BinarySerializer.SerializeInt32(ms, 1);
                parameters.Serialize(ms);
            }
            SaveConfig(Key_Parameters, ms);
        }
    }

    public void SaveEntryPoint(int entryPointId)
    {
        using(var ms = new MemoryStream())
        {
            BinarySerializer.SerializeInt32(ms, entryPointId);
            SaveConfig(Key_EntryPoint, ms);
        }
    }

    public void SaveCapacity(int capacity)
    {
        using( var ms = new MemoryStream())
        {
            BinarySerializer.SerializeInt32(ms, capacity);
            SaveConfig(Key_Capacity, ms);
        }
    }

    public void SaveConfig(string key, MemoryStream memoryStream)
    {
        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
            INSERT INTO [{tableNameOfConfig}] ([Key], [Value])
            VALUES (@key, @value)
            ON CONFLICT([Key]) DO UPDATE SET [Value]=excluded.[Value];
        ";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", memoryStream.ToArray());
            cmd.ExecuteNonQuery();
        }
    }

    public T? GetConfig<T>(string key, Func<Stream, T?> deserializeFunc)
    {
        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT [Value] FROM [{tableNameOfConfig}]
                WHERE [Key] = @key;
            ";

            cmd.Parameters.AddWithValue("@key", key);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var blob = (byte[])reader["Value"];
                using var ms = new MemoryStream(blob);
                return deserializeFunc(ms);
            }
            return default(T?);
        }
    }

    #endregion

    public void SavePoint(HNSWPoint point, int? newId = null)
    {
        ExcuteCount++;

        lock (conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO [{tableNameOfPoints}] ([Label], [Data])
                VALUES (@label, @data)
                ON CONFLICT([Label]) DO UPDATE SET [Data]=excluded.[Data];
            ";

            cmd.Parameters.AddWithValue("@label", point.Label);

            // 序列化 point.Data 为二进制

            using var ms = new MemoryStream();
            
            HNSWPointSqliteSerializer.SerializePoint(ms, point, newId);

            cmd.Parameters.AddWithValue("@data", ms.ToArray());

            cmd.ExecuteNonQuery();
        }
    }

    public void SaveBadPoint(string label)
    {
        // 这里简单地调用 AddPoint，存储一个空的 Data
        var badPoint = HNSWPointSqliteSerializer.CreateBadPoint(label);

        SavePoint(badPoint, -1);
    }

    public void RemovePoint(string label)
    {
        ExcuteCount++;

        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                DELETE FROM [{tableNameOfPoints}]
                WHERE [Label] = @label;
            ";
            cmd.Parameters.AddWithValue("@label", label);
            cmd.ExecuteNonQuery();
        }
    }

    public void SaveNode(Node node)
    {
        ExcuteCount++;

        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO [{tableNameOfNodes}] ([Id], [Data])
                VALUES (@id, @data)
                ON CONFLICT([Id]) DO UPDATE SET [Data]=excluded.[Data];
            ";

            cmd.Parameters.AddWithValue("@id", node.Id);

            using var ms = new MemoryStream();
            node.Serialize(ms);
            cmd.Parameters.AddWithValue("@data", ms.ToArray());

            cmd.ExecuteNonQuery();

        }
    }

    public void RemoveNode(int nodeId)
    {
        ExcuteCount++;

        lock(conn)
        {
            using (var cmd = new SqliteCommand($@"DELETE FROM [{tableNameOfNodes}] WHERE Id = @nodeId", conn))
            {
                cmd.Parameters.AddWithValue("@nodeId", nodeId);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public void UpdateNode(Node node)
    {
        ExcuteCount++;
        UpdateNodeCount++;

        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                UPDATE [{tableNameOfNodes}]
                SET [Data] = @data
                WHERE [Id] = @id;
            ";

            cmd.Parameters.AddWithValue("@id", node.Id);

            using var ms = new MemoryStream();
            node.Serialize(ms);
            cmd.Parameters.AddWithValue("@data", ms.ToArray());

            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// 实现方法。批量更新节点
    /// </summary>
    /// <param name="nodes"></param>
    public void UpdateNodes(IEnumerable<Node> nodes)
    {
        ExcuteCount++;
        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
            UPDATE [{tableNameOfNodes}]
            SET [Data] = @data
            WHERE [Id] = @id;
        ";

            var idParam = cmd.CreateParameter();
            idParam.ParameterName = "@id";
            cmd.Parameters.Add(idParam);

            var dataParam = cmd.CreateParameter();
            dataParam.ParameterName = "@data";
            cmd.Parameters.Add(dataParam);

            foreach (var node in nodes)
            {
                UpdateNodeCount++;
                idParam.Value = node.Id;
                using var ms = new MemoryStream();
                node.Serialize(ms);
                dataParam.Value = ms.ToArray();
                cmd.ExecuteNonQuery();
            }
        }
    }

    public HNSWIndex? Load(Func<HNSWPoint, HNSWPoint, float> distFnc, Action<double>? onProgress = null, CancellationToken? token = null, bool attachAsIndexBackend = true)
    {
        return Load(distFnc, null, onProgress, token, attachAsIndexBackend);
    }

    #endregion

    /// <summary>
    /// 从数据库加载 HNSWIndex
    /// </summary>
    /// <param name="distFnc">HNSWIndex 的距离函数</param>
    /// <param name="failedLabels">SqliteBackend提供了存储失败数据的功能，这里返回失败的 labels</param>
    /// <param name="onProgress">处理进度的回调函数</param>
    /// <param name="token">是否取消</param>
    /// <param name="attachAsIndexBackend">是否将当前的 backend 作为索引的后端</param>
    /// <returns></returns>
    public HNSWIndex? Load(Func<HNSWPoint, HNSWPoint, float> distFnc, List<string>? failedLabels, Action<double>? onProgress = null, CancellationToken? token = null, bool attachAsIndexBackend = true)
    {
        // 读取配置
        int? entryPointId = GetConfig<int?>(Key_EntryPoint, (stream) => BinarySerializer.DeserializeInt32(stream));
        int? capacity = GetConfig<int?>(Key_Capacity, (stream) => BinarySerializer.DeserializeInt32(stream));
        HNSWParameters? parameters = GetConfig<HNSWParameters?>(Key_Parameters, (stream) =>
        {
            int flag = BinarySerializer.DeserializeInt32(stream);
            if (flag == 0) return null;
            return HNSWParameters.Deserialize(stream);
        });

        if (entryPointId == null || capacity == null) return null;

        var nodes = GetAllNodes(token);
        var points = GetAllPoints(failedLabels,token);
        var removedIndexes = GetAllRemovedIndexes();

        if(token?.IsCancellationRequested ?? false)
        {
            return null;
        }

        var dict = new ConcurrentDictionary<int, HNSWPoint>();
        foreach (var kv in points)
        {
            dict[kv.Key] = kv.Value;
        }

        if(token?.IsCancellationRequested ?? false)
        {
            return null;
        }

        HNSWIndexSnapshot snapshot = new HNSWIndexSnapshot();
        snapshot.Parameters = parameters;
        snapshot.DataSnapshot = new GraphDataSnapshot
        {
            EntryPointId = entryPointId.Value,
            Capacity = capacity.Value,
            Nodes = nodes, Items = dict,
            RemovedIndexes = removedIndexes
        };

        IBackend? backend = attachAsIndexBackend ? this : null;

        return new HNSWIndex(distFnc, snapshot, backend);
    }

    public void AddRemovedIndex(int index)
    {
        ExcuteCount++;
        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
            INSERT OR IGNORE INTO [{tableNameOfRemovedIndexes}] ([Index])
            VALUES (@index);
        ";
            cmd.Parameters.AddWithValue("@index", index);
            cmd.ExecuteNonQuery();
        }
    }

    public void RemoveRemovedIndex(int index)
    {
        ExcuteCount++;

        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
            DELETE FROM [{tableNameOfRemovedIndexes}]
            WHERE [Index] = @index;
        ";
            cmd.Parameters.AddWithValue("@index", index);
            cmd.ExecuteNonQuery();
        }
    }

    #region 内部方法

    protected List<Node> GetAllNodes(CancellationToken? cancellationToken = null)
    {
        ExcuteCount++;

        var nodes = new List<Node>();
        lock (conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT [Id], [Data] FROM [{tableNameOfNodes}] ORDER BY [Id]";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    break;
                }

                // 读取二进制数据并反序列化为 Node
                var data = (byte[])reader["Data"];
                using var ms = new MemoryStream(data);
                var node = Node.Deserialize(ms); // 假设你有 Node.Deserialize(Stream) 方法
                nodes.Add(node);
            }
        }
        return nodes;
    }

    protected List<KeyValuePair<int,HNSWPoint>> GetAllPoints(List<string>? failedLabels = null, CancellationToken? cancellationToken = null)
    {
        ExcuteCount++;

        var points = new List<KeyValuePair<int, HNSWPoint>>();

        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT [Label], [Data] FROM [{tableNameOfPoints}]";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    break;
                }

                // 读取二进制数据并反序列化为 HNSWPoint
                var label = reader["Label"].ToString() ?? string.Empty;
                var data = (byte[])reader["Data"];
                using var ms = new MemoryStream(data);

                var point = HNSWPointSqliteSerializer.DeserializePoint(label, ms);

                if (point.IsEmpty)
                {
                    failedLabels?.Add(label);
                }
                else
                {
                    points.Add(new KeyValuePair<int, HNSWPoint>(point.Id, point));
                }
            }
        }

        return points;
    }

    protected Queue<int> GetAllRemovedIndexes()
    {
        ExcuteCount++;

        var indexes = new Queue<int>();

        lock(conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT [Index] FROM [{tableNameOfRemovedIndexes}]";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                indexes.Enqueue((int)(long)reader["Index"]);
            }
        }
        return indexes;
    }

    #endregion

    #region IDisposable Support

    protected virtual void DisposeCore()
    {
        if (!disposedValue)
        {
            Console.WriteLine($"SqliteBackend DisposeCore called.");
            disposedValue = true;

            try
            {
                lock(conn)
                    conn.Close();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"SqliteBackend DisposeCore Close Exception: {ex.Message}");
            }
        }
    }

    ~SqliteBackend()
    {
        Console.WriteLine($"SqliteBackend Finalizer called.");
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        DisposeCore();
    }

    public void Dispose()
    {
        Console.WriteLine($"SqliteBackend Dispose called.");
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    #endregion
}
