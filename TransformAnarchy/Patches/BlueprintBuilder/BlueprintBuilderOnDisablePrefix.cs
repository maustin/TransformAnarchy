using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

[HarmonyPatch]
public class BlueprintBuilderOnDisablePrefix {
    //static MethodBase TargetMethod() => AccessTools.Method(typeof(Builder), "OnDisable");
    static MethodBase TargetMethod() => AccessTools.Method(typeof(BlueprintBuilder), "OnDisable");

    [HarmonyPrefix]
    public static bool Prefix() {
        Debug.Log("TA: BlueprintBuilder.OnDisable Prefix");

        Debug.Log(Environment.StackTrace);

        return true;
    }
}
