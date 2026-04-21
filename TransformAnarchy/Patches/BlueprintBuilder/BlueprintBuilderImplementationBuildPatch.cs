using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace TransformAnarchy
{
    [HarmonyPatch]
    class BlueprintBuilderImplementationBuildPatch
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
            var helper = AccessTools.Method(typeof(BlueprintBuilderImplementationBuildPatch), "GetBlueprintRotation");

            foreach (var instr in instructions)
            {
                yield return instr.Calls(lookRotation)
                    ? new CodeInstruction(OpCodes.Call, helper)
                    : instr;
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
    }
}
