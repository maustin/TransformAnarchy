using HarmonyLib;
using System.Reflection;
using UnityEngine;

// TA keeps the blueprint builder alive across placements (onlyBuildOne = false), so the same ghost - and
// its combinedHullMesh - is reused for every placement. But each placement's build command destroys that
// ghost's combined-hull meshes: BlueprintBuilder.createBuildPreviewData adds generatedMeshes to the
// command's cleanedUpObjects, and AbstractBaseBuildCommand.run() destroys them when the command executes.
//
// In multiplayer the command runs asynchronously, so by the second placement the first command has already
// destroyed those meshes. The MP build preview (Builder.addToMultiplayerBuildPreview) then calls
// CommandBuffer.DrawMesh on a now-destroyed mesh and throws ArgumentNullException, which aborts buildObjects
// entirely - so a TA blueprint could only ever be placed once in MP. (Single-player never draws the MP
// preview, so it is unaffected.)
//
// Fix: before the MP preview is built, drop the stale combined-hull GameObject. The engine then falls back
// to drawing each live ghost object, whose meshes are never destroyed, so the preview still works and the
// build proceeds.
[HarmonyPatch]
class BlueprintBuilderCreateBuildPreviewDataPrefix {
    static readonly FieldInfo combinedHullMeshField = AccessTools.Field(typeof(Builder), "combinedHullMesh");

    static MethodBase TargetMethod() {
        MethodBase methodBase = AccessTools.Method(typeof(BlueprintBuilder), "createBuildPreviewData");
        if (methodBase != null) {
            Debug.Log("TA: BlueprintBuilder.createBuildPreviewData method found");
        } else {
            Debug.Log("TA: BlueprintBuilder.createBuildPreviewData method NOT FOUND");
        }
        return methodBase;
    }

    [HarmonyPrefix]
    static void Prefix(BlueprintBuilder __instance) {
        // Only the multiplayer preview path draws these meshes, so this is the only case that can crash.
        if (!CommandController.Instance.isInMultiplayerMode()) return;

        GameObject combinedHullMesh = (GameObject)combinedHullMeshField.GetValue(__instance);
        if (combinedHullMesh == null) return;

        // If any combined-hull mesh has been destroyed by a previous placement's build command, the whole
        // combined hull is stale - drop it and let the engine draw the live ghost objects instead.
        foreach (MeshFilter meshFilter in combinedHullMesh.GetComponentsInChildren<MeshFilter>()) {
            if (meshFilter.sharedMesh == null) {
                Object.Destroy(combinedHullMesh);
                combinedHullMeshField.SetValue(__instance, null);
                return;
            }
        }
    }
}
