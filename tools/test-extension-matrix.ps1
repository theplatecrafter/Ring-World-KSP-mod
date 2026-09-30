param([int]$TimeoutSeconds=900)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskParent=Split-Path -Parent $taskRoot
$taskGame=Join-Path $taskParent 'template_instance'
if(Get-Process KSP_x64 -ErrorAction SilentlyContinue){throw 'Close KSP before the installation matrix.'}
& (Join-Path $taskRoot 'build.ps1') -Install
foreach($label in @('Clouds','Scattering')){& (Join-Path $taskParent "Ringworld $label/build.ps1") -Install}
$taskBackup=Join-Path $taskRoot ('artifacts/extension-matrix-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
try {
 foreach($case in @('Base','Clouds','Scattering','Combined')){
  foreach($label in @('Clouds','Scattering')){
   $installed=[IO.Path]::GetFullPath((Join-Path $taskGame "GameData/Ringworld$label"))
   $stored=[IO.Path]::GetFullPath((Join-Path $taskBackup "Ringworld$label"))
   if(-not $installed.StartsWith([IO.Path]::GetFullPath($taskGame)+[IO.Path]::DirectorySeparatorChar)){throw 'Invalid install path'}
   if(-not $stored.StartsWith([IO.Path]::GetFullPath($taskBackup)+[IO.Path]::DirectorySeparatorChar)){throw 'Invalid backup path'}
   $wanted=$case -eq $label -or $case -eq 'Combined'
   if($wanted -and (Test-Path -LiteralPath $stored)){Move-Item -LiteralPath $stored -Destination $installed}
   if(-not $wanted -and (Test-Path -LiteralPath $installed)){Move-Item -LiteralPath $installed -Destination $stored}
  }
  if($case -eq 'Combined'){
   & (Join-Path $taskRoot 'smoke-test.ps1') -VisualOptionsOnly -SkipIssue3 -TimeoutSeconds $TimeoutSeconds
   $report=Join-Path $taskRoot 'artifacts/validation/visual-options-smoke.txt'
  }else{
   & (Join-Path $taskRoot 'smoke-test.ps1') -GlobalCloudsOnly -TimeoutSeconds $TimeoutSeconds
   $report=Join-Path $taskRoot 'artifacts/validation/global-clouds-smoke.txt'
  }
  $text=Get-Content -LiteralPath $report -Raw
  $cloud=$case -eq 'Clouds' -or $case -eq 'Combined'
  $scatter=$case -eq 'Scattering' -or $case -eq 'Combined'
  if(-not $text.Contains("Optional extensions: Clouds=$cloud, Scattering=$scatter")){throw "Provider discovery did not match $case"}
  Copy-Item -LiteralPath $report -Destination (Join-Path $taskRoot "artifacts/validation/extensions-$case.txt") -Force
  Write-Host "PASS extension installation: $case"
 }
}finally{
 foreach($label in @('Clouds','Scattering')){
  $stored=Join-Path $taskBackup "Ringworld$label"
  $installed=Join-Path $taskGame "GameData/Ringworld$label"
  if(Test-Path -LiteralPath $stored){if(Test-Path -LiteralPath $installed){throw "Restore conflict: $installed"};Move-Item -LiteralPath $stored -Destination $installed}
 }
}
