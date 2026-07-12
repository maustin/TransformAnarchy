using HarmonyLib;
using System.Reflection;
using TransformAnarchy;

[HarmonyPatch]
public class BuilderOnDisablePrefix {
    static MethodBase TargetMethod() => AccessTools.Method(typeof(Builder), "OnDisable");

    [HarmonyPrefix]
    public static bool Prefix() {
        TA.MainController.OnBuilderDisable();
        return true;
    }
}
