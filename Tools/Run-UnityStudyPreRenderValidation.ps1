param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$LogFile = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "Builds\Android\unity-study-prerender.log")
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityPath)) {
    throw "Unity editor not found: $UnityPath"
}

& (Join-Path $PSScriptRoot "Invoke-UnityBatch.ps1") `
    -UnityPath $UnityPath `
    -ProjectPath $ProjectPath `
    -LogFile $LogFile `
    -ExecuteMethod "TheBigRedButtonInstitute.Editor.BrbUnityStudyPreRenderValidator.Run"
