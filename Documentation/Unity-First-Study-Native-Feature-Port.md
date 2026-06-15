# Unity First Study Native Feature Port

This fork carries the native Quest study structure into the public Unity Big Red Button app while keeping the integration brief's public caller/callee bridge constraints intact.

## App Identity

- APK output: `Builds/Android/TheBigRedButtonInstitute-UnityVersion.apk`
- Android package: `org.thebigredbuttoninstitute.unityversion`
- Product name: `The Big Red Button Institute Unity Version`
- Study controller: `Assets/Scripts/Study/BrbUnityFirstStudyController.cs`
- Scene installer hook: `Assets/Editor/BrbUnityFirstStudySceneInstaller.cs`
- Alternative native Quest study button model:
  `Assets/Models/NativeQuestStudyBigRedButton.glb`

## Ported Study Elements

- Bilingual voiceover library copied to `Assets/StreamingAssets/BRBStudyAudio/localized`.
- Original final condition mixes copied to `Assets/StreamingAssets/BRBStudyAudio/final`.
- Raw native transition cues and SFX copied to `Assets/StreamingAssets/BRBStudyAudio/raw` and `Assets/StreamingAssets/BRBStudyAudio/sfx`.
- Audio registry and maintenance docs copied to:
  - `Documentation/audio-script-lookup-table.csv`
  - `Documentation/speech-asset-protocol.md`
- Unity runtime logs stable cue markers such as `BRB_UNITY_AUDIO_CUE`, including `aud_0320` and `aud_0330` for IPQ history narration.
- Demographics name entry uses an app-owned pop-out spatial keyboard marker:
  `BRB_NAME_APP_KEYBOARD_CONTRACT keyboardPanel=keyboard_panel presentation=pop_out_spatial_panel radialReference=headset_center orientation=faces_headset`.
- Age entry mirrors the native 0-100 headset-side slider contract.
- Prior button experience retains the 4 second feedback-to-pre-start delay in participant mode.
- Condition 1 and 2 keep the 3D Big Red Button visible and active during the instruction tracks.
- Pictographic, redness, presence/IPQ, lost-opportunity, final confirmation, and final extra-press stages are represented in Unity exports.
- Final extra presses still observe the existing 3D Big Red Button press path. The Unity fork labels press sources so validation can distinguish controller contact from direct LSL, OSC, broker, HUD replay, or synthetic validation pulses.

## Existing Brief Features Preserved

- Quest questionnaire product bridge remains app-to-app with explicit intent, caller-owned `content://` result URI, write grant, and one-shot immutable callback `PendingIntent`.
- Direct Polar path remains scene-authored through `PolarH10RuntimeManager`, `PolarUnifiedModule`, `PolarPmdAdapter`, and `PolarHeartbeatButtonDriver`.
- Direct LSL path remains scene-authored through `BigRedButtonDirectLslDriveReceiver`.
- Direct OSC and Rusty XR broker diagnostics remain available for comparison.

## Validation Commands

Use the Unity editor version pinned by the project, `6000.3.16f1` or newer compatible Unity 6.

```powershell
Tools\Run-UnityStudyPreRenderValidation.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe"
Tools\Build-UnityStudyApk.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe"
Tools\Build-UnityStudyApk.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe" -ButtonModelProfile NativeQuestStudy
Tools\Run-UnityStudyHeadsetValidation.ps1
```

Validation output is written under ignored `Builds/Android/Validation/` folders.

The goal is not complete until:

- Unity import/compile succeeds.
- Pre-render validation produces nonblank PNGs and a passing summary.
- The Unity-version APK builds.
- The APK installs and launches on Quest.
- Headset validation captures a screenshot, pulls exports, and observes the required `BRB_UNITY_*` and keyboard contract markers.
