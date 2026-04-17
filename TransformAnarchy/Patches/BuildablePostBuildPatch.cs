using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TransformAnarchy
{
    // Applies TA's per-axis scale to freshly-built Deco objects.
    // handleOnBuild is defined on BuilderImplementation<T>
    // Harmony needs a closed generic MethodBase, so TargetMethods yields the Deco concrete instantiation.
    [HarmonyPatch]
    public static class BuildablePostBuildPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var deco = AccessTools.Method(typeof(BuilderImplementation<DecoBuildCommand.Data>), "handleOnBuild");
            if (deco != null) yield return deco;
            else Debug.LogError("TA: BuildablePostBuildPatch could not resolve handleOnBuild for Deco");
        }

        static void Postfix(BuildableObject builtObjectInstance)
        {
            if (builtObjectInstance == null) return;
            if (!BuilderFunctions.PendingBuildScale.HasValue) return;

            Vector3 scale = BuilderFunctions.PendingBuildScale.Value;
            if (scale == Vector3.one) return;

            // Compose with whatever scale the buildable already has (e.g. CustomSize's uniform factor) so +/- hotkeys keep working on top of the per-axis scale
            var t = builtObjectInstance.transform;
            Vector3 existing = t.localScale;
            Vector3 finalScale = new Vector3(existing.x * scale.x, existing.y * scale.y, existing.z * scale.z);
            t.localScale = finalScale;

            // Persist the composed scale so it survives park save/load
            if (builtObjectInstance.tryGetCustomData<TAScale>(out var taScale))
            {
                taScale.scaleX = finalScale.x;
                taScale.scaleY = finalScale.y;
                taScale.scaleZ = finalScale.z;
            }
            else
            {
                builtObjectInstance.addCustomData(new TAScale { scaleX = finalScale.x, scaleY = finalScale.y, scaleZ = finalScale.z });
            }
        }
    }
}
