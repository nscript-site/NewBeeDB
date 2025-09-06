using ConsoleAppFramework;

namespace NewBeeDB.Tools;

internal class Program
{
    static void Main(string[] args)
    {
        var app = ConsoleApp.Create();
        app.Add("stress", RunStressTest);
        app.Add("load", Load);
        app.Run(args);
    }

    /// <summary>
    /// Perform stress testing
    /// </summary>
    /// <param name="count">the number of inserted samples</param>
    /// <param name="dim">the feature dimension of each sample</param>
    /// <param name="mode">backend mode. 0: no backend, 1: sqlite backend in memory, 2: sqlite backend using file</param>
    static void RunStressTest(int count = 1000, int dim = 512, int mode = 0)
    {
        var test = new StressTest();
        test.Count = count;
        test.Dimension = dim;
        test.BackendMode = mode;
        test.Run();
    }

    /// <summary>
    /// Load index
    /// </summary>
    /// <param name="input">path of index file</param>
    static void Load(string input)
    {
        var load = new LoadIndex();
        load.Path = input;
        load.Run();
    }
}
