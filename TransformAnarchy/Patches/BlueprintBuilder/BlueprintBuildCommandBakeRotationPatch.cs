using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TransformAnarchy
{
    // MP_AUDIT F3 fix. The blueprint build command only carries a "forward" direction, so a remote
    // (or vanilla) peer rebuilds the rotation as Quaternion.LookRotation(forward) - yaw and pitch
    // survive, but roll (the Z-axis tilt the gizmo allows) is lost. Single-player smuggles the full
    // gizmo quaternion across via the BuilderFunctions.PendingBlueprintRotation static, consumed by
    // the transpiler in BlueprintBuilderImplementationBuildPatch - but that static only exists on the
    // acting peer, so it desyncs MP.
    //
    // The only mixed-session-safe channel is the serialized blueprint data itself. So in multiplayer
    // we bake the full rotation into the byte stream here, at command-construction time (which runs
    // only on the acting peer; remote peers deserialize Data directly and never hit this ctor). We
    // pre-rotate every object and re-serialize, then set forward to identity so the standard
    // deterministic build() reproduces the result on every peer - including vanilla peers with no TA.
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
            // Single-player keeps the existing static + transpiler path, which already works there.
            if (!CommandController.Instance.isInMultiplayerMode())
                return;

            if (!BuilderFunctions.PendingBlueprintRotation.HasValue)
                return;

            Quaternion curRot = BuilderFunctions.PendingBlueprintRotation.Value;
            // Never rely on the acting-peer-only static in MP - clear it so the transpiler in
            // BlueprintBuilderImplementationBuildPatch falls through to LookRotation on every peer.
            BuilderFunctions.PendingBlueprintRotation = null;

            // If the rotation is something LookRotation(forward) reproduces exactly (no roll), the
            // forward direction already carries it - every peer rebuilds it identically, so there's
            // nothing to bake.
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

                    // BuildableObject serializes from logicTransform and TreeEntity from a private
                    // serializedRotation - both normally synced during Initialize(), which we skip.
                    // Push the new orientation into them so the re-serialized stream actually carries it.
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

                // build() recomputes the pivot from the (now rotated) stream and subtracts it before
                // adding data.position. The pivot is a floored bounding-box centre, so rotating the
                // object cloud shifts it; compensate so the final placement matches the gizmo exactly.
                Vector3 bakedPivot = BlueprintBuilder.getPivot(objects);

                byte[] baked = SerializeBlueprintObjects(objects);

                data.blueprintData = baked;
                data.position += bakedPivot - originalPivot;
                data.forward = Vector3.forward;
                dataField.SetValue(__instance, data);
            }
            catch (Exception e)
            {
                // On failure the command is left with its original forward (which still carries yaw +
                // pitch) and no static, so every peer stays deterministic - only the roll is lost.
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

        // Deserializes the blueprint stream into transient instances (mirroring the first half of
        // BlueprintBuilderImplementation.build), but never calls Initialize() - so the objects are
        // never registered with the park. They exist only long enough to be re-serialized, then are
        // destroyed by the caller.
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

        // Re-serializes the objects through the game's BlueprintSerializer and strips the outer "SM"
        // container header, returning the bare gzip stream that BlueprintBuildCommand.Data.blueprintData
        // expects (the same form generateBuildCommands feeds in via getBlueprintDataStream).
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
