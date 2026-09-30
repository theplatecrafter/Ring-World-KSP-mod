param(
    [string]$KspRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'template_instance'),
    [switch]$Install,
    [switch]$SmokeTest,
    [switch]$Package
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$gameRoot = (Resolve-Path -LiteralPath $KspRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'KSP_x64_Data\Managed\Assembly-CSharp.dll'))) { throw 'KspRoot is not a KSP 1 installation.' }
& dotnet run --project (Join-Path $taskRoot 'tests\Ringworld.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core verification failed.' }
$buildArguments = @('build', (Join-Path $taskRoot 'src\Ringworld.KSP\Ringworld.KSP.csproj'), '-c', 'Release', '--nologo', "-p:KspRoot=$gameRoot")
if ($SmokeTest) { $buildArguments += '-p:SmokeTest=true' }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
# A fresh package stage prevents stale experimental files entering a release.
$distributionRoot = Join-Path $taskRoot 'artifacts\NivenRingworld'
if ($Package) { $distributionRoot = Join-Path $taskRoot ('artifacts\package-stage-' + [Guid]::NewGuid().ToString('N')) }
$stage = Join-Path $distributionRoot 'GameData\NivenRingworld'
New-Item -ItemType Directory -Path (Join-Path $stage 'Plugins') -Force | Out-Null
Copy-Item -Path (Join-Path $taskRoot 'GameData\NivenRingworld\*') -Destination $stage -Recurse -Force
$compiled = Join-Path $taskRoot 'src\Ringworld.KSP\bin\Release\net472'
foreach ($dll in @('NivenRingworld.dll','Ringworld.Core.dll')) { Copy-Item -LiteralPath (Join-Path $compiled $dll) -Destination (Join-Path $stage 'Plugins') -Force }
if ($Install) {
    $target = Join-Path $gameRoot 'GameData\NivenRingworld'
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    # This shipped configuration moved to the optional Clouds package.
    $obsoleteCloudConfig=Join-Path $target 'Extensions/CloudTypes.cfg'
    if(Test-Path -LiteralPath $obsoleteCloudConfig){Copy-Item -LiteralPath $obsoleteCloudConfig -Destination (Join-Path $taskRoot ('artifacts/legacy-cloud-config-'+[Guid]::NewGuid().ToString('N')+'.cfg'));Remove-Item -LiteralPath $obsoleteCloudConfig}
    Copy-Item -Path (Join-Path $stage '*') -Destination $target -Recurse -Force
    Write-Host "Installed into $target"
}
if ($Package -and -not $SmokeTest) {
    # Include the documentation tree automatically so reorganization cannot omit guides.
    foreach ($doc in @('README.md','RELEASE-NOTES.md','LICENSE','THIRD-PARTY-NOTICES.md','CREDITS.md')) {
        Copy-Item -LiteralPath (Join-Path $taskRoot $doc) -Destination (Join-Path $distributionRoot $doc) -Force
    }
    Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination (Join-Path $distributionRoot 'docs') -Recurse -Force
    $releaseVersion = ([xml](Get-Content -LiteralPath (Join-Path $taskRoot 'src\Ringworld.KSP\Ringworld.KSP.csproj') -Raw)).Project.PropertyGroup.Version
    $unexpected = Get-ChildItem -LiteralPath (Join-Path $distributionRoot 'GameData') | Where-Object Name -ne 'NivenRingworld'
    if ($unexpected) { throw 'Release contains an unexpected bundled mod.' }
    Compress-Archive -Path (Join-Path $distributionRoot '*') -DestinationPath (Join-Path $taskRoot "artifacts\NivenRingworld-$releaseVersion.zip") -Force
}
Write-Host "Build staged in $stage"



