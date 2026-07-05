using HarmonyLib;
using System.Reflection;
using UnityEngine;

[HarmonyPatch]
public class BuilderOnAfterBuildCommandsTriggeredPrefix {
    static MethodBase TargetMethod() => AccessTools.Method(typeof(Builder), "onAfterBuildCommandsTriggered");

    [HarmonyPrefix]
    public static bool Prefix(Builder __instance) {
        Debug.Log("TA: Builder.onAfterBuildCommandsTriggered Prefix");

        var bb = __instance as BlueprintBuilder;
        if (bb != null) {
            Debug.Log("Is BlueprintBuilder");

            if (TransformAnarchy.TA.MainController.GizmoEnabled) {
                Debug.Log("TA Gizmo enabled! Skip");
                return false;
            }
            Debug.Log("TA Gizmo disabled");
        }

        return true;
    }
}
