using TheBigRedButtonInstitute.Study;
using TheBigRedButtonInstitute.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBigRedButtonInstitute.Editor
{
    public static class BrbUnityFirstStudySceneInstaller
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string StudyRuntimeName = "BRB Unity First Study";
        const string ButtonName = "Big Red Button";

        [MenuItem("Tools/Big Red Button/Install Unity First Study Runtime")]
        public static void InstallFromMenu()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var runtimeRoot = GameObject.Find("VR Runtime") ?? new GameObject("VR Runtime");
            var inputManager = runtimeRoot.GetComponent<QuestVrInputManager>() ?? Object.FindFirstObjectByType<QuestVrInputManager>(FindObjectsInactive.Include);
            var headTransform = ResolveHeadTransform();
            var buttonTransform = ResolveButtonTransform();
            InstallIntoOpenScene(scene, runtimeRoot, inputManager, headTransform, buttonTransform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static BrbUnityFirstStudyController InstallIntoOpenScene(
            Scene scene,
            GameObject runtimeRoot,
            QuestVrInputManager inputManager,
            Transform headTransform,
            Transform buttonTransform)
        {
            if (runtimeRoot == null)
            {
                runtimeRoot = new GameObject("VR Runtime");
                SceneManager.MoveGameObjectToScene(runtimeRoot, scene);
            }

            var studyRoot = runtimeRoot.transform.Find(StudyRuntimeName);
            if (studyRoot == null)
            {
                var studyObject = new GameObject(StudyRuntimeName);
                studyObject.transform.SetParent(runtimeRoot.transform, false);
                studyRoot = studyObject.transform;
            }

            var controller = studyRoot.GetComponent<BrbUnityFirstStudyController>() ??
                             studyRoot.gameObject.AddComponent<BrbUnityFirstStudyController>();
            if (studyRoot.GetComponent<AudioSource>() == null)
            {
                studyRoot.gameObject.AddComponent<AudioSource>();
            }

            controller.ConfigureReferences(inputManager, headTransform, buttonTransform);
            EditorUtility.SetDirty(studyRoot.gameObject);
            EditorUtility.SetDirty(controller);
            Debug.Log("Installed BRB Unity first-study runtime into SampleScene.");
            return controller;
        }

        static Transform ResolveHeadTransform()
        {
            var cameraRig = Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (cameraRig != null)
            {
                return cameraRig.centerEyeAnchor;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        static Transform ResolveButtonTransform()
        {
            var button = GameObject.Find(ButtonName);
            return button != null ? button.transform : null;
        }
    }
}
