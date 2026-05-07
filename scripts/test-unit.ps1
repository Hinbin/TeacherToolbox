[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Filter,
    [switch]$NoBuild,
    [switch]$Coverage,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetArgs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "TeacherToolbox.UnitTests/TeacherToolbox.UnitTests.csproj"

$x86DotnetRoot = ${env:DOTNET_ROOT(x86)}
if ([string]::IsNullOrWhiteSpace($x86DotnetRoot)) {
    $x86DotnetRoot = [Environment]::GetEnvironmentVariable("DOTNET_ROOT(x86)", "User")
}

if ([string]::IsNullOrWhiteSpace($x86DotnetRoot) -or -not (Test-Path $x86DotnetRoot)) {
    throw "DOTNET_ROOT(x86) is not set to a valid path. Expected something like 'C:\Program Files (x86)\dotnet'."
}

${env:DOTNET_ROOT(x86)} = $x86DotnetRoot

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Could not find vswhere.exe. Install Visual Studio or Build Tools with MSBuild support."
}

$vsInstallPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if ([string]::IsNullOrWhiteSpace($vsInstallPath)) {
    throw "Could not find a Visual Studio installation with MSBuild support."
}

$appxPackageDir = Join-Path $vsInstallPath "MSBuild/Microsoft/VisualStudio/v17.0/AppxPackage"
$priTaskAssembly = Join-Path $appxPackageDir "Microsoft.Build.Packaging.Pri.Tasks.dll"
$appxTaskAssembly = Join-Path $appxPackageDir "Microsoft.Build.AppxPackage.dll"

if (-not (Test-Path $priTaskAssembly)) {
    throw "Could not find Windows App SDK packaging tasks under '$appxPackageDir'. Install the Visual Studio UWP/Windows App SDK build tools."
}

if (-not (Test-Path $appxTaskAssembly)) {
    throw "Could not find Appx package MSBuild tasks under '$appxPackageDir'. Install the Visual Studio UWP/Windows App SDK build tools."
}

$arguments = @(
    "test",
    $testProject,
    "-p:Platform=x86",
    "--arch",
    "x86",
    "--configuration",
    $Configuration,
    "-p:AppxMSBuildToolsPath=$appxPackageDir",
    "-p:PriProjTaskAssembly=$priTaskAssembly",
    "-p:AppxMSBuildTaskAssembly=$appxTaskAssembly"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $arguments += @("--filter", $Filter)
}

if ($Coverage) {
    $arguments += @(
        "--collect", "XPlat Code Coverage",
        "--",
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[TeacherToolbox]*",
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute=GeneratedCodeAttribute"
    )
}

if ($DotnetArgs) {
    $arguments += $DotnetArgs
}

& dotnet @arguments
exit $LASTEXITCODE
