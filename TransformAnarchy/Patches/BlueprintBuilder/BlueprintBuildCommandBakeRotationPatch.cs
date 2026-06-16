using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TransformAnarchy
{
    // The build command only carries a "forward" direction, so peers rebuild rotation as
    // LookRotation(forward) and lose the gizmo's roll. In multiplayer we bake the full rotation into the
    // serialized blueprint stream here (this constructor runs only on the acting peer), then reset forward to identity
    // so the deterministic build() reproduces it identically on every peer. Single-player keeps the static path.
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
                Debug.Log("TA F3: BlueprintBuildCommand(byte[], Vector3, Vector3, LoadOptions) constructor found");
            else
                Debug.LogError("TA F3: BlueprintBuildCommand constructor NOT FOUND");
            return ctor;
        }

        [HarmonyPostfix]
        static void Postfix(BlueprintBuildCommand __instance)
        {
            // Single-player keeps the existing static + transpiler path.
            if (!CommandController.Instance.isInMultiplayerMode())
                return;

            if (!BuilderFunctions.PendingBlueprintRotation.HasValue)
                return;

            Quaternion curRot = BuilderFunctions.PendingBlueprintRotation.Value;
            // Clear the acting-peer-only static so the transpiler falls through to LookRotation on every peer.
            BuilderFunctions.PendingBlueprintRotation = null;

            // Nothing to bake if LookRotation(forward) already reproduces the rotation (no roll).
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

                // Mirror BlueprintBuilderImplementation.build(): Track4 objects are left untransformed.
                Vector3 originalPivot = BlueprintBuilder.getPivot(objects);
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] is Track4) continue;
                    Transform t = objects[i].transform;
                    t.position = t.position.RotateAroundPivot(originalPivot, curRot);
                    t.rotation = curRot * t.rotation;

                    // Push the new orientation into the fields that get serialized (normally synced in Initialize(), which we skip).
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

                // The floored bounding-box pivot shifts when the object cloud rotates; compensate so build()'s pivot subtraction still lands at the gizmo position.
                Vector3 bakedPivot = BlueprintBuilder.getPivot(objects);

                byte[] baked = SerializeBlueprintObjects(objects);

                data.blueprintData = baked;
                data.position += bakedPivot - originalPivot;
                data.forward = Vector3.forward;
                dataField.SetValue(__instance, data);
            }
            catch (Exception e)
            {
                // On failure the original forward still carries yaw + pitch, so peers stay deterministic - only roll is lost.
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

        // Deserializes the blueprint stream into transient instances (never calls Initialize(), so they aren't registered with the park) for re-serialization; the caller destroys them.
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

        // Re-serializes via BlueprintSerializer and strips the outer "SM" header, returning the bare gzip stream that BlueprintBuildCommand.Data.blueprintData expects.
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
