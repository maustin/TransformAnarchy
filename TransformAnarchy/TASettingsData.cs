using System;


namespace TransformAnarchy
{
    [Serializable]
    public class TASettingsData
    {
        public float gizmoSize = 1f;
        public int gizmoStyle = 0;
        public float rotationAngle = 90f;
        public int useButtonForPipette = 1;
        public int gizmoRenderBehaviourString = 0;
        public bool showAdvancedSettings = false;
        public bool enableBlueprintScaling = true;

        // Min & max bounds for blueprint scaling. Leaving these as non-adjustable for now.
        public float customSizeMinimum = 0.1f;
        public float customSizeMaximum = 10.0f;
    }
}
