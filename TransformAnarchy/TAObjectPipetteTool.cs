using UnityEngine;

namespace TransformAnarchy
{
    public class TAObjectPipetteTool : AbstractPipetteTool
    {
        protected override bool isPickableObject(BuildableObject buildableObject)
        {
            return buildableObject is Deco && buildableObject.isAvailable();
        }
    }
}
