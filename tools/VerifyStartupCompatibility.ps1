param([string]$KspRoot=(Join-Path $PSScriptRoot '../../Ringworld Modpacks/artifacts/validation-game-1ffee7f947f84b39a4c62f0cb6db6857'),[string]$Run='1',[switch]$Issue6,[switch]$DegradedStartup,[switch]$SceneTransitions,[switch]$SolverPlane,[switch]$FreshScenes,[switch]$RetainedSaves)
$ErrorActionPreference='Stop'
$startupBase=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$startupGame=(Resolve-Path -LiteralPath $KspRoot).Path
if(Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue){throw 'Close KSP before this private regression run.'}
$startupBackup=Join-Path $startupBase ('artifacts/startup-backup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $startupBackup | Out-Null
$startupTarget=Join-Path $startupGame 'GameData/NivenRingworld/Plugins'
foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $startupTarget $file) -Destination $startupBackup}
Copy-Item -LiteralPath (Join-Path $startupGame 'settings.cfg') -Destination $startupBackup
$startupProcess=$null
$startupResourceProbe=$null
$startupSaveProbes=@()
try{
 if($Issue6 -and (Test-Path -LiteralPath (Join-Path $startupGame 'GameData/CommunityResourcePack'))){
  $startupProbeDirectory=Join-Path $startupGame ('GameData/RingworldCompatibilityProbe-'+[Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $startupProbeDirectory | Out-Null
  $startupResourceProbe=Join-Path $startupProbeDirectory 'Resources.cfg'
  # The released CRP pack supplies definitions, not Water deposits. Exercise
  # a real community-authored style cfg through ModuleManager/ResourceCache.
  @'
GLOBAL_RESOURCE
{
 ResourceName = Water
 ResourceType = 0
 Distribution
 {
  PresenceChance = 100
  MinAbundance = 10
  MaxAbundance = 10
  Variance = 0
  Dispersal = 1
 }
}
PLANETARY_RESOURCE
{
 ResourceName = Water
 ResourceType = 0
 PlanetName = Ringworld:primary
 Distribution
 {
  PresenceChance = 100
  MinAbundance = 50
  MaxAbundance = 50
  Variance = 0
  Dispersal = 1
 }
}
BIOME_RESOURCE
{
 ResourceName = Water
 ResourceType = 0
 PlanetName = Ringworld:primary
 BiomeName = Grassland
 Distribution
 {
  PresenceChance = 100
  MinAbundance = 20
  MaxAbundance = 20
  Variance = 0
  Dispersal = 1
 }
}
PLANETARY_RESOURCE
{
 ResourceName = Water
 ResourceType = 0
 PlanetName = Sun
 Distribution
 {
  PresenceChance = 0
  MinAbundance = 0
  MaxAbundance = 0
  Variance = 0
  Dispersal = 1
 }
}
'@ | Set-Content -LiteralPath $startupResourceProbe
 }
 if($RetainedSaves){
  if(-not $Issue6 -or -not $startupResourceProbe -or $FreshScenes -or $SolverPlane){throw 'RetainedSaves requires Issue6 with its CRP fixture and a resident test.'}
  foreach($retained in @(@('RingworldSmoke-20260914-211423','format3'),@('RingworldSmoke-20260916-033914','format4'),@('default','format5-pre118'))){
   $retainedSource=(Resolve-Path -LiteralPath (Join-Path $startupBase ('../template_instance/saves/'+$retained[0]+'/persistent.sfs'))).Path
   $retainedCopy=Join-Path $startupProbeDirectory ('Retained-'+$retained[1]+'.sfs')
   $startupSaveProbes+= [pscustomobject]@{Source=$retainedSource;Copy=$retainedCopy;Hash=(Get-FileHash -LiteralPath $retainedSource -Algorithm SHA256).Hash}
   Copy-Item -LiteralPath $retainedSource -Destination $retainedCopy
  }
  $startupSaveProbes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $startupBackup 'retained-provenance.json')
 }
 & (Join-Path $startupBase 'build.ps1') -KspRoot $startupGame -SmokeTest
 if($LASTEXITCODE -ne 0){throw 'Startup smoke build failed.'}
 foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $startupBase "src/Ringworld.KSP/bin/Release/net472/$file") -Destination $startupTarget -Force}
 $startupLog="StartupCompatibility-$Run.log"
 $startupMode=if($Issue6){'-ringworld-issue6-only'}else{'-ringworld-startup-compatibility-only'}
 $startupArgs=@('-ringworld-smoketest',$startupMode,'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-popupwindow','-logFile',$startupLog)
 if($DegradedStartup){if(-not $Issue6){throw 'DegradedStartup requires Issue6.'};$startupArgs+='-ringworld-issue6-degraded-startup'}
 if($SceneTransitions){if(-not $Issue6){throw 'SceneTransitions requires Issue6.'};$startupArgs+='-ringworld-issue6-scenes-only'}
 if($FreshScenes){if(-not $Issue6 -or $SceneTransitions -or $SolverPlane -or $DegradedStartup){throw 'FreshScenes requires Issue6 without another scene/plane/degraded mode.'};$startupArgs+='-ringworld-issue6-fresh-scenes'}
 if($SolverPlane){if(-not $Issue6 -or $DegradedStartup){throw 'SolverPlane requires Issue6 and an enabled stock environment.'};$startupArgs+='-ringworld-solver-plane-only'}
 $startupProcess=Start-Process -FilePath (Join-Path $startupGame 'KSP_x64.exe') -WorkingDirectory $startupGame -WindowStyle Hidden -ArgumentList $startupArgs -PassThru
 Write-Output "START startup compatibility PID $($startupProcess.Id)"
 $startupDeadline=[DateTime]::UtcNow.AddMinutes(15)
 while(-not $startupProcess.HasExited){
  if([DateTime]::UtcNow -gt $startupDeadline){Stop-Process -Id $startupProcess.Id;throw 'Startup compatibility runtime timed out.'}
  Start-Sleep -Seconds 2;$startupProcess.Refresh()
 }
 $startupLines=Get-Content -LiteralPath (Join-Path $startupGame $startupLog)
 $startupLines | Select-String '\[RingworldSmoke\] (PASS|FAIL)' | ForEach-Object {$_.Line}
 if(($startupLines | Select-String '\[RingworldSmoke\] FAIL') -or -not($startupLines | Select-String '\[RingworldSmoke\] PASS startup compatibility regression complete')){throw 'Startup compatibility regression did not complete.'}
}finally{
 if($startupProcess -and -not $startupProcess.HasExited){Stop-Process -Id $startupProcess.Id}
 foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $startupBackup $file) -Destination $startupTarget -Force}
 Copy-Item -LiteralPath (Join-Path $startupBackup 'settings.cfg') -Destination (Join-Path $startupGame 'settings.cfg') -Force
 foreach($retained in $startupSaveProbes){Remove-Item -LiteralPath $retained.Copy;if((Get-FileHash -LiteralPath $retained.Source -Algorithm SHA256).Hash -ne $retained.Hash){throw 'Original retained save changed during private regression.'}}
 if($startupResourceProbe){Remove-Item -LiteralPath $startupResourceProbe;Remove-Item -LiteralPath $startupProbeDirectory}
 & (Join-Path $startupBase 'build.ps1')
 Write-Output 'Original private-game DLLs/settings and normal source build restored; release archives unchanged.'
}
