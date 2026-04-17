using System.Collections.Generic;

namespace TransformAnarchy
{
    public class TAScale : SerializedRawObject
    {
        [Serialized] public float scaleX = 1f;
        [Serialized] public float scaleY = 1f;
        [Serialized] public float scaleZ = 1f;

        // TODO: Are these needed?
        public override void serialize(SerializationContext context, Dictionary<string, object> values)
            => base.serialize(context, values);

        public override void deserialize(SerializationContext context, Dictionary<string, object> values)
            => base.deserialize(context, values);
    }
}
