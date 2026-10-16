param([string]$Run='high', [switch]$AllowWarnings)
$ErrorActionPreference='Stop'
$startupRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$startupGame=Join-Path $startupRoot 'template_instance'
if(Get-Process KSP_x64 -ErrorAction SilentlyContinue){throw 'Close KSP before this check.'}
$startupProcess=$null
try{
 & (Join-Path $startupRoot 'Ring World KSP/build.ps1') -KspRoot $startupGame -Install -SmokeTest
 if($LASTEXITCODE -ne 0){throw 'Startup diagnostic build failed.'}
 $startupProcess=Start-Process -FilePath (Join-Path $startupGame 'KSP_x64.exe') -WorkingDirectory $startupGame -WindowStyle Hidden -ArgumentList @('-ringworld-startup-check','-popupwindow','-logFile',"StartupWarnings-$Run.log") -PassThru
 $startupDeadline=[DateTime]::UtcNow.AddMinutes(12)
 while(-not $startupProcess.HasExited){if([DateTime]::UtcNow -gt $startupDeadline){Stop-Process -Id $startupProcess.Id;throw 'Startup check timed out.'};Start-Sleep -Seconds 2;$startupProcess.Refresh()}
 $startupLines=Get-Content -LiteralPath (Join-Path $startupGame "StartupWarnings-$Run.log")
 $startupLines | Select-String '\[RingworldStartup\]' | ForEach-Object {$_.Line}
 if(-not($startupLines | Select-String '\[RingworldStartup\] COMPLETE')){throw 'Startup check did not finish.'}
 if(-not $AllowWarnings){
  if(-not($startupLines | Select-String '\[RingworldStartup\] Terrain=High ') -or -not($startupLines | Select-String '\[RingworldStartup\] Kopernicus missing textures=0') -or ($startupLines | Select-String '\[RingworldStartup\] (POPUP|MISSING_TEXTURE)')){throw 'Startup warning remains; inspect the recorded text.'}
 }
}finally{
 if($startupProcess -and -not $startupProcess.HasExited){Stop-Process -Id $startupProcess.Id}
 & (Join-Path $startupRoot 'Ring World KSP/build.ps1') -KspRoot $startupGame -Install
 if($LASTEXITCODE -ne 0){throw 'Normal base restoration failed.'}
}
