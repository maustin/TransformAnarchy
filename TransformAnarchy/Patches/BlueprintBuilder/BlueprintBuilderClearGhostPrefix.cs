using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

[HarmonyPatch]
public class BlueprintBuilderClearGhostPrefix {
    //static MethodBase TargetMethod() => AccessTools.Method(typeof(Builder), "clearGhost");
    static MethodBase TargetMethod() => AccessTools.Method(typeof(BlueprintBuilder), "clearGhost");

    [HarmonyPrefix]
    public static bool Prefix() {
        Debug.Log("TA: BlueprintBuilder.clearGhost Prefix");

        Debug.Log(Environment.StackTrace);

        return true;
    }
}
