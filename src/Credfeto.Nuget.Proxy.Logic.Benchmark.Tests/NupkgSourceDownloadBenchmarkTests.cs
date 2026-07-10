using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Nuget.Proxy.Logic.Benchmark.Tests;

public sealed class NupkgSourceDownloadBenchmarkTests : LoggingTestBase
{
    public NupkgSourceDownloadBenchmarkTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void DownloadAndCacheMiss_AllocatesBelowStreamingThreshold()
    {
        (Summary summary, AccumulationLogger logger) = Benchmark<NupkgSourceDownloadBenchmark>();

        this.Output.WriteLine(logger.GetLog());

        // Measured ~5.46 KB/op for a 256 KB payload (streaming avoids buffering the payload in memory); 16 KB leaves headroom for CI variance.
        summary.AssertAllocationsAtMost(maximumBytes: 16_384);
    }
}
