param(
 [string]$KspRoot=(Join-Path $PSScriptRoot '../../Ringworld Modpacks/artifacts/validation-game-1ffee7f947f84b39a4c62f0cb6db6857'),
 [string]$Run='1',
 [string]$ReporterSave
)
$ErrorActionPreference='Stop'
$perfBase=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$perfGame=(Resolve-Path -LiteralPath $KspRoot).Path
if(Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue){throw 'Close KSP before this private regression run.'}
$perfBackup=Join-Path $perfBase ('artifacts/issue7-backup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $perfBackup | Out-Null
$perfTarget=Join-Path $perfGame 'GameData/NivenRingworld/Plugins'
foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $perfTarget $file) -Destination $perfBackup}
Copy-Item -LiteralPath (Join-Path $perfGame 'settings.cfg') -Destination $perfBackup
$perfProcess=$null
$perfReporterPath=Join-Path $perfGame 'ReporterLagSource.sfs'
$perfReporterHash=$null
try{
 if($ReporterSave){
  if(Test-Path -LiteralPath $perfReporterPath){throw 'Reporter fixture target already exists.'}
  $perfReporterHash=(Get-FileHash -LiteralPath $ReporterSave -Algorithm SHA256).Hash
  Copy-Item -LiteralPath $ReporterSave -Destination $perfReporterPath
 }
 & (Join-Path $perfBase 'build.ps1') -KspRoot $perfGame -SmokeTest
 if($LASTEXITCODE -ne 0){throw 'Issue7 smoke build failed.'}
 foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $perfBase "src/Ringworld.KSP/bin/Release/net472/$file") -Destination $perfTarget -Force}
 $perfLog="Issue7Performance-$Run.log"
 $perfArgs=@('-ringworld-smoketest','-ringworld-issue7-only','-ringworld-profile','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-popupwindow','-logFile',$perfLog)
 if($ReporterSave){$perfArgs+='-ringworld-reporter-lag-only'}
 $perfProcess=Start-Process -FilePath (Join-Path $perfGame 'KSP_x64.exe') -WorkingDirectory $perfGame -WindowStyle Hidden -ArgumentList $perfArgs -PassThru
 Write-Output "START issue7 performance PID $($perfProcess.Id)"
 $perfDeadline=[DateTime]::UtcNow.AddMinutes(15)
 while(-not $perfProcess.HasExited){
  if([DateTime]::UtcNow -gt $perfDeadline){Stop-Process -Id $perfProcess.Id;throw 'Issue7 performance runtime timed out.'}
  Start-Sleep -Seconds 2;$perfProcess.Refresh()
 }
 $perfLines=Get-Content -LiteralPath (Join-Path $perfGame $perfLog)
 $perfLines | Select-String '\[RingworldSmoke\] (PASS|FAIL|REPORTER_TIMING)|\[NivenRingworld\] PERF' | ForEach-Object {$_.Line}
 $perfComplete=if($ReporterSave){'\[RingworldSmoke\] PASS reporter-derived lag workload complete'}else{'\[RingworldSmoke\] PASS issue7 performance regression complete'}
 if(($perfLines | Select-String '\[RingworldSmoke\] FAIL') -or -not($perfLines | Select-String $perfComplete)){throw 'Issue7 performance regression did not complete.'}
}finally{
 if($perfProcess -and -not $perfProcess.HasExited){Stop-Process -Id $perfProcess.Id}
 foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $perfBackup $file) -Destination $perfTarget -Force}
 Copy-Item -LiteralPath (Join-Path $perfBackup 'settings.cfg') -Destination (Join-Path $perfGame 'settings.cfg') -Force
 if($ReporterSave -and $perfReporterHash){Remove-Item -LiteralPath $perfReporterPath;if((Get-FileHash -LiteralPath $ReporterSave -Algorithm SHA256).Hash -ne $perfReporterHash){throw 'Original reporter save changed.'}}
 & (Join-Path $perfBase 'build.ps1')
 Write-Output 'Private-game DLLs/settings and normal source build restored; release ZIPs unchanged.'
}
