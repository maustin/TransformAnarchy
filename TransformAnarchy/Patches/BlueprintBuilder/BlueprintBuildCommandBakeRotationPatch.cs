using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TransformAnarchy
{
    // The build command only carries a "forward", so peers rebuild rotation with LookRotation and lose the roll.
    // In MP we bake the full rotation into the serialized blueprint here (runs on the acting peer only), then
    // reset forward so build() reproduces it the same on everyone. SP keeps the static path.
    [HarmonyPatch]
    public class BlueprintBuildCommandBakeRotationPatch
    {
        private static readonly FieldInfo dataField = AccessTools.Field(typeof(BlueprintBuildCommand), "data");
        private static readonly FieldInfo treeSerializedRotationField = AccessTools.Field(typeof(TreeEntity), "serializedRotation");

        static MethodBase TargetMethod()
        {
            MethodBase ctor = AccessTools.Constructor(typeof(BlueprintBuildCommand),
                new Type[] { typeof(byte[]), typeof(Vector3), typeof(Vector3), typeof(LoadOptions) });
            if (ctor != null)
                Debug.Log("TA: BlueprintBuildCommand(byte[], Vector3, Vector3, LoadOptions) constructor found");
            else
                Debug.LogError("TA: BlueprintBuildCommand constructor NOT FOUND");
            return ctor;
        }

        [HarmonyPostfix]
        static void Postfix(BlueprintBuildCommand __instance)
        {
            // SP keeps the static + transpiler path.
            if (!CommandController.Instance.isInMultiplayerMode())
                return;

            if (!BuilderFunctions.PendingBlueprintRotation.HasValue)
                return;

            Quaternion curRot = BuilderFunctions.PendingBlueprintRotation.Value;
            // clear it so the transpiler falls through to LookRotation on every peer
            BuilderFunctions.PendingBlueprintRotation = null;

            // no roll? then LookRotation already gets it right, nothing to bake
            Vector3 forward = curRot * Vector3.forward;
            if (Quaternion.Angle(curRot, Quaternion.LookRotation(forward)) < 0.01f)
                return;

            var data = (BlueprintBuildCommand.Data)dataField.GetValue(__instance);

            List<SerializedMonoBehaviour> objects = null;
            try
            {
                objects = DeserializeBlueprintObjects(data.blueprintData, data.LoadOptions);
                if (objects.Count == 0)
                    return;

                // same as BlueprintBuilderImplementation.build() - leave Track4 objects alone
                Vector3 originalPivot = BlueprintBuilder.getPivot(objects);
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] is Track4) continue;
                    Transform t = objects[i].transform;
                    t.position = t.position.RotateAroundPivot(originalPivot, curRot);
                    t.rotation = curRot * t.rotation;

                    // push the orientation into the serialized fields (Initialize() normally does this but we skip it)
                    if (objects[i] is BuildableObject buildable)
                        buildable.updateLogicTransform();
                    if (objects[i] is TreeEntity && treeSerializedRotationField != null)
                        treeSerializedRotationField.SetValue(objects[i], t.rotation);
                }
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] is TrackSegment4 trackSegment)
                        trackSegment.updatePositionAndLogicTransform();
                }

                // pivot moves when the cloud rotates - shift position to compensate so build() still lands at the gizmo
                Vector3 bakedPivot = BlueprintBuilder.getPivot(objects);

                byte[] baked = SerializeBlueprintObjects(objects);

                data.blueprintData = baked;
                data.position += bakedPivot - originalPivot;
                data.forward = Vector3.forward;
                dataField.SetValue(__instance, data);
            }
            catch (Exception e)
            {
                // if this fails the original forward still has yaw + pitch, so peers stay in sync - we just lose roll
                Debug.LogError("TA: BlueprintBuildCommandBakeRotationPatch failed to bake rotation, falling back to yaw/pitch: " + e);
            }
            finally
            {
                if (objects != null)
                {
                    for (int i = 0; i < objects.Count; i++)
                    {
                        if (objects[i] != null)
                            UnityEngine.Object.Destroy(objects[i].gameObject);
                    }
                }
            }
        }

        // deserialize the blueprint into throwaway objects (no Initialize() so they never register with the park). caller destroys them.
        private static List<SerializedMonoBehaviour> DeserializeBlueprintObjects(byte[] blueprintData, LoadOptions loadOptions)
        {
            var result = new List<SerializedMonoBehaviour>();
            MemoryStream baseStream = new MemoryStream(blueprintData);
            using (SavegameDeserializer deserializer = new SavegameDeserializer(SavegameDeserializer.getBlueprintStreamReader(baseStream), baseStream, new SerializationContext(SerializationContext.Context.Blueprint)))
            {
                Deserializer.Instance.start();
                foreach (ISerialized serialized in deserializer.loadNextObject(new LoadOptions?(loadOptions)))
                {
                    if (serialized is SerializedMonoBehaviour serializedMonoBehaviour && serializedMonoBehaviour != null)
                        result.Add(serializedMonoBehaviour);
                }
                for (int i = 0; i < result.Count; i++)
                    result[i].gameObject.SetActive(true);
                Deserializer.Instance.onAfterDeserialization();
                Deserializer.Instance.Dispose();
            }
            return result;
        }

        // re-serialize and strip the "SM" header back off, leaving the bare gzip stream that Data.blueprintData wants
        private static byte[] SerializeBlueprintObjects(List<SerializedMonoBehaviour> objects)
        {
            byte[] serialized = new BlueprintSerializer(objects, "TransformAnarchy baked blueprint").getSerialized();
            using (MemoryStream input = new MemoryStream(serialized))
            using (BinaryReader reader = new BinaryReader(input))
            {
                reader.ReadByte(); // 'S'
                reader.ReadByte(); // 'M'
                reader.ReadByte(); // version
                uint length = reader.ReadUInt32();
                reader.ReadBytes(16); // MD5 hash
                return reader.ReadBytes((int)length);
            }
        }
    }
}
