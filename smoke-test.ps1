param([switch]$SkipIssue3,[int]$TimeoutSeconds=900,[switch]$InstalledIssuesOnly,[switch]$IntegrationsOnly,[switch]$MapOnly,[switch]$TerrainOnly,[switch]$WarpOnly,[switch]$UnmatchedOnly,[switch]$PhotoOnly,[switch]$DistantOnly,[switch]$WeatherOnly,[switch]$SceneryOnly,[switch]$GlobalCloudsOnly,[switch]$ResidenceOnly,[switch]$GuidanceOnly,[switch]$StabilityOnly,[switch]$LandmarksOnly,[switch]$GearOnly,[switch]$CylaOnly,[switch]$CylaDiagnosticOnly,[switch]$CylaSaveProbe,[switch]$TrackingOnly,[switch]$ReentryOnly,[switch]$RenderOnly,[switch]$VisualOptionsOnly,[switch]$WallOnly,[switch]$ScienceOnly,[switch]$MultiRingOnly,[switch]$WithoutCyla,[switch]$AsteroidAnchor)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$gameRoot=Join-Path (Split-Path -Parent $taskRoot) 'template_instance'
$logName='RingworldSmoke-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log'
$logPath=Join-Path $gameRoot $logName
$testProcess=$null
$cylaBackup=$null
try {
    if ($WithoutCyla) {
        $cylaPath=Join-Path $gameRoot 'GameData\Cyla'
        if (Test-Path -LiteralPath $cylaPath) {
            $resolvedCyla=(Resolve-Path -LiteralPath $cylaPath).Path
            $expectedCyla=[System.IO.Path]::GetFullPath((Join-Path $gameRoot 'GameData\Cyla'))
            if ($resolvedCyla -ne $expectedCyla) { throw 'Unexpected Cyla test path.' }
            $cylaBackup=Join-Path $taskRoot ('artifacts\cyla-test-backup-' + [Guid]::NewGuid().ToString('N'))
            Move-Item -LiteralPath $resolvedCyla -Destination $cylaBackup
        }
    }
    & (Join-Path $taskRoot 'build.ps1') -Install -SmokeTest
    $taskArguments=@('-ringworld-smoketest','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-popupwindow','-logFile',$logName)
    if ($SkipIssue3) { $taskArguments += '-ringworld-skip-issue3' }
    if ($InstalledIssuesOnly) { $taskArguments += '-ringworld-installed-issues-only' }
    if ($IntegrationsOnly) { $taskArguments += '-ringworld-integrations-only' }
    if ($WithoutCyla) { $taskArguments += '-ringworld-no-cyla' }
    if ($AsteroidAnchor) { $taskArguments += '-ringworld-asteroid-anchor' }
    if ($MultiRingOnly) { $taskArguments += '-ringworld-multi-ring-only' }
    if ($ScienceOnly) { $taskArguments += '-ringworld-science-only' }
    if ($WallOnly) { $taskArguments += '-ringworld-wall-only' }
    if ($VisualOptionsOnly) { $taskArguments += '-ringworld-visual-options-only' }
    if ($ReentryOnly -or $RenderOnly) { $taskArguments += '-ringworld-reentry-only' }
    if ($RenderOnly) { $taskArguments += '-ringworld-render-only' }
    if ($TrackingOnly) { $taskArguments += '-ringworld-tracking-only' }
    if ($CylaSaveProbe) { $taskArguments += '-ringworld-cyla-save-probe' }
    if ($CylaDiagnosticOnly) { $taskArguments += '-ringworld-cyla-diagnostic' }
    if ($CylaOnly) { $taskArguments += '-ringworld-cyla-only' }
    if ($GearOnly) { $taskArguments += '-ringworld-gear-only' }
    if ($LandmarksOnly) { $taskArguments += '-ringworld-landmarks-only' }
    if ($StabilityOnly) { $taskArguments += '-ringworld-stability-only' }
    if ($GuidanceOnly) { $taskArguments += '-ringworld-guidance-only' }
    if ($ResidenceOnly) { $taskArguments += '-ringworld-residence-only' }
    if ($WeatherOnly) { $taskArguments += '-ringworld-weather-only' }
    if ($GlobalCloudsOnly) { $taskArguments += '-ringworld-global-clouds-only' }
    if ($SceneryOnly) { $taskArguments += '-ringworld-scenery-only' }
    if ($DistantOnly) { $taskArguments += '-ringworld-distant-only' }
    if ($PhotoOnly -or $DistantOnly) { $taskArguments += '-ringworld-photo-only' }
    if ($UnmatchedOnly) { $taskArguments += '-ringworld-unmatched-only' }
    if ($MapOnly) { $taskArguments += '-ringworld-map-only' }
    if ($TerrainOnly -or $WarpOnly -or $WeatherOnly) { $taskArguments += '-ringworld-terrain-only' }
    if ($WarpOnly) { $taskArguments += '-ringworld-warp-only' }
    $testProcess=Start-Process -FilePath (Join-Path $gameRoot 'KSP_x64.exe') -WorkingDirectory $gameRoot -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    if (-not $testProcess.WaitForExit($TimeoutSeconds*1000)) {
        $testProcess.Kill()
        $testProcess.WaitForExit()
        throw "Game smoke test timed out. Log: $logPath"
    }
    $text=Get-Content -LiteralPath $logPath -Raw
    $reportDir=Join-Path $taskRoot 'artifacts\validation'
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    $reportName='game-smoke.txt'
    if ($InstalledIssuesOnly) { $reportName='installed-issues-smoke.txt' }
    if ($IntegrationsOnly) { $reportName='integrations-smoke.txt' }
    if ($AsteroidAnchor) { $taskArguments += '-ringworld-asteroid-anchor' }
    if ($MultiRingOnly) { $reportName='multi-ring-smoke.txt' }
    if ($ScienceOnly) { $reportName='science-smoke.txt' }
    if ($WallOnly) { $reportName='wall-smoke.txt' }
    if ($VisualOptionsOnly) { $reportName='visual-options-smoke.txt' }
    if ($ReentryOnly) { $reportName='reentry-smoke.txt' }
    if ($RenderOnly) { $reportName='render-smoke.txt' }
    if ($TrackingOnly) { $reportName='tracking-smoke.txt' }
    if ($CylaDiagnosticOnly) { $reportName='cyla-diagnostic.txt' }
    if ($CylaOnly) { $reportName='cyla-smoke.txt' }
    if ($GearOnly) { $reportName='gear-smoke.txt' }
    if ($LandmarksOnly) { $reportName='landmarks-smoke.txt' }
    if ($StabilityOnly) { $reportName='stability-smoke.txt' }
    if ($GuidanceOnly) { $reportName='guidance-smoke.txt' }
    if ($ResidenceOnly) { $reportName='residence-smoke.txt' }
    if ($PhotoOnly) { $reportName='photo-smoke.txt' }
    if ($DistantOnly) { $reportName='distant-smoke.txt' }
    if ($UnmatchedOnly) { $reportName='unmatched-smoke.txt' }
    if ($MapOnly) { $reportName='map-smoke.txt' }
    if ($TerrainOnly) { $reportName='terrain-smoke.txt' }
    if ($WarpOnly) { $reportName='warp-smoke.txt' }
    if ($WeatherOnly) { $reportName='weather-smoke.txt' }
    if ($GlobalCloudsOnly) { $reportName='global-clouds-smoke.txt' }
    if ($SceneryOnly) { $reportName='scenery-smoke.txt' }
    $text -split '\r?\n' | Where-Object { $_ -match '\[RingworldSmoke\]|\[NivenRingworld\]' } | Set-Content -LiteralPath (Join-Path $reportDir $reportName)
    if ($GearOnly -and $SceneryOnly -and $LandmarksOnly -and $GuidanceOnly) {
        foreach ($name in @('gear','guidance','scenery','landmarks')) {
            $text -split '\r?\n' | Where-Object { $_ -match '\[RingworldSmoke\]|\[NivenRingworld\]' } | Set-Content -LiteralPath (Join-Path $reportDir ($name+'-smoke.txt'))
        }
    }
    if ($SceneryOnly -and $LandmarksOnly -and $GuidanceOnly -and -not $GearOnly) {
        foreach ($name in @('guidance','scenery','landmarks')) {
            $text -split '\r?\n' | Where-Object { $_ -match '\[RingworldSmoke\]|\[NivenRingworld\]' } | Set-Content -LiteralPath (Join-Path $reportDir ($name+'-smoke.txt'))
        }
    }
    if ($text -notmatch '\[RingworldSmoke\] PASS' -or $text -match '\[RingworldSmoke\] FAIL') { throw "Game smoke test failed. Log: $logPath" }
    Write-Host "Game smoke test passed. Log: $logPath"
}
finally {
    if ($cylaBackup -and (Test-Path -LiteralPath $cylaBackup)) {
        if ($testProcess -and -not $testProcess.HasExited) { $testProcess.Kill();$testProcess.WaitForExit() }
        Move-Item -LiteralPath $cylaBackup -Destination (Join-Path $gameRoot 'GameData\Cyla')
    }
    if ($testProcess -and -not $testProcess.HasExited) { $testProcess.Kill(); $testProcess.WaitForExit() }
    & (Join-Path $taskRoot 'build.ps1') -Install
}
