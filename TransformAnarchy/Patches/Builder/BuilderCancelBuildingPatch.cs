using HarmonyLib;
using System.Reflection;
using UnityEngine;

[HarmonyPatch]
public class BuilderCancelBuildingPatch {
    static MethodBase TargetMethod() => AccessTools.Method(typeof(Builder), "cancelBuilding");

    [HarmonyPrefix]
    public static bool Prefix() {
        Debug.Log("TA: Builder.cancelBuilding Prefix");

        if (TransformAnarchy.TA.MainController.GizmoEnabled) {
            Debug.Log("TA: DO IT");
            TransformAnarchy.TA.MainController.OnBuilderDisable();
        } else {
            Debug.Log("TA: SKIP IT");
        }

        return true;
    }
}
