using UnityEngine;

namespace TransformAnarchy
{
    public class ScaleGizmo : Gizmo<ScaleGizmoComponent>
    {
        public const float MIN_SCALE = 0.1f;
        public const float MAX_SCALE = 10.0f;

        private Vector3 _startingScale;
        private Vector3 _startingAxis;
        private Axis _activeAxis;
        private float _totalDragSoFar;

        public override void OnDragStart(DragInformation eventInfo)
        {
            _startingScale = TA.MainController.CurrentScale;
            _totalDragSoFar = 0;
            _activeAxis = eventInfo.ModifyAxis;

            switch (eventInfo.ModifyAxis)
            {
                case Axis.NONE:
                    return;
                case Axis.X:
                    _startingAxis = transform.right;
                    break;
                case Axis.Y:
                    _startingAxis = transform.up;
                    break;
                case Axis.Z:
                    _startingAxis = transform.forward;
                    break;
            }
        }

        public override void OnDrag(DragInformation eventInfo)
        {
            Vector3 projVec = Vector3.Project(eventInfo.DragDelta, _startingAxis);
            _totalDragSoFar += (Vector3.Dot(_startingAxis, projVec) > 0 ? 1 : -1) * projVec.magnitude;

            // 1 world unit of drag = 2x the starting scale on that axis
            float factor = Mathf.Pow(2f, _totalDragSoFar);

            Vector3 s = _startingScale;
            switch (_activeAxis)
            {
                case Axis.X: s.x = Mathf.Clamp(_startingScale.x * factor, MIN_SCALE, MAX_SCALE); break;
                case Axis.Y: s.y = Mathf.Clamp(_startingScale.y * factor, MIN_SCALE, MAX_SCALE); break;
                case Axis.Z: s.z = Mathf.Clamp(_startingScale.z * factor, MIN_SCALE, MAX_SCALE); break;
            }

            if (TA.MainController.ShouldSnap)
            {
                float gridStepSize = 0.1f / TA.MainController.GridSubdivision;
                s.x = Mathf.Round(s.x / gridStepSize) * gridStepSize;
                s.y = Mathf.Round(s.y / gridStepSize) * gridStepSize;
                s.z = Mathf.Round(s.z / gridStepSize) * gridStepSize;
                s.x = Mathf.Clamp(s.x, MIN_SCALE, MAX_SCALE);
                s.y = Mathf.Clamp(s.y, MIN_SCALE, MAX_SCALE);
                s.z = Mathf.Clamp(s.z, MIN_SCALE, MAX_SCALE);
            }

            TA.MainController.CurrentScale = s;

            UpdateGizmoTransforms();
        }

        public void UpdatePosition(Vector3 newPos)
        {
            transform.position = newPos;
            UpdateGizmoTransforms();
        }

        public void UpdateRotation(Quaternion newRot)
        {
            transform.rotation = newRot;
            UpdateGizmoTransforms();
        }

        // Scale is always local-space. Override to ignore _rotationMode
        protected override void UpdateGizmoTransforms()
        {
            XComponent.transform.rotation = transform.rotation * XAxisRotation();
            YComponent.transform.rotation = transform.rotation * YAxisRotation();
            ZComponent.transform.rotation = transform.rotation * ZAxisRotation();
        }

        public override Quaternion XAxisRotation() => Quaternion.LookRotation(Vector3.right, Vector3.up);
        public override Quaternion YAxisRotation() => Quaternion.LookRotation(Vector3.up, -Vector3.forward);
        public override Quaternion ZAxisRotation() => Quaternion.LookRotation(Vector3.forward, Vector3.up);
    }
}
