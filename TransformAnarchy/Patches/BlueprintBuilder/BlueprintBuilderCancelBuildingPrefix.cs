using HarmonyLib;
using System.Reflection;
using UnityEngine;

[HarmonyPatch]
public class BlueprintBuilderCancelBuildingPrefix {
    static MethodBase TargetMethod() => AccessTools.Method(typeof(BlueprintBuilder), "cancelBuilding");

    [HarmonyPrefix]
    public static bool Prefix() {
        Debug.Log("TA: BlueprintBuilder.cancelBuilding Prefix");

        if (TransformAnarchy.TA.MainController.GizmoEnabled) {
            TransformAnarchy.TA.MainController.OnBuilderDisable();
        } else {
            Debug.Log("TA: Gizmo disabled, skip");
        }

        return true;
    }
}
