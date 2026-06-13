using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using TheBigRedButtonInstitute.VR;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace TheBigRedButtonInstitute.Study
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6)]
    public sealed class BrbUnityFirstStudyController : MonoBehaviour
    {
        const string AudioRoot = "BRBStudyAudio";
        const string LocalizedAudioRoot = "localized";
        const string ExportDirectoryName = "BigRedButtonUnityStudyExports";
        const string NativeExportMirrorDirectoryName = "ExperimentResults";
        const string ContractVersion = "brb_unity_first_study_v1";
        const string UnityVersionMarker = "UNITY_VERSION";
        const string NameKeyboardContractMarker = "BRB_NAME_APP_KEYBOARD_CONTRACT";
        const float PriorFeedbackToPreStartSeconds = 4f;

        public enum StudyStage
        {
            Idle = 0,
            LanguageSelect = 1,
            DemographicsName = 2,
            DemographicsAge = 3,
            PriorExperience = 4,
            PreStartInstructions = 5,
            Condition1 = 6,
            Pictographic1 = 7,
            Presence1 = 8,
            LostOpportunity1 = 9,
            Condition2 = 10,
            Pictographic2 = 11,
            Presence2 = 12,
            FinalEndConfirmation = 13,
            FinalExtraPresses = 14,
            Complete = 15
        }

        readonly struct AudioSpec
        {
            public AudioSpec(string audioId, string enPath, string jaPath, int enDurationMs, int jaDurationMs, bool gatesAction)
            {
                AudioId = audioId;
                EnPath = enPath;
                JaPath = jaPath;
                EnDurationMs = enDurationMs;
                JaDurationMs = jaDurationMs;
                GatesAction = gatesAction;
            }

            public string AudioId { get; }
            public string EnPath { get; }
            public string JaPath { get; }
            public int EnDurationMs { get; }
            public int JaDurationMs { get; }
            public bool GatesAction { get; }

            public string PathForLocale(string locale)
            {
                return string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(JaPath)
                    ? JaPath
                    : EnPath;
            }

            public int DurationForLocale(string locale)
            {
                return string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase) && JaDurationMs > 0
                    ? JaDurationMs
                    : EnDurationMs;
            }
        }

        sealed class StageRecord
        {
            public string stage;
            public string cue;
            public string audioId;
            public string asset;
            public string language;
            public bool gated;
            public double realtime;
            public int pressCount;
        }

        sealed class PressRecord
        {
            public int count;
            public string source;
            public string stage;
            public double realtime;
            public bool controllerContact;
        }

        static readonly AudioSpec[] AudioCatalog =
        {
            new("aud_0100", "en_us/aud_0100_condition_1_instructions__en_us.mp3", "ja_jp/aud_0100_condition_1_instructions__ja_jp.mp3", 300774, 300774, true),
            new("aud_0110", "en_us/aud_0110_condition_2_instructions__en_us.mp3", "ja_jp/aud_0110_condition_2_instructions__ja_jp.mp3", 325590, 325590, true),
            new("aud_0200", "en_us/aud_0200_prior_experience_question__en_us.mp3", "ja_jp/aud_0200_prior_experience_question__ja_jp.mp3", 10527, 10449, false),
            new("aud_0210", "en_us/aud_0210_prior_experience_yes_feedback__en_us.mp3", "ja_jp/aud_0210_prior_experience_yes_feedback__ja_jp.mp3", 5251, 8281, false),
            new("aud_0220", "en_us/aud_0220_prior_experience_no_feedback__en_us.mp3", "ja_jp/aud_0220_prior_experience_no_feedback__ja_jp.mp3", 4284, 5146, false),
            new("aud_0230", "en_us/aud_0230_pre_start_instructions__en_us.mp3", "ja_jp/aud_0230_pre_start_instructions__ja_jp.mp3", 34273, 47804, true),
            new("aud_0300", "en_us/aud_0300_redness_vas_to_likert_changeover__en_us.mp3", "ja_jp/aud_0300_redness_vas_to_likert_changeover__ja_jp.mp3", 22988, 23327, false),
            new("aud_0310", "en_us/aud_0310_redness_likert_to_vas_changeover__en_us.mp3", "ja_jp/aud_0310_redness_likert_to_vas_changeover__ja_jp.mp3", 16771, 27559, false),
            new("aud_0320", "en_us/aud_0320_ipq_history_part1__en_us.mp3", "ja_jp/aud_0320_ipq_history_part1__ja_jp.mp3", 48718, 57835, false),
            new("aud_0330", "en_us/aud_0330_ipq_history_part2__en_us.mp3", "ja_jp/aud_0330_ipq_history_part2__ja_jp.mp3", 54491, 58723, false),
            new("aud_0500", "en_us/aud_0500_final_end_confirmation_question__en_us.mp3", "ja_jp/aud_0500_final_end_confirmation_question__ja_jp.mp3", 5146, 8359, false),
            new("aud_0510", "en_us/aud_0510_final_end_confirmation_10_feedback__en_us.mp3", "ja_jp/aud_0510_final_end_confirmation_10_feedback__ja_jp.mp3", 16274, 10371, false),
            new("aud_0600", "en_us/aud_0600_final_extra_presses_prompt__en_us.mp3", "ja_jp/aud_0600_final_extra_presses_prompt__ja_jp.mp3", 45636, 49084, false)
        };

        static readonly string[] KeyboardKeys =
        {
            "A", "B", "C", "D", "E", "F",
            "G", "H", "I", "J", "K", "L",
            "M", "N", "O", "P", "Q", "R",
            "S", "T", "U", "V", "W", "X",
            "Y", "Z", "SPACE", "BACK", "OK"
        };

        [Header("References")]
        [SerializeField] QuestVrInputManager inputManager;
        [SerializeField] Transform headTransform;
        [SerializeField] Transform buttonTransform;

        [Header("Study Runtime")]
        [SerializeField] bool runStudyOnStart = true;
        [SerializeField] bool allowValidationIntentExtras = true;
        [SerializeField] bool validationFastMode;
        [SerializeField] bool visualValidationMode;
        [SerializeField] bool rendererPretestMode;
        [SerializeField] string defaultLanguage = "en-US";
        [SerializeField] string participantIdPrefix = "unity";
        [SerializeField, Min(0.5f)] float fastConditionSeconds = 5f;
        [SerializeField, Min(0.5f)] float fastQuestionnaireSeconds = 1.25f;
        [SerializeField, Min(0.5f)] float fastAudioPreviewSeconds = 1.5f;
        [SerializeField] bool allowSyntheticValidationPresses = true;

        [Header("Spatial Panels")]
        [SerializeField, Min(0.4f)] float panelDistanceFromHead = 1.25f;
        [SerializeField] float panelVerticalOffset = -0.03f;
        [SerializeField] float keyboardHorizontalOffset = -0.58f;
        [SerializeField] Vector2 mainPanelSize = new(900f, 560f);
        [SerializeField] Vector2 keyboardPanelSize = new(620f, 390f);
        [SerializeField, Min(0.0005f)] float panelWorldScale = 0.00115f;

        Canvas _mainCanvas;
        Canvas _keyboardCanvas;
        TextMeshProUGUI _mainText;
        TextMeshProUGUI _keyboardText;
        Image _mainBackground;
        Image _keyboardBackground;
        AudioSource _audioSource;
        Coroutine _studyRoutine;
        StudyStage _stage = StudyStage.Idle;
        string _language;
        string _participantId;
        string _participantName = string.Empty;
        int _participantAge = 25;
        string _priorExperience = string.Empty;
        int _stageSelectedIndex;
        int _keyboardSelectedIndex;
        float _stageStartedAt;
        float _nextSyntheticAdvanceAt;
        bool _keyboardContractLogged;
        bool _nextPressed;
        bool _previousPressed;
        bool _selectPressed;
        bool _verticalStickArmed = true;
        bool _horizontalStickArmed = true;
        int _condition1StartPressCount;
        int _condition1EndPressCount;
        int _condition2StartPressCount;
        int _condition2EndPressCount;
        int _finalStartPressCount;
        int _finalEndPressCount;
        bool _finalControllerContactObserved;
        readonly List<StageRecord> _stageRecords = new();
        readonly List<PressRecord> _pressRecords = new();
        readonly Dictionary<string, int> _presenceAnswers = new();
        float _presenceSlider = 50f;
        float _rednessValue = 50f;
        int _rednessLikert = 4;
        int _finalExpectedPresses = 10;
        string _exportJsonPath = string.Empty;
        string _exportCsvPath = string.Empty;

        public StudyStage CurrentStage => _stage;
        public string LatestExportJsonPath => _exportJsonPath;
        public string LatestExportCsvPath => _exportCsvPath;
        public bool IsValidationFastMode => validationFastMode;

        public void ConfigureReferences(QuestVrInputManager manager, Transform head, Transform button)
        {
            inputManager = manager;
            headTransform = head;
            buttonTransform = button;
        }

        void Reset()
        {
            ResolveReferences(forceRefresh: true);
        }

        void Awake()
        {
            ResolveReferences(forceRefresh: true);
            EnsureAudioSource();
            EnsurePanels();
            ReadValidationIntentExtras();
        }

        void OnEnable()
        {
            ResolveReferences(forceRefresh: false);
            SubscribeToButtonPresses();
        }

        void Start()
        {
            _language = NormalizeLanguage(defaultLanguage);
            _participantId = BuildParticipantId(participantIdPrefix);
            LogStructuralMarkers();
            if (runStudyOnStart && _studyRoutine == null)
            {
                _studyRoutine = StartCoroutine(RunStudySequence());
            }
        }

        void Update()
        {
            ResolveReferences(forceRefresh: false);
            EnsurePanels();
            UpdatePanelPose();
            PollInput();
        }

        void OnDisable()
        {
            UnsubscribeFromButtonPresses();
        }

        public void RestartStudy(bool fastValidation)
        {
            validationFastMode = fastValidation;
            if (_studyRoutine != null)
            {
                StopCoroutine(_studyRoutine);
            }

            _stageRecords.Clear();
            _pressRecords.Clear();
            _presenceAnswers.Clear();
            _participantName = string.Empty;
            _priorExperience = string.Empty;
            _finalControllerContactObserved = false;
            _participantId = BuildParticipantId(participantIdPrefix);
            _studyRoutine = StartCoroutine(RunStudySequence());
        }

        IEnumerator RunStudySequence()
        {
            yield return RunLanguageSelect();
            yield return RunNameEntry();
            yield return RunAgeEntry();
            yield return RunPriorExperience();
            yield return RunPreStartInstructions();
            yield return RunCondition(1);
            yield return RunPictographicQuestionnaire(1);
            yield return RunPresenceQuestionnaire(1);
            yield return RunLostOpportunityInterstitial();
            yield return RunCondition(2);
            yield return RunPictographicQuestionnaire(2);
            yield return RunPresenceQuestionnaire(2);
            yield return RunFinalEndConfirmation();
            yield return RunFinalExtraPresses();
            yield return RunCompletion();
        }

        IEnumerator RunLanguageSelect()
        {
            EnterStage(StudyStage.LanguageSelect, "language_select");
            _stageSelectedIndex = string.Equals(_language, "ja-JP", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            while (!ShouldAutoAdvance())
            {
                UpdateSelection(2);
                _language = _stageSelectedIndex == 1 ? "ja-JP" : "en-US";
                SetMainPanel(
                    "Big Red Button Institute",
                    "Unity study version\n\nChoose study language.",
                    new[] { "English", "Japanese" },
                    _stageSelectedIndex,
                    "Use right thumbstick or arrow keys. Press trigger, A, Enter, or Space to continue.");
                if (ConsumeSelect())
                {
                    break;
                }

                yield return null;
            }

            if (validationFastMode)
            {
                _language = NormalizeLanguage(ReadStringExtra("brb.studyLanguage", _language));
            }

            RecordStage("language_selected", null, null, false);
        }

        IEnumerator RunNameEntry()
        {
            EnterStage(StudyStage.DemographicsName, "demographics_name");
            _keyboardSelectedIndex = 0;
            _participantName = validationFastMode ? "UNITY QA" : _participantName;
            ShowKeyboard(true);
            LogNameKeyboardContract();

            while (!ShouldAutoAdvance())
            {
                if (ConsumeNext())
                {
                    _keyboardSelectedIndex = (_keyboardSelectedIndex + 1) % KeyboardKeys.Length;
                    PlayNavigationBlip();
                }

                if (ConsumePrevious())
                {
                    _keyboardSelectedIndex = (_keyboardSelectedIndex + KeyboardKeys.Length - 1) % KeyboardKeys.Length;
                    PlayNavigationBlip();
                }

                if (ConsumeSelect())
                {
                    var key = KeyboardKeys[_keyboardSelectedIndex];
                    if (key == "OK")
                    {
                        break;
                    }

                    if (key == "BACK")
                    {
                        if (_participantName.Length > 0)
                        {
                            _participantName = _participantName.Substring(0, _participantName.Length - 1);
                        }
                    }
                    else if (key == "SPACE")
                    {
                        _participantName += " ";
                    }
                    else if (_participantName.Length < 32)
                    {
                        _participantName += key;
                    }

                    PlayChoiceBlip();
                }

                SetMainPanel(
                    "Participant Setup",
                    "Name entry\n\nThis uses the app-owned pop-out spatial keyboard contract from the native Quest study.",
                    Array.Empty<string>(),
                    -1,
                    $"Name: {(_participantName.Length == 0 ? "<empty>" : _participantName)}");
                SetKeyboardPanel();
                yield return null;
            }

            if (string.IsNullOrWhiteSpace(_participantName))
            {
                _participantName = "UNITY QA";
            }

            ShowKeyboard(false);
            RecordStage("demographics_name_saved", null, null, false);
        }

        IEnumerator RunAgeEntry()
        {
            EnterStage(StudyStage.DemographicsAge, "demographics_age");
            _participantAge = validationFastMode ? 42 : Mathf.Clamp(_participantAge, 0, 100);
            while (!ShouldAutoAdvance())
            {
                if (ConsumeNext())
                {
                    _participantAge = Mathf.Clamp(_participantAge + 1, 0, 100);
                    PlayNavigationBlip();
                }

                if (ConsumePrevious())
                {
                    _participantAge = Mathf.Clamp(_participantAge - 1, 0, 100);
                    PlayNavigationBlip();
                }

                SetMainPanel(
                    "Participant Setup",
                    "Age\n\nThe Unity fork mirrors the native slider contract: headset-side value, range 0-100, no Android IME owner.",
                    Array.Empty<string>(),
                    -1,
                    $"Age slider: {_participantAge:0} / 100");
                if (ConsumeSelect())
                {
                    break;
                }

                yield return null;
            }

            RecordStage("demographics_age_saved", null, null, false);
        }

        IEnumerator RunPriorExperience()
        {
            EnterStage(StudyStage.PriorExperience, "prior_experience");
            yield return PlayAudioCue("aud_0200", "prior_experience_prompt", false);
            _stageSelectedIndex = 0;
            while (!ShouldAutoAdvance())
            {
                UpdateSelection(2);
                SetMainPanel(
                    "One More Question",
                    "Do you have any experience with pressing big red buttons?",
                    new[] { "Yes", "No" },
                    _stageSelectedIndex,
                    "Choose one answer.");
                if (ConsumeSelect())
                {
                    break;
                }

                yield return null;
            }

            _priorExperience = _stageSelectedIndex == 0 ? "yes" : "no";
            var feedbackAudio = _priorExperience == "yes" ? "aud_0210" : "aud_0220";
            yield return PlayAudioCue(feedbackAudio, $"prior_experience_{_priorExperience}_feedback", false);
            RecordStage($"prior_experience_answer_{_priorExperience}", null, null, false);
            yield return WaitSeconds(validationFastMode ? fastQuestionnaireSeconds : PriorFeedbackToPreStartSeconds);
        }

        IEnumerator RunPreStartInstructions()
        {
            EnterStage(StudyStage.PreStartInstructions, "pre_start_instructions");
            SetMainPanel(
                "Before We Start",
                "The next voiceover prepares the first button session. The Big Red Button remains participant controlled.",
                Array.Empty<string>(),
                -1,
                "Please listen.");
            yield return PlayAudioCue("aud_0230", "pre_start_instructions", true);
        }

        IEnumerator RunCondition(int condition)
        {
            var stage = condition == 1 ? StudyStage.Condition1 : StudyStage.Condition2;
            EnterStage(stage, $"condition_{condition}");
            var audioId = condition == 1 ? "aud_0100" : "aud_0110";
            if (condition == 1)
            {
                _condition1StartPressCount = inputManager != null ? inputManager.ButtonPressCount : 0;
            }
            else
            {
                _condition2StartPressCount = inputManager != null ? inputManager.ButtonPressCount : 0;
            }

            SetMainPanel(
                $"Button Session {condition}",
                "The 3D Big Red Button is active. Press it with the controller contact surface only if you want to.",
                Array.Empty<string>(),
                -1,
                "No gaze, touch-screen, keyboard, or flat 2D substitute is used for the button.");
            yield return PlayAudioCue(audioId, $"condition_{condition}_instruction_mix", true);
            if (validationFastMode)
            {
                yield return WaitSeconds(fastConditionSeconds);
            }

            if (condition == 1)
            {
                _condition1EndPressCount = inputManager != null ? inputManager.ButtonPressCount : _condition1StartPressCount;
            }
            else
            {
                _condition2EndPressCount = inputManager != null ? inputManager.ButtonPressCount : _condition2StartPressCount;
            }

            RecordStage($"condition_{condition}_complete", null, null, false);
        }

        IEnumerator RunPictographicQuestionnaire(int condition)
        {
            var stage = condition == 1 ? StudyStage.Pictographic1 : StudyStage.Pictographic2;
            EnterStage(stage, $"pictographic_{condition}");
            var changeAudio = condition == 1 ? "aud_0300" : "aud_0310";
            yield return PlayAudioCue(changeAudio, $"redness_changeover_{condition}", false);

            while (!ShouldAutoAdvance())
            {
                if (ConsumeNext())
                {
                    _presenceSlider = Mathf.Clamp(_presenceSlider + 5f, 0f, 100f);
                    _rednessValue = Mathf.Clamp(_rednessValue + 5f, 0f, 100f);
                    _rednessLikert = Mathf.Clamp(_rednessLikert + 1, 1, 7);
                    PlayNavigationBlip();
                }

                if (ConsumePrevious())
                {
                    _presenceSlider = Mathf.Clamp(_presenceSlider - 5f, 0f, 100f);
                    _rednessValue = Mathf.Clamp(_rednessValue - 5f, 0f, 100f);
                    _rednessLikert = Mathf.Clamp(_rednessLikert - 1, 1, 7);
                    PlayNavigationBlip();
                }

                SetMainPanel(
                    $"After Button Session {condition}",
                    "Pictographic presence and redness checks\n\nPresence slider: " +
                    $"{_presenceSlider:0}/100\nRedness VAS: {_rednessValue:0}/100\nRedness Likert: {_rednessLikert}/7",
                    Array.Empty<string>(),
                    -1,
                    "Press trigger/A/Enter to save.");
                if (ConsumeSelect())
                {
                    break;
                }

                yield return null;
            }

            RecordStage($"pictographic_{condition}_saved", null, null, false);
        }

        IEnumerator RunPresenceQuestionnaire(int condition)
        {
            var stage = condition == 1 ? StudyStage.Presence1 : StudyStage.Presence2;
            EnterStage(stage, $"presence_{condition}");
            yield return PlayAudioCue(condition == 1 ? "aud_0320" : "aud_0330", $"ipq_history_{condition}", false);
            var itemIds = new[] { "ipq_sp1", "ipq_sp2", "ipq_sp3", "ipq_inv1", "ipq_real1" };
            var itemIndex = 0;
            var value = 3;

            while (itemIndex < itemIds.Length)
            {
                if (validationFastMode && Time.unscaledTime >= _nextSyntheticAdvanceAt)
                {
                    _presenceAnswers[$"{itemIds[itemIndex]}_condition_{condition}"] = value;
                    itemIndex++;
                    ArmSyntheticAdvance();
                    continue;
                }

                if (ConsumeNext())
                {
                    value = Mathf.Clamp(value + 1, 0, 6);
                    PlayNavigationBlip();
                }

                if (ConsumePrevious())
                {
                    value = Mathf.Clamp(value - 1, 0, 6);
                    PlayNavigationBlip();
                }

                SetMainPanel(
                    $"Presence Questionnaire {condition}",
                    $"{LocalizedPresenceItem(itemIds[itemIndex])}\n\nSelected: {value} / 6",
                    Array.Empty<string>(),
                    -1,
                    $"{itemIndex + 1}/{itemIds.Length}. Press trigger/A/Enter to save this item.");
                if (ConsumeSelect())
                {
                    _presenceAnswers[$"{itemIds[itemIndex]}_condition_{condition}"] = value;
                    itemIndex++;
                    PlayChoiceBlip();
                }

                yield return null;
            }

            RecordStage($"presence_{condition}_saved", null, null, false);
        }

        IEnumerator RunLostOpportunityInterstitial()
        {
            EnterStage(StudyStage.LostOpportunity1, "lost_opportunity");
            SetMainPanel(
                "Short Transition",
                "The Unity fork preserves the structural lost-opportunity break between the first and second button sessions.",
                Array.Empty<string>(),
                -1,
                "Preparing the second session.");
            RecordStage("lost_opportunity_presented", null, null, false);
            yield return WaitSeconds(validationFastMode ? fastQuestionnaireSeconds : 2.5f);
        }

        IEnumerator RunFinalEndConfirmation()
        {
            EnterStage(StudyStage.FinalEndConfirmation, "final_end_confirmation");
            yield return PlayAudioCue("aud_0500", "final_end_confirmation_prompt", false);
            _stageSelectedIndex = 0;
            while (!ShouldAutoAdvance())
            {
                UpdateSelection(2);
                SetMainPanel(
                    "Final Question",
                    $"Do you think your final number of button presses will be exactly {_finalExpectedPresses}?",
                    new[] { "Yes", "No" },
                    _stageSelectedIndex,
                    "This panel does not replace the final physical button interaction.");
                if (ConsumeSelect())
                {
                    break;
                }

                yield return null;
            }

            if (_stageSelectedIndex == 0)
            {
                yield return PlayAudioCue("aud_0510", "final_end_confirmation_10_feedback", false);
            }

            RecordStage("final_end_confirmation_saved", null, null, false);
        }

        IEnumerator RunFinalExtraPresses()
        {
            EnterStage(StudyStage.FinalExtraPresses, "final_extra_presses");
            _finalStartPressCount = inputManager != null ? inputManager.ButtonPressCount : 0;
            yield return PlayAudioCue("aud_0600", "final_extra_presses_prompt", false);
            ArmSyntheticAdvance();

            while (true)
            {
                var currentPressCount = inputManager != null ? inputManager.ButtonPressCount : _finalStartPressCount;
                var finalExtraCount = Mathf.Max(0, currentPressCount - _finalStartPressCount);
                SetMainPanel(
                    "Final Button Interaction",
                    "The final action remains the controller-based physical press of the 3D Big Red Button.\n\n" +
                    $"Extra presses in this stage: {finalExtraCount}",
                    Array.Empty<string>(),
                    -1,
                    _finalControllerContactObserved
                        ? "Controller-contact press observed. Press trigger/A/Enter to finish."
                        : "Press the physical 3D button with the controller contact surface.");

                if (_finalControllerContactObserved && ConsumeSelect())
                {
                    break;
                }

                if (validationFastMode && allowSyntheticValidationPresses && Time.unscaledTime >= _nextSyntheticAdvanceAt)
                {
                    Debug.Log("[BRB_UNITY_STUDY_VALIDATION] syntheticFinalPress=1 reason=fast_validation source=runtime");
                    inputManager?.TriggerButtonPressFromRuntime("validation_synthetic");
                    _finalControllerContactObserved = true;
                    ArmSyntheticAdvance();
                }

                if (validationFastMode && _finalControllerContactObserved && Time.unscaledTime >= _nextSyntheticAdvanceAt)
                {
                    break;
                }

                yield return null;
            }

            _finalEndPressCount = inputManager != null ? inputManager.ButtonPressCount : _finalStartPressCount;
            RecordStage("final_extra_presses_complete", null, null, false);
        }

        IEnumerator RunCompletion()
        {
            EnterStage(StudyStage.Complete, "complete");
            WriteExports();
            SetMainPanel(
                "Unity Study Export Complete",
                "The Unity version finished the native-equivalent study flow and wrote local export artifacts.",
                Array.Empty<string>(),
                -1,
                $"JSON: {_exportJsonPath}\nCSV: {_exportCsvPath}");
            Debug.Log($"[BRB_UNITY_STUDY_COMPLETE] exportJson={_exportJsonPath} exportCsv={_exportCsvPath}");
            if (visualValidationMode)
            {
                Debug.Log($"[BRB_UNITY_VISUAL_READY] stage=complete exportJson={_exportJsonPath}");
            }

            yield return null;
        }

        void EnterStage(StudyStage stage, string cue)
        {
            _stage = stage;
            _stageStartedAt = Time.unscaledTime;
            _stageSelectedIndex = 0;
            ArmSyntheticAdvance();
            RecordStage(cue, null, null, false);
            Debug.Log($"[BRB_UNITY_STUDY_STAGE] stage={stage} cue={cue} language={_language} validationFast={validationFastMode}");
        }

        IEnumerator PlayAudioCue(string audioId, string cue, bool gatesAction)
        {
            if (!TryGetAudioSpec(audioId, out var spec))
            {
                Debug.LogWarning($"[BRB_UNITY_AUDIO_MISSING] audioId={audioId} cue={cue}");
                RecordStage(cue, audioId, null, gatesAction);
                yield break;
            }

            var relativePath = spec.PathForLocale(_language);
            var durationMs = spec.DurationForLocale(_language);
            RecordStage(cue, audioId, relativePath, gatesAction || spec.GatesAction);
            Debug.Log(
                $"[BRB_UNITY_AUDIO_CUE] condition={ConditionLabelForStage(_stage)} cue={cue} audioId={audioId} " +
                $"asset={relativePath} language={_language} trigger=unity_study_controller gated={(gatesAction || spec.GatesAction ? 1 : 0)} " +
                $"durationMs={durationMs}");

            if (_audioSource == null)
            {
                yield break;
            }

            var url = StreamingAssetUrl(AudioRoot, LocalizedAudioRoot, relativePath);
            using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioTypeForPath(relativePath));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[BRB_UNITY_AUDIO_LOAD_FAILED] audioId={audioId} url={url} error={request.error}");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Debug.LogWarning($"[BRB_UNITY_AUDIO_LOAD_FAILED] audioId={audioId} url={url} error=null_clip");
                yield break;
            }

            _audioSource.Stop();
            _audioSource.clip = clip;
            _audioSource.Play();

            var waitSeconds = validationFastMode ? Mathf.Min(fastAudioPreviewSeconds, Mathf.Max(0.25f, clip.length)) : clip.length;
            yield return WaitSeconds(waitSeconds);

            if (validationFastMode && _audioSource != null && _audioSource.clip == clip)
            {
                _audioSource.Stop();
            }
        }

        IEnumerator WaitSeconds(float seconds)
        {
            var endTime = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (Time.unscaledTime < endTime)
            {
                yield return null;
            }
        }

        void RecordStage(string cue, string audioId, string asset, bool gated)
        {
            _stageRecords.Add(new StageRecord
            {
                stage = _stage.ToString(),
                cue = cue,
                audioId = audioId ?? string.Empty,
                asset = asset ?? string.Empty,
                language = _language ?? string.Empty,
                gated = gated,
                realtime = Time.realtimeSinceStartupAsDouble,
                pressCount = inputManager != null ? inputManager.ButtonPressCount : 0
            });
        }

        void HandleButtonPress(QuestVrInputManager.ButtonPressEvent evt)
        {
            var stageName = _stage.ToString();
            _pressRecords.Add(new PressRecord
            {
                count = evt.Count,
                source = evt.Source,
                stage = stageName,
                realtime = evt.RealtimeSinceStartup,
                controllerContact = evt.IsControllerContact
            });

            if (_stage == StudyStage.FinalExtraPresses && evt.IsControllerContact)
            {
                _finalControllerContactObserved = true;
            }

            Debug.Log(
                $"[BRB_UNITY_BUTTON_PRESS] count={evt.Count} source={evt.Source} " +
                $"controllerContact={(evt.IsControllerContact ? 1 : 0)} stage={stageName}");
        }

        void WriteExports()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var exportRoot = Path.Combine(Application.persistentDataPath, ExportDirectoryName, timestamp);
            Directory.CreateDirectory(exportRoot);
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, NativeExportMirrorDirectoryName));
            _exportJsonPath = Path.Combine(exportRoot, $"brb-unity-study-{_participantId}.json");
            _exportCsvPath = Path.Combine(exportRoot, $"brb-unity-study-{_participantId}.csv");

            var json = BuildExportJson();
            File.WriteAllText(_exportJsonPath, json, Encoding.UTF8);
            File.WriteAllText(_exportCsvPath, BuildExportCsv(), Encoding.UTF8);

            var mirrorPath = Path.Combine(
                Application.persistentDataPath,
                NativeExportMirrorDirectoryName,
                $"brb-unity-study-{_participantId}-{timestamp}.json");
            File.WriteAllText(mirrorPath, json, Encoding.UTF8);
        }

        string BuildExportJson()
        {
            var builder = new StringBuilder(8192);
            builder.AppendLine("{");
            AppendJson(builder, "contractVersion", ContractVersion, comma: true, indent: 1);
            AppendJson(builder, "appVariant", UnityVersionMarker, comma: true, indent: 1);
            AppendJson(builder, "unityVersion", Application.unityVersion, comma: true, indent: 1);
            AppendJson(builder, "productName", Application.productName, comma: true, indent: 1);
            AppendJson(builder, "participantId", _participantId, comma: true, indent: 1);
            AppendJson(builder, "participantName", _participantName, comma: true, indent: 1);
            AppendJson(builder, "language", _language, comma: true, indent: 1);
            AppendJson(builder, "priorButtonExperience", _priorExperience, comma: true, indent: 1);
            AppendJson(builder, "age", _participantAge, comma: true, indent: 1);
            AppendJson(builder, "validationFastMode", validationFastMode, comma: true, indent: 1);
            AppendJson(builder, "visualValidationMode", visualValidationMode, comma: true, indent: 1);
            AppendJson(builder, "rendererPretestMode", rendererPretestMode, comma: true, indent: 1);
            AppendJson(builder, "condition1Presses", Mathf.Max(0, _condition1EndPressCount - _condition1StartPressCount), comma: true, indent: 1);
            AppendJson(builder, "condition2Presses", Mathf.Max(0, _condition2EndPressCount - _condition2StartPressCount), comma: true, indent: 1);
            AppendJson(builder, "finalExtraPresses", Mathf.Max(0, _finalEndPressCount - _finalStartPressCount), comma: true, indent: 1);
            AppendJson(builder, "finalControllerContactObserved", _finalControllerContactObserved, comma: true, indent: 1);

            builder.AppendLine("  \"stageRecords\": [");
            for (var i = 0; i < _stageRecords.Count; i++)
            {
                var record = _stageRecords[i];
                builder.Append("    {");
                builder.Append($"\"stage\":\"{EscapeJson(record.stage)}\",");
                builder.Append($"\"cue\":\"{EscapeJson(record.cue)}\",");
                builder.Append($"\"audioId\":\"{EscapeJson(record.audioId)}\",");
                builder.Append($"\"asset\":\"{EscapeJson(record.asset)}\",");
                builder.Append($"\"language\":\"{EscapeJson(record.language)}\",");
                builder.Append($"\"gated\":{JsonBool(record.gated)},");
                builder.Append($"\"realtime\":{record.realtime.ToString("0.###", CultureInfo.InvariantCulture)},");
                builder.Append($"\"pressCount\":{record.pressCount}");
                builder.Append(i == _stageRecords.Count - 1 ? "}" : "},");
                builder.AppendLine();
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"buttonPresses\": [");
            for (var i = 0; i < _pressRecords.Count; i++)
            {
                var press = _pressRecords[i];
                builder.Append("    {");
                builder.Append($"\"count\":{press.count},");
                builder.Append($"\"source\":\"{EscapeJson(press.source)}\",");
                builder.Append($"\"stage\":\"{EscapeJson(press.stage)}\",");
                builder.Append($"\"realtime\":{press.realtime.ToString("0.###", CultureInfo.InvariantCulture)},");
                builder.Append($"\"controllerContact\":{JsonBool(press.controllerContact)}");
                builder.Append(i == _pressRecords.Count - 1 ? "}" : "},");
                builder.AppendLine();
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"presenceAnswers\": {");
            var presenceIndex = 0;
            foreach (var pair in _presenceAnswers)
            {
                builder.Append("    ");
                builder.Append($"\"{EscapeJson(pair.Key)}\": {pair.Value}");
                builder.Append(presenceIndex == _presenceAnswers.Count - 1 ? string.Empty : ",");
                builder.AppendLine();
                presenceIndex++;
            }

            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        string BuildExportCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("event_type,stage,cue,audio_id,asset,language,press_count,source,controller_contact,realtime");
            foreach (var record in _stageRecords)
            {
                builder.AppendLine(
                    $"stage,{Csv(record.stage)},{Csv(record.cue)},{Csv(record.audioId)},{Csv(record.asset)},{Csv(record.language)},{record.pressCount},,,{record.realtime.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            foreach (var press in _pressRecords)
            {
                builder.AppendLine(
                    $"button_press,{Csv(press.stage)},,,,,{press.count},{Csv(press.source)},{(press.controllerContact ? 1 : 0)},{press.realtime.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            return builder.ToString();
        }

        void SetMainPanel(string title, string body, IReadOnlyList<string> options, int selectedIndex, string footer)
        {
            if (_mainText == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"<size=128%><b>{EscapeRichText(title)}</b></size>");
            builder.AppendLine($"<size=72%><color=#9ED8FF>{EscapeRichText(ContractVersion)} | {EscapeRichText(UnityVersionMarker)}</color></size>");
            builder.AppendLine();
            builder.AppendLine(EscapeRichText(body));
            if (options != null && options.Count > 0)
            {
                builder.AppendLine();
                for (var i = 0; i < options.Count; i++)
                {
                    var prefix = i == selectedIndex ? "> " : "  ";
                    var color = i == selectedIndex ? "#FFFFFF" : "#9EB0BA";
                    builder.AppendLine($"<color={color}>{prefix}{EscapeRichText(options[i])}</color>");
                }
            }

            if (!string.IsNullOrWhiteSpace(footer))
            {
                builder.AppendLine();
                builder.AppendLine($"<size=72%><color=#BFD1DA>{EscapeRichText(footer)}</color></size>");
            }

            _mainText.text = builder.ToString();
        }

        void SetKeyboardPanel()
        {
            if (_keyboardText == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("<b>keyboard_panel</b>");
            builder.AppendLine("<size=72%>presentation=pop_out_spatial_panel | radialReference=headset_center | orientation=faces_headset</size>");
            builder.AppendLine();
            for (var i = 0; i < KeyboardKeys.Length; i++)
            {
                var key = KeyboardKeys[i];
                var selected = i == _keyboardSelectedIndex;
                builder.Append(selected ? "<color=#FFFFFF><b>[" : "<color=#9EB0BA> ");
                builder.Append(EscapeRichText(key));
                builder.Append(selected ? "]</b></color> " : " </color> ");
                if ((i + 1) % 6 == 0)
                {
                    builder.AppendLine();
                }
            }

            _keyboardText.text = builder.ToString();
        }

        void EnsurePanels()
        {
            if (_mainCanvas == null)
            {
                _mainCanvas = CreatePanelCanvas("BRB Unity Study Panel", mainPanelSize, out _mainBackground, out _mainText);
            }

            if (_keyboardCanvas == null)
            {
                _keyboardCanvas = CreatePanelCanvas("keyboard_panel", keyboardPanelSize, out _keyboardBackground, out _keyboardText);
                ShowKeyboard(false);
            }
        }

        Canvas CreatePanelCanvas(string objectName, Vector2 size, out Image background, out TextMeshProUGUI text)
        {
            var canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * panelWorldScale;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;
            canvas.pixelPerfect = false;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 1f;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(rect, false);
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            background = panelObject.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(0.015f, 0.023f, 0.03f, 0.92f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelRect, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(42f, 34f);
            textRect.offsetMax = new Vector2(-42f, -34f);
            text = textObject.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = true;
            text.fontSize = 34f;
            text.lineSpacing = 2f;
            text.color = new Color(0.92f, 0.96f, 0.98f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return canvas;
        }

        void UpdatePanelPose()
        {
            if (headTransform == null)
            {
                return;
            }

            var basePosition = headTransform.position + headTransform.forward * panelDistanceFromHead + headTransform.up * panelVerticalOffset;
            var rotation = headTransform.rotation;
            if (_mainCanvas != null)
            {
                _mainCanvas.transform.SetPositionAndRotation(basePosition, rotation);
            }

            if (_keyboardCanvas != null)
            {
                var keyboardPosition = basePosition + headTransform.right * keyboardHorizontalOffset + headTransform.up * -0.04f;
                _keyboardCanvas.transform.SetPositionAndRotation(keyboardPosition, rotation);
            }
        }

        void ShowKeyboard(bool visible)
        {
            if (_keyboardCanvas != null)
            {
                _keyboardCanvas.enabled = visible;
            }

            if (_keyboardBackground != null)
            {
                _keyboardBackground.enabled = visible;
            }

            if (_keyboardText != null)
            {
                _keyboardText.enabled = visible;
            }
        }

        void PollInput()
        {
            _nextPressed |= Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow) || OvrButtonDown(OVRInput.Button.Two);
            _previousPressed |= Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow) || OvrButtonDown(OVRInput.Button.One);
            _selectPressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) ||
                              OvrRawButtonDown(OVRInput.RawButton.RIndexTrigger) || OvrRawButtonDown(OVRInput.RawButton.LIndexTrigger);

            var axis = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
            var vertical = Mathf.Abs(axis.y);
            if (_verticalStickArmed && vertical > 0.7f)
            {
                if (axis.y > 0f)
                {
                    _previousPressed = true;
                }
                else
                {
                    _nextPressed = true;
                }

                _verticalStickArmed = false;
            }
            else if (vertical < 0.25f)
            {
                _verticalStickArmed = true;
            }

            var horizontal = Mathf.Abs(axis.x);
            if (_horizontalStickArmed && horizontal > 0.7f)
            {
                if (axis.x > 0f)
                {
                    _nextPressed = true;
                }
                else
                {
                    _previousPressed = true;
                }

                _horizontalStickArmed = false;
            }
            else if (horizontal < 0.25f)
            {
                _horizontalStickArmed = true;
            }
        }

        void UpdateSelection(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (ConsumeNext())
            {
                _stageSelectedIndex = (_stageSelectedIndex + 1) % count;
                PlayNavigationBlip();
            }

            if (ConsumePrevious())
            {
                _stageSelectedIndex = (_stageSelectedIndex + count - 1) % count;
                PlayNavigationBlip();
            }
        }

        bool ConsumeNext()
        {
            if (!_nextPressed)
            {
                return false;
            }

            _nextPressed = false;
            return true;
        }

        bool ConsumePrevious()
        {
            if (!_previousPressed)
            {
                return false;
            }

            _previousPressed = false;
            return true;
        }

        bool ConsumeSelect()
        {
            if (!_selectPressed)
            {
                return false;
            }

            _selectPressed = false;
            return true;
        }

        bool ShouldAutoAdvance()
        {
            if (!validationFastMode)
            {
                return false;
            }

            return Time.unscaledTime >= _nextSyntheticAdvanceAt;
        }

        void ArmSyntheticAdvance()
        {
            _nextSyntheticAdvanceAt = Time.unscaledTime + Mathf.Max(0.25f, fastQuestionnaireSeconds);
        }

        void ResolveReferences(bool forceRefresh)
        {
            if (inputManager == null || forceRefresh)
            {
                inputManager = GetComponent<QuestVrInputManager>();
                if (inputManager == null)
                {
                    inputManager = FindAnyObjectByType<QuestVrInputManager>();
                }
            }

            if (headTransform == null || forceRefresh)
            {
                var cameraRig = FindAnyObjectByType<OVRCameraRig>();
                if (cameraRig != null)
                {
                    headTransform = cameraRig.centerEyeAnchor;
                }
                else if (Camera.main != null)
                {
                    headTransform = Camera.main.transform;
                }
            }

            if (buttonTransform == null || forceRefresh)
            {
                var button = GameObject.Find("Big Red Button");
                buttonTransform = button != null ? button.transform : null;
            }
        }

        void SubscribeToButtonPresses()
        {
            if (inputManager == null)
            {
                return;
            }

            inputManager.ButtonPressAccepted -= HandleButtonPress;
            inputManager.ButtonPressAccepted += HandleButtonPress;
        }

        void UnsubscribeFromButtonPresses()
        {
            if (inputManager != null)
            {
                inputManager.ButtonPressAccepted -= HandleButtonPress;
            }
        }

        void EnsureAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        void ReadValidationIntentExtras()
        {
            if (!allowValidationIntentExtras)
            {
                return;
            }

            validationFastMode |= ReadBoolExtra("brb.unityStudyValidation", false);
            visualValidationMode |= ReadBoolExtra("brb.unityVisualValidation", false);
            rendererPretestMode |= ReadBoolExtra("brb.unityRendererPretest", false);
            defaultLanguage = NormalizeLanguage(ReadStringExtra("brb.studyLanguage", defaultLanguage));
        }

        void LogStructuralMarkers()
        {
            Debug.Log(
                $"[BRB_UNITY_STUDY_BOOT] contract={ContractVersion} appVariant={UnityVersionMarker} " +
                $"localizedAudio=StreamingAssets/{AudioRoot}/{LocalizedAudioRoot}/manifest.json directPolar=present directLsl=present questionnaireBridge=present");
            if (visualValidationMode)
            {
                Debug.Log("[BRB_UNITY_VISUAL_READY] stage=boot panels=world_space renderer=unity");
            }
        }

        void LogNameKeyboardContract()
        {
            if (_keyboardContractLogged)
            {
                return;
            }

            _keyboardContractLogged = true;
            Debug.Log(
                $"[{NameKeyboardContractMarker}] keyboardPanel=keyboard_panel presentation=pop_out_spatial_panel " +
                "radialReference=headset_center orientation=faces_headset nonObstructing=true fovVisible=true");
        }

        void PlayChoiceBlip()
        {
            Debug.Log("[BRB_UNITY_AUDIO_CUE] cue=questionnaire_choice audioId=sfx_9000 asset=shared/sfx_9000_questionnaire_choice_blip.wav language=shared gated=0");
        }

        void PlayNavigationBlip()
        {
            Debug.Log("[BRB_UNITY_AUDIO_CUE] cue=questionnaire_navigation audioId=sfx_9010 asset=shared/sfx_9010_questionnaire_navigation_blip.wav language=shared gated=0");
        }

        static bool TryGetAudioSpec(string audioId, out AudioSpec spec)
        {
            for (var i = 0; i < AudioCatalog.Length; i++)
            {
                if (string.Equals(AudioCatalog[i].AudioId, audioId, StringComparison.Ordinal))
                {
                    spec = AudioCatalog[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }

        static string LocalizedPresenceItem(string itemId)
        {
            return itemId switch
            {
                "ipq_sp1" => "In the previous button session, I had a sense that the Big Red Button was really there with me.",
                "ipq_sp2" => "I felt that the Big Red Button occupied the space in front of me.",
                "ipq_sp3" => "I felt present with the Big Red Button.",
                "ipq_inv1" => "I was aware of the real room while focusing on the Big Red Button.",
                "ipq_real1" => "The Big Red Button seemed like a real object.",
                _ => "Presence item"
            };
        }

        static string ConditionLabelForStage(StudyStage stage)
        {
            return stage switch
            {
                StudyStage.Condition1 or StudyStage.Pictographic1 or StudyStage.Presence1 => "1",
                StudyStage.Condition2 or StudyStage.Pictographic2 or StudyStage.Presence2 => "2",
                _ => "none"
            };
        }

        static AudioType AudioTypeForPath(string relativePath)
        {
            var extension = Path.GetExtension(relativePath).ToLowerInvariant();
            return extension switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN
            };
        }

        static string StreamingAssetUrl(params string[] segments)
        {
            var path = Application.streamingAssetsPath.Replace("\\", "/").TrimEnd('/');
            for (var i = 0; i < segments.Length; i++)
            {
                path += "/" + segments[i].Replace("\\", "/").Trim('/');
            }

            return path;
        }

        static string NormalizeLanguage(string language)
        {
            return string.Equals(language, "ja", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(language, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? "ja-JP"
                : "en-US";
        }

        static string BuildParticipantId(string prefix)
        {
            var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "unity" : prefix.Trim();
            return $"{safePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }

        static string EscapeRichText(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        static string Csv(string value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        static string JsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        static void AppendJson(StringBuilder builder, string key, string value, bool comma, int indent)
        {
            builder.Append(' ', indent * 2);
            builder.Append($"\"{EscapeJson(key)}\": \"{EscapeJson(value)}\"");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        static void AppendJson(StringBuilder builder, string key, int value, bool comma, int indent)
        {
            builder.Append(' ', indent * 2);
            builder.Append($"\"{EscapeJson(key)}\": {value}");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        static void AppendJson(StringBuilder builder, string key, bool value, bool comma, int indent)
        {
            builder.Append(' ', indent * 2);
            builder.Append($"\"{EscapeJson(key)}\": {JsonBool(value)}");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        static bool OvrButtonDown(OVRInput.Button button)
        {
            try
            {
                return OVRInput.GetDown(button);
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool OvrRawButtonDown(OVRInput.RawButton button)
        {
            try
            {
                return OVRInput.GetDown(button);
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool ReadBoolExtra(string key, bool defaultValue)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity?.Call<AndroidJavaObject>("getIntent");
                return intent != null ? intent.Call<bool>("getBooleanExtra", key, defaultValue) : defaultValue;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BRB_UNITY_STUDY_INTENT] boolExtra={key} failed={ex.Message}");
            }
#endif
            return defaultValue;
        }

        static string ReadStringExtra(string key, string defaultValue)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity?.Call<AndroidJavaObject>("getIntent");
                var value = intent?.Call<string>("getStringExtra", key);
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BRB_UNITY_STUDY_INTENT] stringExtra={key} failed={ex.Message}");
            }
#endif
            return defaultValue;
        }
    }
}
