param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$OutputPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "Builds\Android\Validation\unity-study-static-port-summary.json")
)

$ErrorActionPreference = 'Stop'

$requiredFiles = @(
    "Assets\Scripts\Study\BrbUnityFirstStudyController.cs",
    "Assets\Editor\BrbUnityFirstStudySceneInstaller.cs",
    "Assets\Editor\BrbUnityStudyPreRenderValidator.cs",
    "Assets\StreamingAssets\BRBStudyAudio\localized\manifest.json",
    "Documentation\audio-script-lookup-table.csv",
    "Documentation\speech-asset-protocol.md",
    "Tools\Run-UnityStudyPreRenderValidation.ps1",
    "Tools\Build-UnityStudyApk.ps1",
    "Tools\Run-UnityStudyHeadsetValidation.ps1"
)

$expectedHashes = @{
    "Assets\StreamingAssets\BRBStudyAudio\localized\en_us\aud_0320_ipq_history_part1__en_us.mp3" = "AAD3BDB00F1928D7688790567197C934629300CF038337D6BC66CA364F01C137"
    "Assets\StreamingAssets\BRBStudyAudio\localized\ja_jp\aud_0320_ipq_history_part1__ja_jp.mp3" = "AAB46B0E6D8FF2E2CE00444E11F4963B309E152B007EBC185D9FCFB69869A0DC"
    "Assets\StreamingAssets\BRBStudyAudio\localized\en_us\aud_0330_ipq_history_part2__en_us.mp3" = "05F81ADFAD49EECEB9245DCBBF0656EE90C0B8ADE0D3856E9DBE40FB65087772"
    "Assets\StreamingAssets\BRBStudyAudio\localized\ja_jp\aud_0330_ipq_history_part2__ja_jp.mp3" = "6C05D79DC79F5FDAA2C776A0AFEBA00B1889D0027F2B4EB42A9EC82FAE255B63"
}

Push-Location $ProjectPath
try {
    $missingFiles = @($requiredFiles | Where-Object { !(Test-Path $_) })
    $assetsRoot = Join-Path $ProjectPath "Assets"
    $missingAssetMetas = @(
        Get-ChildItem -Path $assetsRoot -File -Recurse |
            Where-Object { -not $_.Name.EndsWith(".meta") -and !(Test-Path ($_.FullName + ".meta")) } |
            ForEach-Object { $_.FullName.Substring($ProjectPath.Length).TrimStart("\") }
    )
    $missingFolderMetas = @(
        Get-ChildItem -Path $assetsRoot -Directory -Recurse |
            Where-Object { !(Test-Path ($_.FullName + ".meta")) } |
            ForEach-Object { $_.FullName.Substring($ProjectPath.Length).TrimStart("\") }
    )
    $hashResults = foreach ($pair in $expectedHashes.GetEnumerator()) {
        $actual = if (Test-Path $pair.Key) { (Get-FileHash -Algorithm SHA256 $pair.Key).Hash } else { "" }
        [ordered]@{
            path = $pair.Key
            expected = $pair.Value
            actual = $actual
            matches = ($actual -eq $pair.Value)
        }
    }

    $controller = Get-Content "Assets\Scripts\Study\BrbUnityFirstStudyController.cs" -Raw
    $inputManager = Get-Content "Assets\Scripts\VR\QuestVrInputManager.cs" -Raw
    $manualPress = Get-Content "Assets\Scripts\BigRedButtonManualPressController.cs" -Raw
    $directLsl = Get-Content "Assets\Scripts\Diagnostics\BigRedButtonDirectLslDriveReceiver.cs" -Raw
    $directOsc = Get-Content "Assets\Scripts\Diagnostics\BigRedButtonDirectOscDriveReceiver.cs" -Raw
    $apkBuilder = Get-Content "Assets\Editor\QuestVrApkBuilder.cs" -Raw
    $manifest = Get-Content "Assets\Plugins\Android\AndroidManifest.xml" -Raw
    $sceneInstaller = Get-Content "Assets\Editor\QuestVrSceneInstaller.cs" -Raw

    $summary = [ordered]@{
        schema = "brb.unity.static_port_validation.v1"
        projectPath = $ProjectPath
        requiredFilesPresent = ($missingFiles.Count -eq 0)
        missingFiles = $missingFiles
        allAssetsHaveMetaFiles = ($missingAssetMetas.Count -eq 0 -and $missingFolderMetas.Count -eq 0)
        missingAssetMetas = $missingAssetMetas
        missingFolderMetas = $missingFolderMetas
        audioHashResults = $hashResults
        hasUnityVersionApkName = $apkBuilder.Contains("TheBigRedButtonInstitute-UnityVersion.apk")
        hasUnityVersionProductName = $apkBuilder.Contains("The Big Red Button Institute Unity Version")
        hasUnityPackageName = $manifest.Contains("org.thebigredbuttoninstitute.unityversion")
        hasStudyControllerInstallerHook = $sceneInstaller.Contains("BrbUnityFirstStudySceneInstaller.InstallIntoOpenScene")
        hasKeyboardContractMarker = $controller.Contains("BRB_NAME_APP_KEYBOARD_CONTRACT")
        hasIpqHistoryPart1Cue = $controller.Contains('"aud_0320"')
        hasIpqHistoryPart2Cue = $controller.Contains('"aud_0330"')
        hasFourSecondPriorDelay = $controller.Contains("PriorFeedbackToPreStartSeconds = 4f")
        hasFinalPhysicalPressText = $controller.Contains("controller-based physical press")
        hasPressSourceLabels = $inputManager.Contains("ButtonPressAccepted") `
            -and $manualPress.Contains("controller_contact") `
            -and $directLsl.Contains("direct_lsl") `
            -and $directOsc.Contains("direct_osc")
    }

    $summary.passed = $summary.requiredFilesPresent `
        -and $summary.allAssetsHaveMetaFiles `
        -and (@($hashResults | Where-Object { -not $_.matches }).Count -eq 0) `
        -and $summary.hasUnityVersionApkName `
        -and $summary.hasUnityVersionProductName `
        -and $summary.hasUnityPackageName `
        -and $summary.hasStudyControllerInstallerHook `
        -and $summary.hasKeyboardContractMarker `
        -and $summary.hasIpqHistoryPart1Cue `
        -and $summary.hasIpqHistoryPart2Cue `
        -and $summary.hasFourSecondPriorDelay `
        -and $summary.hasFinalPhysicalPressText `
        -and $summary.hasPressSourceLabels

    $outputDirectory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $summary | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputPath

    if (-not $summary.passed) {
        throw "Unity study static port validation failed: $OutputPath"
    }

    Get-Content $OutputPath
}
finally {
    Pop-Location
}
