using HarmonyLib;
using System.Reflection;
using UnityEngine;

// We reuse one ghost across placements, but each build command destroys its combinedHullMesh. In MP the command
// runs async, so the next placement's preview ends up calling DrawMesh on a dead mesh and throws - meaning a TA
// blueprint could only be placed once in MP. So drop the stale hull first and let the engine draw the live ghosts.
// (SP never draws this preview.)
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
        // only the MP preview draws these
        if (!CommandController.Instance.isInMultiplayerMode()) return;

        GameObject combinedHullMesh = (GameObject)combinedHullMeshField.GetValue(__instance);
        if (combinedHullMesh == null) return;

        // if a previous build destroyed any mesh the whole hull is stale, so toss it
        foreach (MeshFilter meshFilter in combinedHullMesh.GetComponentsInChildren<MeshFilter>()) {
            if (meshFilter.sharedMesh == null) {
                Object.Destroy(combinedHullMesh);
                combinedHullMeshField.SetValue(__instance, null);
                return;
            }
        }
    }
}
