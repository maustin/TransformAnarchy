using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace TransformAnarchy
{
    [HarmonyPatch]
    public class BlueprintBuilderImplementationBuildPatch
    {
        static MethodBase TargetMethod()
        {
            foreach (var t in typeof(BlueprintBuilderImplementation).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (t.Name.StartsWith("<build>"))
                {
                    var m = AccessTools.Method(t, "MoveNext");
                    if (m != null) {
                        return m;
                    } else {
                        Debug.LogError("TA: BlueprintBuilderImplementationBuildPatch Could not find BlueprintBuilderImplementation.<build> MoveNext");
                        return null;
                    }
                }
            }
            Debug.LogError("TA: BlueprintBuilderImplementationBuildPatch Could not locate BlueprintBuilderImplementation.<build>");
            return null;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var lookRotation = AccessTools.Method(typeof(Quaternion), "LookRotation", new[] { typeof(Vector3) });
            var rotateAroundPivot = AccessTools.Method(typeof(ExtensionMethods), "RotateAroundPivot",
                new[] { typeof(Vector3), typeof(Vector3), typeof(Quaternion) });
            var rotHelper = AccessTools.Method(typeof(BlueprintBuilderImplementationBuildPatch), "GetBlueprintRotation");
            var scaleHelper = AccessTools.Method(typeof(BlueprintBuilderImplementationBuildPatch), "ScalePosition");

            foreach (var instr in instructions)
            {
                yield return instr.Calls(lookRotation)
                    ? new CodeInstruction(OpCodes.Call, rotHelper)
                    : instr;

                if (instr.Calls(rotateAroundPivot))
                    yield return new CodeInstruction(OpCodes.Call, scaleHelper);
            }
        }

        public static Quaternion GetBlueprintRotation(Vector3 forward)
        {
            if (BuilderFunctions.PendingBlueprintRotation.HasValue)
            {
                var rot = BuilderFunctions.PendingBlueprintRotation.Value;
                BuilderFunctions.PendingBlueprintRotation = null;
                return rot;
            }
            return Quaternion.LookRotation(forward);
        }

        public static Vector3 ScalePosition(Vector3 position)
        {
            return BuilderFunctions.PendingBlueprintScale.HasValue
                ? position * BuilderFunctions.PendingBlueprintScale.Value
                : position;
        }
    }

    [HarmonyPatch(typeof(BlueprintBuilderImplementation), "onAfterBuild")]
    class BlueprintBuilderImplementationAfterBuildPatch
    {
        [HarmonyPostfix]
        static void Postfix(List<BuildableObject> builtObjectInstances)
        {
            if (!BuilderFunctions.AppliedBlueprintScale.HasValue) return;
            float scale = BuilderFunctions.AppliedBlueprintScale.Value;
            BuilderFunctions.AppliedBlueprintScale = null;

            if (scale == 1.0f) return;

            foreach (var obj in builtObjectInstances)
            {
                var customSize = obj.GetComponentInChildren<CustomSize>();
                if (customSize == null) continue;
                float newSize = Mathf.Clamp(customSize.getValue() * scale, customSize.minSize, customSize.maxSize);
                customSize.setValue(newSize);
            }
        }
    }
}
