using HarmonyLib;
using System.Reflection;
using UnityEngine;

[HarmonyPatch]
public class BuilderOnDisablePrefix {
    static MethodBase TargetMethod() => AccessTools.Method(typeof(Builder), "OnDisable");

    [HarmonyPrefix]
    public static bool Prefix() {
        Debug.Log("TA: Builder.OnDisable Prefix");

        return true;
    }
}
