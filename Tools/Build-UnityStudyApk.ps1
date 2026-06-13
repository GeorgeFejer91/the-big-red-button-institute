param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$LogFile = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "Builds\Android\unity-study-apk-build.log")
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityPath)) {
    throw "Unity editor not found: $UnityPath"
}

& (Join-Path $PSScriptRoot "Invoke-UnityBatch.ps1") `
    -UnityPath $UnityPath `
    -ProjectPath $ProjectPath `
    -LogFile $LogFile `
    -ExecuteMethod "TheBigRedButtonInstitute.Editor.QuestVrApkBuilder.InstallSceneAndBuildApk"

$apk = Join-Path $ProjectPath "Builds\Android\TheBigRedButtonInstitute-UnityVersion.apk"
if (!(Test-Path $apk)) {
    throw "Unity study APK was not produced: $apk"
}

Get-Item $apk | Select-Object FullName, Length, LastWriteTime
