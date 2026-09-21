using PHYSXR.Ghost;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PHYSXR.EditorTools
{
    // One-shot, Editor-only scene-setup utility for the minimal Ghost
    // visual prototype. Lives under an Editor/ folder so Unity excludes it
    // from any player build - it is tooling, not runtime/production code,
    // and is not part of the decision/execution pipeline.
    //
    // Builds/rebuilds the "Ghost" GameObject in SampleScene.unity using
    // Unity's own primitive/material creation, rather than hand-authoring
    // scene YAML, so mesh/material references are guaranteed correct.
    // Idempotent: re-running it replaces any previous "Ghost" GameObject.
    public static class GhostPrototypeSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find("Ghost");
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghost.name = "Ghost";
            ghost.transform.position = new Vector3(0f, 1f, 0f);

            var visualController = ghost.AddComponent<GhostVisualController>();
            var runtime = ghost.AddComponent<GhostRuntime>();
            var binding = ghost.AddComponent<GhostVisualBinding>();
            var demoDriver = ghost.AddComponent<GhostVisualDemoDriver>();

            var bindingSerialized = new SerializedObject(binding);
            bindingSerialized.FindProperty("ghostRuntime").objectReferenceValue = runtime;
            bindingSerialized.FindProperty("visualController").objectReferenceValue = visualController;
            bindingSerialized.ApplyModifiedPropertiesWithoutUndo();

            var demoSerialized = new SerializedObject(demoDriver);
            demoSerialized.FindProperty("visualController").objectReferenceValue = visualController;
            demoSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("GhostPrototypeSceneSetup: 'Ghost' GameObject created/updated and wired in " + ScenePath);
        }
    }
}
