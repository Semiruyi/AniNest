using AniNest.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

string repoRoot = ResolveRepoRoot(AppContext.BaseDirectory);
string artifactsPath = Path.Combine(repoRoot, "artifacts", "AniNest.Benchmarks", "BenchmarkDotNet.Artifacts");
var config = DefaultConfig.Instance.WithArtifactsPath(artifactsPath);

BenchmarkRunner.Run<ThumbnailExtractionBenchmarks>(config);

static string ResolveRepoRoot(string startDirectory)
{
    string? directory = startDirectory;
    while (!string.IsNullOrWhiteSpace(directory))
    {
        if (File.Exists(Path.Combine(directory, "AniNest.sln")))
            return directory;

        directory = Directory.GetParent(directory)?.FullName;
    }

    return Path.GetFullPath(Path.Combine(startDirectory, "..", "..", "..", ".."));
}
