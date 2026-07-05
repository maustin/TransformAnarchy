using HarmonyLib;
using System.Reflection;
using TransformAnarchy;

[HarmonyPatch]
public class BlueprintBuilderOnDisablePrefix {
    static MethodBase TargetMethod() => AccessTools.Method(typeof(BlueprintBuilder), "OnDisable");

    [HarmonyPrefix]
    public static bool Prefix() {
        TA.MainController.OnBuilderDisable();
        return true;
    }
}