using UnityEngine;

namespace TransformAnarchy
{
    public class ScaleGizmoComponent : GizmoComponent
    {
        public override Vector3 GetPlaneOffset(Ray ray)
        {
            return PositionalGizmoComponent.ClosestPointsOnTwoLines(
                ray.origin, ray.direction, transform.position, transform.forward);
        }
    }
}
