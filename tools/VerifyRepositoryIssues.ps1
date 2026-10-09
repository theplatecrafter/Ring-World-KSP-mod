param([string[]]$Cases=@('full','residents'),[string]$Run='5')
$ErrorActionPreference='Stop'
$issueRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$issueGame=(Resolve-Path (Join-Path $issueRoot '../template_instance')).Path
$auditRoot=Join-Path $issueRoot 'artifacts/issue-audit-20261008'
if(Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue){throw 'Close the existing KSP instance before this regression run.'}
$settingsBackup=Join-Path $auditRoot 'settings-before-tests.cfg'
if(-not(Test-Path -LiteralPath $settingsBackup)){throw 'Original settings backup missing.'}
$ownedProcess=$null
$failedCases=@()
try{
    & (Join-Path $issueRoot 'build.ps1') -Install -SmokeTest
    if($LASTEXITCODE -ne 0){throw 'Regression build failed.'}
    foreach($case in $Cases){
        $arguments=@('-ringworld-smoketest','-ringworld-installed-issues-only','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-popupwindow','-logFile',"RepositoryIssues-$case-$Run.log")
        switch($case){
            'full'{$arguments+='-ringworld-repo-issue-audit'}
            'residents'{$arguments+='-ringworld-repo-residents-only'}
            'actions'{$arguments+=@('-ringworld-repo-residents-only','-ringworld-repo-local-actions-only')}
            'aquatic'{$arguments+='-ringworld-repo-aquatic-only'}
            default{throw "Unknown case: $case"}
        }
        $ownedProcess=Start-Process -FilePath (Join-Path $issueGame 'KSP_x64.exe') -WorkingDirectory $issueGame -WindowStyle Hidden -ArgumentList $arguments -PassThru
        $ownedProcess.Id | Set-Content -LiteralPath (Join-Path $auditRoot 'test-pid.txt')
        Write-Output "START $case PID $($ownedProcess.Id)"
        $deadline=[DateTime]::UtcNow.AddMinutes(20)
        while(-not $ownedProcess.HasExited){
            if([DateTime]::UtcNow -gt $deadline){Stop-Process -Id $ownedProcess.Id;throw "$case timed out"}
            Start-Sleep -Seconds 2;$ownedProcess.Refresh()
        }
        $log=Join-Path $issueGame "RepositoryIssues-$case-$Run.log"
        $lines=Get-Content -LiteralPath $log
        $lines | Select-String '\[RingworldSmoke\] (FAIL|PASS|AIRSHIP|STOCK AIR)' | ForEach-Object {$_.Line}
        $complete=if($case -in @('residents','actions')){'\[RingworldSmoke\] PASS natural EVA'}else{'\[RingworldSmoke\] PASS issue 4 actual craft'}
        if(($lines | Select-String '\[RingworldSmoke\] FAIL') -or -not($lines | Select-String $complete)){$failedCases+=$case}
        $ownedProcess=$null
    }
}finally{
    if($ownedProcess -and -not $ownedProcess.HasExited){Stop-Process -Id $ownedProcess.Id}
    & (Join-Path $issueRoot 'build.ps1') -Install
    Copy-Item -LiteralPath $settingsBackup -Destination (Join-Path $issueGame 'settings.cfg') -Force
    Write-Output 'Normal development build and original user settings restored.'
}
if($failedCases.Count){throw ('Unverified regression cases: '+($failedCases -join ', '))}
