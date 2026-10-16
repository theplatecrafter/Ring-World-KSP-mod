param([string]$Run='baseline')
$ErrorActionPreference='Stop'
$frameRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$frameGame=Join-Path $frameRoot 'template_instance'
if(Get-Process KSP_x64 -ErrorAction SilentlyContinue){throw 'Close KSP before this regression.'}
$frameBackup=Join-Path $frameRoot ('artifacts/frame-backup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $frameBackup | Out-Null
Copy-Item -LiteralPath (Join-Path $frameGame 'settings.cfg') -Destination $frameBackup
$frameProcess=$null
try{
 & (Join-Path $frameRoot 'Ring World KSP/build.ps1') -KspRoot $frameGame -Install -SmokeTest
 foreach($label in @('Clouds','Scattering','Parallax')){& (Join-Path $frameRoot "Ringworld $label/build.ps1") -KspRoot $frameGame -Install -SmokeTest;if($LASTEXITCODE -ne 0){throw "Build failed: $label"}}
 $frameArgs=@('-ringworld-smoketest','-ringworld-frame-performance','-ringworld-profile','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-popupwindow','-logFile',"FramePerformance-$Run.log")
 $frameProcess=Start-Process -FilePath (Join-Path $frameGame 'KSP_x64.exe') -WorkingDirectory $frameGame -WindowStyle Hidden -ArgumentList $frameArgs -PassThru
 Write-Output "Frame workload PID $($frameProcess.Id)"
 $frameDeadline=[DateTime]::UtcNow.AddMinutes(15)
 while(-not $frameProcess.HasExited){if([DateTime]::UtcNow -gt $frameDeadline){Stop-Process -Id $frameProcess.Id;throw 'Frame workload timed out.'};Start-Sleep -Seconds 2;$frameProcess.Refresh()}
 $frameLines=Get-Content -LiteralPath (Join-Path $frameGame "FramePerformance-$Run.log")
 $frameLines | Select-String '\[RingworldSmoke\] (FRAME_TIMING|CLOUD_|Cloud GPU|PASS|FAIL)' | ForEach-Object {$_.Line}
 if(($frameLines | Select-String '\[RingworldSmoke\] FAIL') -or -not($frameLines | Select-String '\[RingworldSmoke\] PASS frame performance workload complete')){throw 'Frame workload failed.'}
}finally{
 if($frameProcess -and -not $frameProcess.HasExited){Stop-Process -Id $frameProcess.Id}
 try{
 & (Join-Path $frameRoot 'Ring World KSP/build.ps1') -KspRoot $frameGame -Install
 foreach($label in @('Clouds','Scattering','Parallax')){& (Join-Path $frameRoot "Ringworld $label/build.ps1") -KspRoot $frameGame -Install}
 }finally{Copy-Item -LiteralPath (Join-Path $frameBackup 'settings.cfg') -Destination (Join-Path $frameGame 'settings.cfg') -Force}
 Write-Output 'Normal development binaries and original settings restored. No release archives rebuilt.'
}
