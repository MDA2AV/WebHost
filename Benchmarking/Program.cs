using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarking;

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkRunner.Run<HeaderEncodingBenchmark>(config);
        //BenchmarkRunner.Run<CreateDataFrameBenchmark>(config);
    }
}