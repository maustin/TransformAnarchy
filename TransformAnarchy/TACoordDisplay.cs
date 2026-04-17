using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TransformAnarchy
{
    // Manages the coordinates text-entry panel that appears in MOVE and ROTATE modes.
    // Create this component on a new GameObject, then call Initialize().
    // Position the host GameObject in screen-space to place the panels.
    public class TACoordDisplay : MonoBehaviour
    {
        public event Action<Vector3> OnPositionCommit;
        public event Action<Vector3> OnRotationCommit;

        private readonly InputField[] _positionFields = new InputField[3];
        private readonly InputField[] _rotationFields = new InputField[3];
        private GameObject _positionPanel;
        private GameObject _rotationPanel;

        // Last-known values; used to restore a field if the user types invalid input.
        private Vector3 _lastPosition;
        private Vector3 _lastEuler;

        // When true, programmatic text changes won't trigger commit callbacks.
        private bool _suppressCallbacks;

        public void Initialize()
        {
            _positionPanel = CreatePanel("PosPanel");
            _positionFields[0] = AddRow(_positionPanel.transform, "X", new Color(1f, 1f, 1f));
            _positionFields[1] = AddRow(_positionPanel.transform, "Y", new Color(1f, 1f, 1f));
            _positionFields[2] = AddRow(_positionPanel.transform, "Z", new Color(1f, 1f, 1f));
            WireFields(_positionFields, isPosition: true);

            _rotationPanel = CreatePanel("RotPanel");
            _rotationFields[0] = AddRow(_rotationPanel.transform, "X", new Color(1f, 1f, 1f));
            _rotationFields[1] = AddRow(_rotationPanel.transform, "Y", new Color(1f, 1f, 1f));
            _rotationFields[2] = AddRow(_rotationPanel.transform, "Z", new Color(1f, 1f,  1f));
            WireFields(_rotationFields, isPosition: false);

            _positionPanel.SetActive(false);
            _rotationPanel.SetActive(false);
        }

        // ── Panel / row builders ──────────────────────────────────────────────

        private GameObject CreatePanel(string name)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(transform, false);

            var rt = panel.AddComponent<RectTransform>();
            rt.pivot       = new Vector2(0.5f, 0.5f);
            rt.anchorMin   = new Vector2(0.5f, 0.5f);
            rt.anchorMax   = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta   = new Vector2(80f, 76f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.65f, 1f, 1f, 0.2f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding            = new RectOffset(4, 4, 4, 4);
            vlg.spacing            = 2f;
            vlg.childControlWidth  = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var csf = panel.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            return panel;
        }

        private InputField AddRow(Transform parent, string axisLabel, Color labelColor)
        {
            // Row
            var row = new GameObject("Row_" + axisLabel);
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 22f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing            = 4f;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth  = false;
            hlg.childForceExpandWidth  = false;

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(18f, 22f);
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.minWidth = 18f;
            labelLE.preferredWidth = 18f;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text      = axisLabel;
            labelText.font      = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize  = 11;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color     = labelColor;
            labelText.alignment = TextAnchor.MiddleCenter;

            // Input field
            var field = CreateInputField(row.transform, axisLabel);
            var fieldLE = field.gameObject.AddComponent<LayoutElement>();
            fieldLE.minHeight       = 22f;
            fieldLE.preferredHeight = 22f;

            return field;
        }

        private InputField CreateInputField(Transform parent, string name)
        {
            var go = new GameObject(name + "_Field");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(50f, 22f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.35f, 0.35f, 0.35f, 1f);

            var field = go.AddComponent<InputField>();
            field.contentType    = InputField.ContentType.DecimalNumber;
            field.characterLimit = 12;

            // Visible text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin  = Vector2.zero;
            textRT.anchorMax  = Vector2.one;
            textRT.offsetMin  = new Vector2(4f,  2f);
            textRT.offsetMax  = new Vector2(-4f, -2f);
            var textComp = textGO.AddComponent<Text>();
            textComp.font      = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComp.fontSize  = 11;
            textComp.color     = Color.white;
            textComp.alignment = TextAnchor.MiddleRight;
            field.textComponent = textComp;

            // Placeholder
            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var phRT = phGO.AddComponent<RectTransform>();
            phRT.anchorMin  = Vector2.zero;
            phRT.anchorMax  = Vector2.one;
            phRT.offsetMin  = new Vector2(4f,  2f);
            phRT.offsetMax  = new Vector2(-4f, -2f);
            var phText = phGO.AddComponent<Text>();
            phText.font      = Resources.GetBuiltinResource<Font>("Arial.ttf");
            phText.fontSize  = 11;
            phText.color     = new Color(1f, 1f, 1f, 0.3f);
            phText.fontStyle = FontStyle.Italic;
            phText.text      = "0.00";
            phText.alignment = TextAnchor.MiddleRight;
            field.placeholder = phText;
            field.navigation  = new Navigation { mode = Navigation.Mode.None };

            return field;
        }

        // ── Input handling ────────────────────────────────────────────────────

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Tab)) return;

            InputField[] fields = null;
            if (_positionPanel != null && _positionPanel.activeSelf)
                fields = _positionFields;
            else if (_rotationPanel != null && _rotationPanel.activeSelf)
                fields = _rotationFields;
            if (fields == null) return;

            bool backward = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            int step = backward ? 2 : 1; // +2 mod 3 == -1 mod 3

            for (int i = 0; i < 3; i++)
            {
                if (fields[i] == null || !fields[i].isFocused) continue;
                fields[i].DeactivateInputField();
                var next = fields[(i + step) % 3];
                next.ActivateInputField();
                next.Select();
                break;
            }
        }

        // ── Event wiring ─────────────────────────────────────────────────────

        private void WireFields(InputField[] fields, bool isPosition)
        {
            for (int i = 0; i < 3; i++)
            {
                int axis = i; // capture for lambda
                fields[i].onEndEdit.AddListener(value =>
                {
                    if (!_suppressCallbacks)
                        CommitInput(axis, value, isPosition);
                });
            }
        }

        private void CommitInput(int axis, string value, bool isPosition)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                // Restore the last-known valid value on bad input
                if (isPosition)
                    SetText(_positionFields[axis], _lastPosition[axis], "F2");
                else
                    SetText(_rotationFields[axis], _lastEuler[axis],    "F1");
                return;
            }

            if (isPosition)
            {
                Vector3 newPos = _lastPosition;
                newPos[axis]   = result;
                _lastPosition  = newPos;
                OnPositionCommit?.Invoke(newPos);
            }
            else
            {
                Vector3 newEuler = _lastEuler;
                newEuler[axis]   = result;
                _lastEuler       = newEuler;
                OnRotationCommit?.Invoke(newEuler);
            }
        }

        // ── Public update API ─────────────────────────────────────────────────

        // Call every frame (or after drag) from TAController.
        // Skips update while the user is actively editing a field.
        public void UpdatePosition(Vector3 worldPos)
        {
            _lastPosition = worldPos;
            if (AnyFocused(_positionFields)) return;

            _suppressCallbacks = true;
            SetText(_positionFields[0], worldPos.x, "F2");
            SetText(_positionFields[1], worldPos.y, "F2");
            SetText(_positionFields[2], worldPos.z, "F2");
            _suppressCallbacks = false;
        }

        public void UpdateRotation(Vector3 euler)
        {
            _lastEuler = euler;
            if (AnyFocused(_rotationFields)) return;

            _suppressCallbacks = true;
            SetText(_rotationFields[0], NormalizeAngle(euler.x), "F1");
            SetText(_rotationFields[1], NormalizeAngle(euler.y), "F1");
            SetText(_rotationFields[2], NormalizeAngle(euler.z), "F1");
            _suppressCallbacks = false;
        }

        public void ShowPositionMode()
        {
            _positionPanel.SetActive(true);
            _rotationPanel.SetActive(false);
        }

        public void ShowRotationMode()
        {
            _positionPanel.SetActive(false);
            _rotationPanel.SetActive(true);
        }

        public void HideAll()
        {
            _positionPanel.SetActive(false);
            _rotationPanel.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool AnyFocused(InputField[] fields)
        {
            foreach (var f in fields)
                if (f != null && f.isFocused) return true;
            return false;
        }

        private static void SetText(InputField field, float value, string format)
        {
            if (field == null) return;
            field.text = value.ToString(format, CultureInfo.InvariantCulture);
        }

        // Map Euler angle to [-180, 180] for nicer display (e.g. -10 instead of 350).
        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f)  angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
