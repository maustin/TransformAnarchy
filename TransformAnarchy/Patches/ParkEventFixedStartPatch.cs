using HarmonyLib;
using UnityEngine;

namespace TransformAnarchy
{
    [HarmonyPatch(typeof(Park), "eventFixedStart")]
    public static class ParkEventFixedStartPatch
    {
        static void Postfix()
        {
            // On Park start, iterate through all objects and apply custom scale if present
            foreach (var obj in GameController.Instance.getSerializedObjects())
            {
                if (obj.tryGetCustomData<TAScale>(out var taScale))
                    obj.transform.localScale = new Vector3(taScale.scaleX, taScale.scaleY, taScale.scaleZ);
            }
        }
    }
}
