using System.Reflection;

namespace Vipi.AuroraProfiles.Tests;

/// <summary>
/// Locates the real ProfilesTest folder shipped in the repo by walking up from the test
/// assembly location until it is found. Keeps tests running against real data without
/// duplicating the profiles into the test project.
/// </summary>
public static class TestProfiles
{
    public static string Directory { get; } = Locate();

    public static IEnumerable<string> AllFiles() =>
        System.IO.Directory.EnumerateFiles(Directory, "*.cpr").OrderBy(p => p);

    public static string Path(string fileName) => System.IO.Path.Combine(Directory, fileName);

    private static string Locate()
    {
        var dir = new DirectoryInfo(
            System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "ProfilesTest");
            if (System.IO.Directory.Exists(candidate) &&
                System.IO.Directory.EnumerateFiles(candidate, "*.cpr").Any())
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate the ProfilesTest folder above the test assembly.");
    }
}

/// <summary>xUnit member-data source: every real profile file path.</summary>
public sealed class AllProfilesData : TheoryData<string>
{
    public AllProfilesData()
    {
        foreach (var f in TestProfiles.AllFiles())
            Add(f);
    }
}
