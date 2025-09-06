using System.IO;
using System.IO.Compression;

namespace NewBeeDB;

internal class HNSWIndexZipFileSerializer
{
    public static void Serialize(HNSWIndex index, string zipFilePath, string indexName = "default", int sliceMaxCount = 500000)
    {
        string entryKey = $"{indexName}.hnswindex";
        HNSWIndexSnapshotWithSlices slices = HNSWIndexSnapshotWithSlices.CreateFrom(index, sliceMaxCount);

        using (var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Update))
        {
            List<ZipArchiveEntry>? existingEntries = null;

            // 查找旧的条目
            foreach (var entry in zip.Entries)
            {
                if (entry.Name.StartsWith(entryKey))
                {
                    if(existingEntries == null)
                    {
                        existingEntries = new List<ZipArchiveEntry>();
                    }

                    existingEntries.Add(entry);
                }
            }

            // 删除旧的条目
            if (existingEntries != null)
            {
                foreach (var entry in existingEntries)
                {
                    entry.Delete();
                }
            }

            // 创建新的条目
            var mainEntry = zip.CreateEntry(entryKey);
            using var stream = mainEntry.Open();
            if (stream != null)
            {
                slices.Snapshot.Serialize(stream);
            }

            int n = 0;
            foreach(var slice in slices.Slices)
            {
                n++;
                string sliceEntryKey = $"{entryKey}.slice{n}";
                var sliceEntry = zip.CreateEntry(sliceEntryKey);
                using var sliceStream = sliceEntry.Open();
                if (sliceStream != null)
                {
                    slice.Serialize(sliceStream);
                }
            }
        }
    }

    public static HNSWIndexSnapshotWithSlices? Deserialize(string zipFilePath, string indexName = "default")
    {
        string entryKey = $"{indexName}.hnswindex";
        using (var zip = ZipFile.OpenRead(zipFilePath))
        {
            HNSWIndexSnapshot? body = null;

            var mainEntry = zip.GetEntry(entryKey);
            if (mainEntry != null)
            {
                using var stream = mainEntry.Open();
                body = HNSWIndexSnapshot.Deserialize(stream);
            }

            if(body == null)
            {
                return null; // 没有找到主条目
            }

            HNSWIndexSnapshotWithSlices slices = new HNSWIndexSnapshotWithSlices
            {
                Snapshot = body
            };

            var sliceKeyPrefix = $"{entryKey}.slice";

            foreach (var entry in zip.Entries)
            {
                if(entry.Name.StartsWith(sliceKeyPrefix))
                {
                    using var sliceStream = entry.Open();
                    var slice = HNSWIndexSlice.Deserialize(sliceStream);
                    if (slice != null)
                    {
                        slices.Slices.Add(slice);
                    }
                }
            }

            return slices;
        }
    }

    /// <summary>
    /// 只解析节点数量
    /// </summary>
    /// <param name="zipFilePath"></param>
    /// <param name="indexName"></param>
    /// <returns></returns>
    public static int DeserializeNodeCount(string zipFilePath, string indexName = "default")
    {
        int sum = 0;
        string entryKey = $"{indexName}.hnswindex";
        using (var zip = ZipFile.OpenRead(zipFilePath))
        {
            var sliceKeyPrefix = $"{entryKey}.slice";
            foreach (var entry in zip.Entries)
            {
                if (entry.Name.StartsWith(sliceKeyPrefix))
                {
                    using var sliceStream = entry.Open();
                    var count = HNSWIndexSlice.DeserializeNodeCount(sliceStream);
                    sum += count;
                }
            }

            return sum;
        }
    }
}

