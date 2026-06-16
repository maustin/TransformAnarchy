using HarmonyLib;
using System.Reflection;
using UnityEngine;

// TA reuses one blueprint ghost across placements (onlyBuildOne = false), but each placement's build command
// destroys that ghost's combinedHullMesh. In MP the command runs async, so by the second placement the MP
// preview (Builder.addToMultiplayerBuildPreview) calls DrawMesh on a destroyed mesh and throws, aborting the
// build - a TA blueprint could only ever be placed once in MP. Fix: drop the stale combined hull before the
// MP preview is built so the engine falls back to drawing the live ghost objects. (SP never draws this preview.)
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
        // Only the MP preview path draws these meshes.
        if (!CommandController.Instance.isInMultiplayerMode()) return;

        GameObject combinedHullMesh = (GameObject)combinedHullMeshField.GetValue(__instance);
        if (combinedHullMesh == null) return;

        // If any mesh was destroyed by a previous placement's build command, the whole combined hull is stale - drop it.
        foreach (MeshFilter meshFilter in combinedHullMesh.GetComponentsInChildren<MeshFilter>()) {
            if (meshFilter.sharedMesh == null) {
                Object.Destroy(combinedHullMesh);
                combinedHullMeshField.SetValue(__instance, null);
                return;
            }
        }
    }
}
