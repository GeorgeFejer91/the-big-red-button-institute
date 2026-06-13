param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [Parameter(Mandatory = $true)]
    [string]$LogFile,

    [string]$ExecuteMethod,

    [int]$BackgroundWaitSeconds = 300
)

$ErrorActionPreference = 'Stop'

if (Test-Path $LogFile)
{
    Remove-Item $LogFile -Force
}

$logDirectory = Split-Path -Parent $LogFile
if (![string]::IsNullOrWhiteSpace($logDirectory))
{
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

$start = Get-Date
$arguments = @(
    '-batchmode'
    '-quit'
    '-projectPath'
    $ProjectPath
)

if (-not [string]::IsNullOrWhiteSpace($ExecuteMethod))
{
    $arguments += @('-executeMethod', $ExecuteMethod)
}

$arguments += @('-logFile', $LogFile)

$unityExitCode = 0
& $UnityPath @arguments
$unityExitCode = $LASTEXITCODE
if ($null -eq $unityExitCode)
{
    $unityExitCode = -1
}

$deadline = (Get-Date).AddSeconds([Math]::Max(5, $BackgroundWaitSeconds))
do
{
    Start-Sleep -Seconds 2
    $activeProcesses = @(
        Get-Process Unity, UnityPackageManager, bee_backend -ErrorAction SilentlyContinue |
            Where-Object { $_.StartTime -ge $start }
    )
}
while ($activeProcesses.Count -gt 0 -and (Get-Date) -lt $deadline)

if (!(Test-Path $LogFile))
{
    throw "Unity log was not created: $LogFile"
}

Get-Content $LogFile -Tail 200

if ($unityExitCode -ne 0)
{
    throw "Unity batchmode failed with exit code $unityExitCode. Log: $LogFile"
}
