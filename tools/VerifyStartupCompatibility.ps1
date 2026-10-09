param([string]$KspRoot=(Join-Path $PSScriptRoot '../../Ringworld Modpacks/artifacts/validation-game-1ffee7f947f84b39a4c62f0cb6db6857'),[string]$Run='1')
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
try{
 & (Join-Path $startupBase 'build.ps1') -KspRoot $startupGame -SmokeTest
 if($LASTEXITCODE -ne 0){throw 'Startup smoke build failed.'}
 foreach($file in @('NivenRingworld.dll','Ringworld.Core.dll')){Copy-Item -LiteralPath (Join-Path $startupBase "src/Ringworld.KSP/bin/Release/net472/$file") -Destination $startupTarget -Force}
 $startupLog="StartupCompatibility-$Run.log"
 $startupProcess=Start-Process -FilePath (Join-Path $startupGame 'KSP_x64.exe') -WorkingDirectory $startupGame -WindowStyle Hidden -ArgumentList @('-ringworld-smoketest','-ringworld-startup-compatibility-only','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-popupwindow','-logFile',$startupLog) -PassThru
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
 & (Join-Path $startupBase 'build.ps1')
 Write-Output 'Original private-game DLLs/settings and normal source build restored; release archives unchanged.'
}
