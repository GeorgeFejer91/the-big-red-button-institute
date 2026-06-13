# Speech Asset Generation And Integration Protocol

Use this protocol when adding participant-facing speech elements to the native Quest study.

## Core Rules

- Keep API keys out of chat and source. The ElevenLabs key may live only in `C:\Users\Georgeous\Desktop\elevenlabs_access_codex.txt`.
- Generate drafts under ignored `artifacts/` folders first. Copy only approved final MP3s, scripts, transcript stubs, and back-translations into `..\audio-assets\localized`.
- Use stable audio IDs before generation. Keep IDs ordered by runtime position, for example `aud_0320` and `aud_0330` for the IPQ history narration.
- Participant-facing questionnaire narration can use dry science-nerd sarcasm, but must not reveal factor names, subscale names, scoring machinery, internal variables, or hypotheses. Use literal `... ` markers for interrupted thoughts and strategic self-corrections.
- Add about 1000 ms of tail silence to ElevenLabs speech MP3s to avoid clipped endings on headset playback.
- Do not edit the two long condition instruction stimuli in `..\audio-assets\final`; those are source-of-truth study stimuli.

## Generation

1. Write one script file per audio ID and locale.
2. For ElevenLabs, use:
   - Voice name: `calm voice`
   - Voice ID: `IVxgxz5EgbHtWNcgBjOV`
   - Model: `eleven_v3`
   - Output format: `mp3_44100_128`
   - Japanese language code: `ja`
   - English language code: `en`
3. Put punctuation and pauses directly into the script. Do not rely on a later runtime delay to create performance timing.
4. Generate into `artifacts/<task-name>/`, then add the 1000 ms tail silence and record duration plus SHA-256.
5. Listen for clipped endings, missing pauses, wrong language, overlong duration, or psychometric spoilers before promoting the file.

## Library Promotion

For each approved speech element, copy files into the localized library:

- English MP3: `..\audio-assets\localized\en_us\<audio_id>_<clip_key>__en_us.mp3`
- Japanese MP3: `..\audio-assets\localized\ja_jp\<audio_id>_<clip_key>__ja_jp.mp3`
- Script files: `..\audio-assets\localized\transcripts\<locale>\<audio_id>_<clip_key>__<locale>.script.txt`
- Transcript JSON stubs: same folder with `.json`
- Japanese back-translation: `..\audio-assets\localized\transcripts\ja_jp\<audio_id>_<clip_key>__ja_jp.backtranslation.txt`

Then update `..\audio-assets\localized\manifest.json` with:

- `audioId`, `stage`, `role`, `participantFacing`, `translationPolicy`, and runtime cue.
- `tailSilenceMs` when tail padding was added.
- Per-locale `path`, `scriptPath`, `transcriptPath`, `ttsProvider`, `voiceId`, `modelId`, `languageCode`, `outputFormat`, `durationMs`, `observedDurationMs`, `status`, `sha256`, and `generatedAt`.
- Japanese `backTranslationPath`.

Run `tools\update-localized-audio-manifest.ps1` after file promotion so hashes and durations are recalculated from disk.

## Lookup Table

Add one row per audio ID per locale to `docs\audio-script-lookup-table.csv`.

Each row should include the runtime hook/log marker, package asset path, library path, SHA-256, duration, generation provider/model/voice, script path, translation policy, and a short maintenance note. Confirm the CSV imports with `Import-Csv` after editing; multiline scripts are allowed, fused rows are not.

## Runtime Wiring

For app-routed speech:

- Add explicit Kotlin constants for audio IDs and asset paths.
- Select English or Japanese by `selectedLanguageState`.
- Use APK asset playback for localized files and provide an English fallback for missing localized assets.
- Log a stable marker with `condition`, `cue`, `audioId`, `asset`, `language`, `trigger`, and whether the clip gates participant action.
- Keep long narration non-blocking unless the study design explicitly says the participant must wait.

## Validation

Before claiming the integration is ready:

1. Run `tools\update-localized-audio-manifest.ps1`.
2. Run `tools\validate-localized-audio.ps1 -RequireJapaneseAudio`.
3. Add or update `tools\validate-study.ps1` checks for new files, hashes, manifest rows, lookup-table rows, and runtime markers.
4. Run `tools\validate-study.ps1 -SkipBuild`.
5. Build the APK with `.\gradlew.bat :app:assembleDebug`.
6. Run the smallest relevant headset gate. For questionnaire speech that starts from a questionnaire transition, use the fast Quest keyevent/export gate or an audio-rig/headset smoke that observes the cue marker.

Local validation alone is not final completion for native Quest app changes.
