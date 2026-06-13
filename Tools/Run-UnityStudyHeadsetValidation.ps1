param(
    [string]$AdbPath = "D:\GithubVR\bigredbutton.institute\studies\first-big-red-button-study\native-quest-mr-study\artifacts\toolchain\android-platform-tools\platform-tools\adb.exe",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ApkPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "Builds\Android\TheBigRedButtonInstitute-UnityVersion.apk"),
    [string]$Language = "en-US",
    [int]$WaitSeconds = 90
)

$ErrorActionPreference = 'Stop'

$package = "org.thebigredbuttoninstitute.unityversion"
$activity = "com.unity3d.player.UnityPlayerGameActivity"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputRoot = Join-Path $ProjectPath "Builds\Android\Validation\unity-study-headset-$stamp"
$screenshotDevicePath = "/sdcard/Download/brb-unity-study-visual-$stamp.png"
$externalFiles = "/sdcard/Android/data/$package/files"

if (!(Test-Path $AdbPath)) {
    throw "adb not found: $AdbPath"
}

if (!(Test-Path $ApkPath)) {
    throw "APK not found: $ApkPath"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

& $AdbPath start-server | Out-Null
$devices = & $AdbPath devices -l
if (($devices | Select-String -Pattern "\bdevice\b").Count -lt 1) {
    throw "No Quest device is connected according to adb devices."
}

& $AdbPath install -r $ApkPath | Tee-Object -FilePath (Join-Path $outputRoot "install.txt")
& $AdbPath logcat -c

& $AdbPath shell am start `
    -n "$package/$activity" `
    --ez brb.unityStudyValidation true `
    --ez brb.unityVisualValidation true `
    --es brb.studyLanguage $Language |
    Tee-Object -FilePath (Join-Path $outputRoot "launch.txt")

Start-Sleep -Seconds ([Math]::Max(15, $WaitSeconds))

& $AdbPath shell screencap -p $screenshotDevicePath | Out-Null
& $AdbPath pull $screenshotDevicePath (Join-Path $outputRoot "brb-unity-study-visual.png") | Tee-Object -FilePath (Join-Path $outputRoot "screenshot-pull.txt")
& $AdbPath shell rm $screenshotDevicePath | Out-Null

$logPath = Join-Path $outputRoot "brb-unity-study-logcat.txt"
& $AdbPath logcat -d -v time | Select-String -Pattern "BRB_UNITY|BRB_NAME_APP_KEYBOARD_CONTRACT" | ForEach-Object { $_.Line } | Set-Content -Path $logPath

$exportsRoot = Join-Path $outputRoot "device-exports"
New-Item -ItemType Directory -Path $exportsRoot -Force | Out-Null
& $AdbPath pull "$externalFiles/BigRedButtonUnityStudyExports" (Join-Path $exportsRoot "BigRedButtonUnityStudyExports") | Tee-Object -FilePath (Join-Path $outputRoot "exports-pull.txt")
& $AdbPath pull "$externalFiles/ExperimentResults" (Join-Path $exportsRoot "ExperimentResults") | Tee-Object -FilePath (Join-Path $outputRoot "experiment-results-pull.txt")

$logText = if (Test-Path $logPath) { Get-Content $logPath -Raw } else { "" }
$screenshotPath = Join-Path $outputRoot "brb-unity-study-visual.png"
$summary = [ordered]@{
    schema = "brb.unity.headset.validation.v1"
    package = $package
    apkPath = $ApkPath
    outputRoot = $outputRoot
    language = $Language
    screenshotPath = $screenshotPath
    screenshotBytes = if (Test-Path $screenshotPath) { (Get-Item $screenshotPath).Length } else { 0 }
    hasStudyCompleteMarker = $logText.Contains("BRB_UNITY_STUDY_COMPLETE")
    hasVisualReadyMarker = $logText.Contains("BRB_UNITY_VISUAL_READY")
    hasKeyboardContractMarker = $logText.Contains("BRB_NAME_APP_KEYBOARD_CONTRACT")
    hasIpqHistoryPart1AudioMarker = $logText.Contains("audioId=aud_0320")
    hasIpqHistoryPart2AudioMarker = $logText.Contains("audioId=aud_0330")
    hasButtonPressMarker = $logText.Contains("BRB_UNITY_BUTTON_PRESS")
    exportJsonCount = @(Get-ChildItem -Path $exportsRoot -Recurse -Filter "*.json" -ErrorAction SilentlyContinue).Count
}

$summaryPath = Join-Path $outputRoot "unity-study-headset-validation-summary.json"
$summary | ConvertTo-Json -Depth 4 | Set-Content -Path $summaryPath

if ($summary.screenshotBytes -lt 1024 -or
    -not $summary.hasStudyCompleteMarker -or
    -not $summary.hasVisualReadyMarker -or
    -not $summary.hasKeyboardContractMarker -or
    -not $summary.hasIpqHistoryPart1AudioMarker -or
    -not $summary.hasIpqHistoryPart2AudioMarker -or
    $summary.exportJsonCount -lt 1) {
    throw "Unity study headset validation failed. Summary: $summaryPath"
}

Get-Content $summaryPath
