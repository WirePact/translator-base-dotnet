using System;
using GlobExpressions;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s
            .SetProjectFile(Solution)));

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .EnableNoRestore()));

    Target Test => _ => _
        .DependsOn(Clean, Compile)
        .Executes(() => DotNetTest(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .SetProperty("CollectCoverage", true)
            .EnableNoRestore()
            .EnableNoBuild()));

    #region Library Project

    const short MaxReleaseNoteLength = 30000;

    [Parameter("Version of the nuget package to build.")] readonly string Version = string.Empty;

    [Parameter("Optional release notes to append.")] readonly string ReleaseNotes = string.Empty;

    [Parameter("Name of the environment variable that contains the api key.")] readonly string ApiKeyEnv = string.Empty;

    [Parameter("Name of the environment variable that contains the nuget source.")]
    readonly string SourceEnv = string.Empty;

    string PackageReleaseNotes => (ReleaseNotes.Length > MaxReleaseNoteLength
            ? ReleaseNotes.Substring(0, MaxReleaseNoteLength)
            : ReleaseNotes)
        .Replace(",", "%2c")
        .Replace(";", "%3b");

    Target Pack => _ => _
        .DependsOn(Clean, Restore)
        .Requires(() => !string.IsNullOrWhiteSpace(Version))
        .Executes(() => DotNetPack(s => s
            .SetConfiguration(Configuration.Release)
            .SetVersion(Version)
            .SetPackageReleaseNotes(PackageReleaseNotes)
            .SetOutputDirectory(ArtifactsDirectory)
            .SetProject(Solution)));

    Target NugetPublish => _ => _
        .Requires(
            () => !string.IsNullOrWhiteSpace(ApiKeyEnv) &&
                  !string.IsNullOrWhiteSpace(SourceEnv) &&
                  !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyEnv)) &&
                  !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SourceEnv)))
        .Executes(() => DotNetNuGetPush(s => s
            .SetSource(Environment.GetEnvironmentVariable(SourceEnv) ?? string.Empty)
            .SetApiKey(Environment.GetEnvironmentVariable(ApiKeyEnv) ?? string.Empty)
            .CombineWith(Glob.Files(ArtifactsDirectory, "*.nupkg"), (ss, package) => ss
                .SetTargetPath(ArtifactsDirectory / package))));

    #endregion
}
