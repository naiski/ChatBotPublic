using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.PowerShell;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Deploy);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    AbsolutePath ProjectFile => RootDirectory / "src" / "Application" / "Application.csproj";
    AbsolutePath StableDiffusionDirectory => RootDirectory / ".." / "stable-diffusion-webui";
    AbsolutePath OobaBoogaDirectory => RootDirectory / ".." / "oobabooga-windows";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetClean(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration));
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration));
        });

    Target Run => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetRun(s => s
                .SetProjectFile(ProjectFile)
                .SetConfiguration(Configuration));
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration));
        });

    Target StartStableDiffusionServer => _ => _
        .Executes(() =>
        {
            PowerShellTasks.PowerShell($"Start-Process {StableDiffusionDirectory / "webui-user.bat"}",
                StableDiffusionDirectory);
        });

    Target StartTextGenerationServer => _ => _
        .Executes(() =>
        {
            PowerShellTasks.PowerShell($"Start-Process {OobaBoogaDirectory / "start-webui.bat"}",
                OobaBoogaDirectory);
        });

    Target StartServers => _ => _
        .Before(Clean, Restore, Compile, Run)
        .DependsOn(StartStableDiffusionServer, StartTextGenerationServer);

    Target Deploy => _ => _
        .DependsOn(StartServers, Run);
}