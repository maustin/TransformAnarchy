using Parkitect.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TransformAnarchy
{
    [DefaultExecutionOrder(-10)]
    public class TAController : MonoBehaviour
    {

        public GameObject ArrowGO;
        public GameObject RingGO;

        public Builder CurrentBuilder;
        public float BuilderSize;
        public float GridSubdivision = 1f;
        public bool ShouldSnap = false;
        public bool GizmoCurrentState = false;
        public bool GizmoEnabled = false;
        public bool GizmoControlsBeingUsed = false;
        public bool IsEditingOrigin = false;
        public bool useFixedGizmoSize = true;
        // public float gizmoSize;

        public enum Tool
        {
            MOVE,
            ROTATE
        };

        public Tool CurrentTool = Tool.MOVE;
        public ToolSpace CurrentSpace = ToolSpace.LOCAL;

        public PositionalGizmo positionalGizmo;
        public RotationalGizmo rotationalGizmo;
        private Camera _cachedMaincam;
        private Camera gizmoCamera;

        private Transform _gizmoHelperParent;
        private Transform _gizmoHelperChild;

        public GameObject UITransform;

        // Coordinate text-entry display (separate GO so it can be positioned independently)
        private GameObject _coordDisplayGO;
        private TACoordDisplay _coordDisplay;
        private bool _coordDisplayVisible = false;
        private GameObject _coordDisplayToggleGO;
        private UIButton UICoordDisplayToggle;
        private Sprite _coordToggleOpenSprite;
        private Sprite _coordToggleCloseSprite;

        public struct UIButton
        {
            public Button button;
            public Image icon;
            public UITooltip tooltip;

            public UIButton(Button b, Image i = null, UITooltip t = null)
            {
                this.button = b;
                this.icon = i;
                this.tooltip = t;
            }
        }

        public UIButton UIToolButton;
        public UIButton UISpaceButton;
        public UIButton UIBuildButton;
        public UIButton UIGizmoToggleButton;
        public UIButton UIResetRotationButton;
        public UIButton UIPivotEdit;
        public UIButton UIPivotCancel;

        // Flags
        public bool UseTransformFromLastBuilder = false;
        public bool PipetteWaitForMouseUp = false;
        private bool _alreadyToggledThisFrame = false;

        // Edit-placed-object state
        private TAObjectPipetteTool _editPipetteTool;
        private BuildableObject _editTarget;
        private Builder _editBuilder;

        // We cannot directly build the builder. So we instead do this.
        public bool ForceBuildThisFrame = false;
        private bool _dontUpdateGrid = false;

        // Allowed builder types
        public static HashSet<Type> AllowedBuilderTypes = new HashSet<Type>()
        {
            typeof(DecoBuilder),
            typeof(FlatRideBuilder),
            typeof(BlueprintBuilder)
        };

        public void OnBuilderEnable(Builder builder)
        {

            if (builder == CurrentBuilder)
            {
                return;
            }

            if (builder != null)
            {
                if (!AllowedBuilderTypes.Contains(builder.GetType()))
                {
                    Debug.Log("TA: TAController OnBuilderEnable");
                    OnBuilderDisable();
                    return;
                }
            }

            if (!UseTransformFromLastBuilder)
            {
                _gizmoHelperChild.transform.localPosition = Vector3.zero;
                _gizmoHelperChild.transform.localRotation = Quaternion.identity;
            }

            CurrentBuilder = builder;
            UpdateUIContent();
            SetGizmoCamera();

        }

        public void OnBuilderDisable()
        {

            if (CurrentBuilder == null)
            {
                return;
            }

            Debug.Log("TA: TAController OnBuilderDisable");

            UseTransformFromLastBuilder = GizmoEnabled &&
                (CurrentBuilder.GetType() == typeof(DecoBuilder) ||
                 CurrentBuilder.GetType() == typeof(BlueprintBuilder));
            StartCoroutine(StoppedBuildingWatch());

            CurrentBuilder = null;
            GizmoEnabled = false;
            GizmoCurrentState = false;
            positionalGizmo.SetActiveGizmo(false);
            rotationalGizmo.SetActiveGizmo(false);
            CurrentTool = Tool.MOVE;
            CurrentSpace = ToolSpace.LOCAL;
            _coordDisplayVisible = false;

            ClearBuilderGrid();
            UpdateUIContent();

        }

        public void InitGizmoTransform(GameObject ghost, Vector3 position, Quaternion rotation)
        {

            SetGizmoTransform(position, rotation);

            // Object size based gizmo
            if (TA.TASettings.gizmoStyle == 2)
            {
                BuilderSize = Mathf.Clamp(ghost.GetRecursiveBounds().size.magnitude * 1.1f, 1f, 50f * TA.TASettings.gizmoSize);

                positionalGizmo.transform.localScale = Vector3.one * BuilderSize;
                rotationalGizmo.transform.localScale = Vector3.one * BuilderSize;
            }
            // Fixed size based gizmo
            else if (TA.TASettings.gizmoStyle == 0) 
            {
                BuilderSize = TA.TASettings.gizmoSize;
                positionalGizmo.transform.localScale = Vector3.one * BuilderSize;
                rotationalGizmo.transform.localScale = Vector3.one * BuilderSize;
            }
        }

        // Screen size based gizmo, run every frame in OnBuilderUpdate if enabled
        public void UpdateGizmoSize()
        {
            if (TA.TASettings.gizmoStyle == 1)
            {
                // Get the distance between the gizmo position and the camera
                float screenDistance = Vector3.Distance(positionalGizmo.transform.position, Camera.main.transform.position);

                // Calculate the gizmo size based on the screen size and the distance from the camera
                BuilderSize = Mathf.Clamp((Screen.height / 30000f) * screenDistance * TA.TASettings.gizmoSize, 0.2f, 50f);

                // Set the gizmo size
                positionalGizmo.transform.localScale = Vector3.one * BuilderSize;
                rotationalGizmo.transform.localScale = Vector3.one * BuilderSize;
            }
        }

        public void GetBuildTransform(out Vector3 wsPos, out Quaternion wsRot)
        {
            wsPos = _gizmoHelperChild.transform.position;
            wsRot = _gizmoHelperChild.transform.rotation;
        }

        public void GetGizmoTransform(out Vector3 wsPos, out Quaternion wsRot)
        {
            wsPos = positionalGizmo.transform.position;
            wsRot = rotationalGizmo.transform.rotation;
        }

        public void SetGizmoTransform(Vector3 position, Quaternion rotation)
        {
            positionalGizmo.transform.position = position;
            rotationalGizmo.transform.rotation = rotation;
            _gizmoHelperParent.transform.position = positionalGizmo.transform.position;
            _gizmoHelperParent.transform.rotation = rotationalGizmo.transform.rotation;
            UpdateBuilderGridToGizmo();
        }

        public void SetGizmoMoving(bool moving)
        {
            GizmoControlsBeingUsed = moving;
        }

        public void SetGizmoEnabled(bool setTo, bool setGizmoCurrentState = false)
        {
            Debug.Log("TA: SetGizmoEnabled " + setTo.ToString());
            GizmoEnabled = setTo;
            GizmoCurrentState = setGizmoCurrentState;

            if (!GizmoEnabled)
            {
                _gizmoHelperChild.transform.localPosition = Vector3.zero;
                _gizmoHelperChild.transform.localRotation = Quaternion.identity;
                IsEditingOrigin = false;
            }

            StartCoroutine(WaitToAllowToggle());
            UpdateUIContent();

            if (setTo)
            {
                UpdateBuilderGridToGizmo();
            }
            else
            {
                ClearBuilderGrid();
            }
        }

        public void ResetGizmoRotation()
        {


            Vector3 lastFullPosition = _gizmoHelperChild.transform.position;
            Quaternion lastFullRotation = _gizmoHelperChild.transform.rotation;

            _gizmoHelperParent.transform.rotation = Quaternion.identity;

            if (IsEditingOrigin)
            {
                _gizmoHelperChild.transform.position = lastFullPosition;
                _gizmoHelperChild.transform.rotation = lastFullRotation;
            }

            positionalGizmo.transform.position = _gizmoHelperParent.transform.position;
            rotationalGizmo.transform.rotation = _gizmoHelperParent.transform.rotation;

            UpdateGizmoTransforms();
            UpdateBuilderGridToGizmo();

        }

        public void ResetPivot()
        {

            Vector3 cachedBuilderPos = _gizmoHelperChild.transform.position;
            Quaternion cachedBuilderRot = _gizmoHelperChild.transform.rotation;

            _gizmoHelperParent.transform.position = cachedBuilderPos;
            _gizmoHelperParent.transform.rotation = cachedBuilderRot;
            positionalGizmo.transform.position = cachedBuilderPos;
            rotationalGizmo.transform.rotation = cachedBuilderRot;
            _gizmoHelperChild.transform.localPosition = Vector3.zero;
            _gizmoHelperChild.transform.localRotation = Quaternion.identity;

            IsEditingOrigin = false;

            UpdateGizmoTransforms();
            UpdateUIContent();

        }

        public void ToggleCoordDisplay()
        {
            _coordDisplayVisible = !_coordDisplayVisible;
            UpdateUIContent();
        }

        public void ToggleGizmoTool()
        {
            switch (CurrentTool)
            {
                case Tool.MOVE:
                    CurrentTool = Tool.ROTATE;
                    break;
                case Tool.ROTATE:
                    CurrentTool = Tool.MOVE;
                    break;
            }

            UpdateUIContent();

        }

        public void ToggleGizmoSpace()
        {
            switch (CurrentSpace)
            {
                case ToolSpace.LOCAL:
                    CurrentSpace = ToolSpace.GLOBAL;
                    break;
                case ToolSpace.GLOBAL:
                    CurrentSpace = ToolSpace.LOCAL;
                    break;
            }

            UpdateUIContent();

        }

        public void TogglePivotEdit()
        {
            IsEditingOrigin = !IsEditingOrigin;
            UpdateUIContent();
        }

        public void UpdateUIContent()
        {

            // Pivot editing update
            UIBuildButton.button.interactable = !IsEditingOrigin;

            UIPivotEdit.icon.sprite = (IsEditingOrigin) ? TA.TickSprite : TA.OriginMoveSprite;
            UIPivotEdit.tooltip.text = (IsEditingOrigin) ? "Keep pivot changes" : "Change pivot";

            // Icon updates
            UIToolButton.icon.sprite = (CurrentTool == Tool.MOVE) ? TA.RotateSprite : TA.MoveSprite;
            UIToolButton.tooltip.text = (CurrentTool == Tool.MOVE) ? "Rotate tool" : "Move tool";
            UISpaceButton.icon.sprite = (CurrentSpace == ToolSpace.LOCAL) ? TA.GlobalSprite : TA.LocalSprite;
            UISpaceButton.tooltip.text = (CurrentSpace == ToolSpace.LOCAL) ? "Global space" : "Local space";

            // Main update
            bool showUI = CurrentBuilder != null && GizmoEnabled;
            UITransform.SetActive(showUI);

            if (_coordDisplayToggleGO != null)
            {
                _coordDisplayToggleGO.SetActive(showUI);
                if (showUI)
                    UICoordDisplayToggle.icon.sprite = _coordDisplayVisible ? _coordToggleCloseSprite : _coordToggleOpenSprite;
            }

            if (_coordDisplayGO != null)
            {
                _coordDisplayGO.SetActive(showUI && _coordDisplayVisible);
                if (showUI && _coordDisplayVisible && _coordDisplay != null)
                {
                    if (CurrentTool == Tool.MOVE)
                        _coordDisplay.ShowPositionMode();
                    else
                        _coordDisplay.ShowRotationMode();
                }
            }
        }

        public void UpdateUIPosition()
        {

            if (_cachedMaincam == null)
            {
                return;
            }

            // left and up relative to cam from position of gizmo, with width calced
            Vector3 uiScreenPos = _cachedMaincam.WorldToScreenPoint(
                positionalGizmo.transform.position +
                _cachedMaincam.transform.rotation * (new Vector3(0.9f, 0.9f, 0) * BuilderSize));

            UITransform.transform.position = uiScreenPos;

            if (_coordDisplayToggleGO != null)
            {
                _coordDisplayToggleGO.transform.position = uiScreenPos + new Vector3(78f, -53f, 0f);
            }

            if (_coordDisplayGO != null)
            {
                _coordDisplayGO.transform.position = uiScreenPos + new Vector3(130f, -150f, 0f);
            }

        }

        public void UpdateBuilderGridToGizmo()
        {
            GameController.Instance.terrainGridProjector.transform.position = positionalGizmo.transform.position;
            GameController.Instance.terrainGridBuilderProjector.transform.position = positionalGizmo.transform.position;
            GameController.Instance.terrainGridProjector.transform.rotation = Quaternion.LookRotation(Vector3.down, rotationalGizmo.transform.forward);
            GameController.Instance.terrainGridBuilderProjector.transform.rotation = Quaternion.LookRotation(Vector3.down, rotationalGizmo.transform.forward);
        }

        private void UpdateGizmoTransforms()
        {
            // Keep both gizmos sync'd with eachother
            rotationalGizmo.UpdatePosition(positionalGizmo.transform.position);
            positionalGizmo.UpdateRotation(rotationalGizmo.transform.rotation);

            _gizmoHelperParent.transform.position = positionalGizmo.transform.position;
            _gizmoHelperParent.transform.rotation = rotationalGizmo.transform.rotation;
        }

        public void ClearBuilderGrid()
        {
            GameController.Instance.terrainGridProjector.transform.position = Vector3.zero;
            GameController.Instance.terrainGridBuilderProjector.transform.position = Vector3.zero;
            GameController.Instance.terrainGridProjector.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            GameController.Instance.terrainGridBuilderProjector.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }

        public void SetGizmoCamera()
        {
            if (_cachedMaincam != null)
            {
                // Configure gizmo camera settings
                if (TA.TASettings.gizmoRenderBehaviourString == 0)
                {
                    gizmoCamera.cullingMask = 1 << 28;
                    // Set depth to normal
                    gizmoCamera.depth = _cachedMaincam.depth + 1000;
                }
                else
                {
                    // Don't question this
                    gizmoCamera.cullingMask = 1 << 31;
                    // Set depth to render on top
                    gizmoCamera.depth = _cachedMaincam.depth - 1;
                }
            }
            else
            {
                return;
            }
        }

        public void OnEnable() {

            Debug.Log("TA: Enabling TAController");

            // Spawn gizmo offset helpers (i honestly tried to do this without them and I suffered)
            _gizmoHelperParent = new GameObject().GetComponent<Transform>();
            _gizmoHelperParent.SetParent(this.transform, false);
            _gizmoHelperParent.transform.localPosition = Vector3.zero;
            _gizmoHelperParent.transform.localRotation = Quaternion.identity;

            _gizmoHelperChild = new GameObject().GetComponent<Transform>();
            _gizmoHelperChild.SetParent(_gizmoHelperParent, false);
            _gizmoHelperParent.transform.localPosition = Vector3.zero;
            _gizmoHelperParent.transform.localRotation = Quaternion.identity;


            // Positional Gizmo
            positionalGizmo = (new GameObject()).AddComponent<PositionalGizmo>();
            positionalGizmo.gameObject.name = "Positional Gizmo";
            positionalGizmo.SpawnIn = TA.ArrowGO;
            positionalGizmo.OnCreate();

            // Rotational Gizmo
            rotationalGizmo = (new GameObject()).AddComponent<RotationalGizmo>();
            rotationalGizmo.gameObject.name = "Rotational Gizmo";
            rotationalGizmo.SpawnIn = TA.RingGO;
            rotationalGizmo.OnCreate();

            positionalGizmo.OnDuringDrag.AddListener(a => SetGizmoMoving(true));
            positionalGizmo.OnEndDrag.AddListener(a => StartCoroutine(WaitToSetMovingOff()));

            rotationalGizmo.OnDuringDrag.AddListener(a => SetGizmoMoving(true));
            rotationalGizmo.OnEndDrag.AddListener(a => StartCoroutine(WaitToSetMovingOff()));

            // Ui window time.
            TA.UiHolder.SetActive(false);
            UITransform = Instantiate(TA.UiHolder, Parkitect.UI.UIWorldOverlayController.Instance.transform);

            // Temp vars
            Button b;
            Image i;
            UITooltip t;

            b = UITransform.transform.Find("Gizmo_Button").GetComponent<Button>();
            i = b.transform.Find("Image").GetComponent<Image>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            UIToolButton = new UIButton(b, i, t);

            b = UITransform.transform.Find("Space_Button").GetComponent<Button>();
            i = b.transform.Find("Image").GetComponent<Image>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            UISpaceButton = new UIButton(b, i, t);

            b = UITransform.transform.Find("Build_Button").GetComponent<Button>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            t.text = "Build";
            UIBuildButton = new UIButton(b, null, t);

            b = UITransform.transform.Find("Reset_Button").GetComponent<Button>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            t.text = "Reset rotation";
            UIResetRotationButton = new UIButton(b, null, t);

            b = UITransform.transform.Find("Cancel_Button").GetComponent<Button>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            t.text = "Use basic move";
            UIGizmoToggleButton = new UIButton(b, null, t);

            b = UITransform.transform.Find("Pivot_Set_Button").GetComponent<Button>();
            i = b.transform.Find("Image").GetComponent<Image>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            UIPivotEdit = new UIButton(b, i, t);

            b = UITransform.transform.Find("Pivot_Cancel_Button").GetComponent<Button>();
            t = b.gameObject.AddComponent<UITooltip>();
            t.context = "Transform Anarchy";
            t.text = "Cancel pivot changes";
            UIPivotCancel = new UIButton(b, null, t);

            UIToolButton.button.onClick.AddListener(ToggleGizmoTool);
            UISpaceButton.button.onClick.AddListener(ToggleGizmoSpace);
            UIPivotEdit.button.onClick.AddListener(TogglePivotEdit);
            UIPivotCancel.button.onClick.AddListener(ResetPivot);

            UIBuildButton.button.onClick.AddListener(() => ForceBuildThisFrame = true && !IsEditingOrigin);
            UIGizmoToggleButton.button.onClick.AddListener(() => SetGizmoEnabled(!GizmoEnabled));
            UIResetRotationButton.button.onClick.AddListener(ResetGizmoRotation);

            // Coordinate display panel (sibling of UITransform so it can be positioned independently)
            _coordDisplayGO = new GameObject("TA_CoordDisplay");
            _coordDisplayGO.transform.SetParent(Parkitect.UI.UIWorldOverlayController.Instance.transform, false);
            _coordDisplay = _coordDisplayGO.AddComponent<TACoordDisplay>();
            _coordDisplay.Initialize();
            _coordDisplay.OnPositionCommit += OnCoordPositionCommit;
            _coordDisplay.OnRotationCommit += OnCoordRotationCommit;
            _coordDisplayGO.SetActive(false);

            // Coord display toggle button
            var openTex = TA.GetLooseTexture(TA.LOOSE_TEXTURES.NUMERIC_ENTRY_OPEN_BUTTON);
            _coordToggleOpenSprite = Sprite.Create(openTex, new Rect(0, 0, openTex.width, openTex.height), new Vector2(0.5f, 0.5f));
            var closeTex = TA.GetLooseTexture(TA.LOOSE_TEXTURES.NUMERIC_ENTRY_CLOSE_BUTTON);
            _coordToggleCloseSprite = Sprite.Create(closeTex, new Rect(0, 0, closeTex.width, closeTex.height), new Vector2(0.5f, 0.5f));

            _coordDisplayToggleGO = new GameObject("TA_CoordDisplayToggle");
            _coordDisplayToggleGO.transform.SetParent(Parkitect.UI.UIWorldOverlayController.Instance.transform, false);

            RectTransform toggleRT = _coordDisplayToggleGO.AddComponent<RectTransform>();
            toggleRT.sizeDelta = new Vector2(30f, 30f);

            Image toggleBg = _coordDisplayToggleGO.AddComponent<Image>();
            toggleBg.sprite = TA.InfoPipCircleSprite;
            toggleBg.color = new Color(0.65f, 1f, 1f, 0.45f);

            Button toggleBtn = _coordDisplayToggleGO.AddComponent<Button>();
            toggleBtn.targetGraphic = toggleBg;

            GameObject toggleIconGO = new GameObject("Image");
            toggleIconGO.transform.SetParent(_coordDisplayToggleGO.transform, false);
            RectTransform toggleIconRT = toggleIconGO.AddComponent<RectTransform>();
            toggleIconRT.anchorMin = Vector2.zero;
            toggleIconRT.anchorMax = Vector2.one;
            toggleIconRT.sizeDelta = Vector2.zero;
            toggleIconRT.anchoredPosition = Vector2.zero;
            Image toggleIcon = toggleIconGO.AddComponent<Image>();
            toggleIcon.sprite = _coordToggleOpenSprite;

            UITooltip toggleTip = _coordDisplayToggleGO.AddComponent<UITooltip>();
            toggleTip.context = "Transform Anarchy";
            toggleTip.text = "Toggle numeric entry";

            UICoordDisplayToggle = new UIButton(toggleBtn, toggleIcon, toggleTip);
            toggleBtn.onClick.AddListener(ToggleCoordDisplay);
            _coordDisplayToggleGO.SetActive(false);

            Debug.Log("TA: transform Anarchy initialized");

            UpdateUIContent();

        }

        public void Update()
        {
            // Tick the picker tool while it is active
            if (_editPipetteTool != null && GameController.Instance.isActiveMouseTool(_editPipetteTool))
            {
                _editPipetteTool.tick();
            }

            // Start picker when toggleGizmoOn is pressed and no builder is active
            if (CurrentBuilder == null && _editPipetteTool == null
                && !_alreadyToggledThisFrame
                && InputManager.getKeyDown("toggleGizmoOn")
                && !UIUtility.isInputFieldFocused())
            {
                StartEditPickerMode();
            }
        }

        private void StartEditPickerMode()
        {
            Debug.Log("TA: Starting edit picker mode");
            _editPipetteTool = new TAObjectPipetteTool();
            _editPipetteTool.OnObjectSelected += OnEditPickerObjectSelected;
            _editPipetteTool.OnRemoved += () => { _editPipetteTool = null; };
            GameController.Instance.enableMouseTool(_editPipetteTool);
        }

        private void OnEditPickerObjectSelected(BuildableObject buildableObject)
        {
            GameController.Instance.removeMouseTool(_editPipetteTool);
            // _editPipetteTool is nulled by the OnRemoved handler

            Deco deco = buildableObject as Deco;
            if (deco == null) return;

            Debug.Log("TA: Edit picker selected: " + deco.getReferenceName());
            _editTarget = deco;
            deco.gameObject.SetActive(false);
            
            // Position the gizmo at the original object before the builder is created
            UseTransformFromLastBuilder = true;
            PipetteWaitForMouseUp = true;
            SetGizmoTransform(deco.logicTransform.position, deco.logicTransform.rotation);

            // Create a builder from the clean prefab so we don't mutate the placed object
            BuildableObject prefab = ScriptableSingleton<AssetManager>.Instance.getPrefab<BuildableObject>(deco.getReferenceName());
            _editBuilder = prefab.instantiateBuilder();
            _editBuilder.snapRotation(deco.logicTransform.rotation * Vector3.forward);
            _editBuilder.setFixedGhostHeightIfRaised(deco.logicTransform.position);
            _editBuilder.copySettingsFrom(deco);

            _editBuilder.OnBuildTriggered += OnEditBuilderBuildTriggered;
            _editBuilder.OnCancelled += OnEditBuilderCancelled;
        }

        private void OnEditBuilderBuildTriggered()
        {
            Debug.Log("TA: OnEditBuilderBuildTriggered");
            if (_editTarget != null)
            {
                Debug.Log("TA: Has _editTarget");
                UnityEngine.Object.Destroy(_editTarget.gameObject);
                _editTarget = null;
            }
            // Detach so subsequent placements (DecoBuilder stays open) don't re-fire
            if (_editBuilder != null)
                _editBuilder.OnBuildTriggered -= OnEditBuilderBuildTriggered;
        }

        private void OnEditBuilderCancelled()
        {
            Debug.Log("TA: OnEditBuilderCancelled");
            if (_editTarget != null)
            {
                _editTarget.gameObject.SetActive(true);
                _editTarget = null;
            }
            _editBuilder = null;
        }

        // basically wait two frames in order to make sure
        public IEnumerator StoppedBuildingWatch()
        {
            yield return null;
            yield return null;
            UseTransformFromLastBuilder = false;
        }

        public IEnumerator WaitToSetMovingOff()
        {
            yield return null;
            SetGizmoMoving(false);
        }

        public IEnumerator WaitToAllowToggle()
        {
            _alreadyToggledThisFrame = true;
            yield return null;
            _alreadyToggledThisFrame = false;
        }

        private void OnCoordPositionCommit(Vector3 newPos)
        {
            if (CurrentSpace == ToolSpace.LOCAL)
                newPos = rotationalGizmo.transform.rotation * newPos;
            SetGizmoTransform(newPos, rotationalGizmo.transform.rotation);
        }

        private void OnCoordRotationCommit(Vector3 newEuler)
        {
            SetGizmoTransform(positionalGizmo.transform.position, Quaternion.Euler(newEuler));
        }

        public void OnDisable()
        {
            Debug.Log("TA: Controller.OnDisable");

            UIToolButton.button.onClick.RemoveListener(ToggleGizmoTool);
            UISpaceButton.button.onClick.RemoveListener(ToggleGizmoSpace);
            UIPivotEdit.button.onClick.RemoveListener(TogglePivotEdit);
            UIPivotCancel.button.onClick.RemoveListener(ResetPivot);

            UIBuildButton.button.onClick.RemoveListener(() => ForceBuildThisFrame = true && !IsEditingOrigin);
            UIGizmoToggleButton.button.onClick.RemoveListener(() => SetGizmoEnabled(!GizmoEnabled));
            UIResetRotationButton.button.onClick.RemoveListener(ResetGizmoRotation);

            if (_coordDisplay != null)
            {
                _coordDisplay.OnPositionCommit -= OnCoordPositionCommit;
                _coordDisplay.OnRotationCommit -= OnCoordRotationCommit;
                _coordDisplay = null;
            }
            if (_coordDisplayGO != null)
            {
                Destroy(_coordDisplayGO);
                _coordDisplayGO = null;
            }
            if (UICoordDisplayToggle.button != null)
                UICoordDisplayToggle.button.onClick.RemoveListener(ToggleCoordDisplay);
            if (_coordDisplayToggleGO != null)
            {
                Destroy(_coordDisplayToggleGO);
                _coordDisplayToggleGO = null;
            }
            _coordToggleOpenSprite = null;
            _coordToggleCloseSprite = null;

            Destroy(UITransform);

            positionalGizmo.OnDuringDrag.RemoveListener(a => SetGizmoMoving(true));
            positionalGizmo.OnEndDrag.RemoveListener(a => StartCoroutine(WaitToSetMovingOff()));
            rotationalGizmo.OnDuringDrag.RemoveListener(a => SetGizmoMoving(true));
            rotationalGizmo.OnEndDrag.RemoveListener(a => StartCoroutine(WaitToSetMovingOff()));

            Destroy(positionalGizmo.gameObject);
            Destroy(rotationalGizmo.gameObject);

            ClearBuilderGrid();

            // Clear bit
            if (_cachedMaincam == null) return;
            _cachedMaincam.cullingMask = _cachedMaincam.cullingMask & (~Gizmo<PositionalGizmoComponent>.LAYER_MASK);
        }

        public void OnBeforeInit()
        {

            if (_cachedMaincam == null)
            {
                // Cache the main camera
                _cachedMaincam = Camera.main;

                if (_cachedMaincam != null)
                {
                    _cachedMaincam.cullingMask = _cachedMaincam.cullingMask | Gizmo<PositionalGizmoComponent>.LAYER_MASK;

                    // Create a new camera for gizmo rendering
                    GameObject gizmoCameraObject = new GameObject("GizmoCamera");
                    gizmoCamera = gizmoCameraObject.AddComponent<Camera>();

                    // Set gizmo camera as a child of the main camera
                    gizmoCamera.transform.parent = _cachedMaincam.transform;

                    // Copy relevant properties from the main camera
                    gizmoCamera.CopyFrom(_cachedMaincam);

                    // Set to only depth buffer
                    gizmoCamera.clearFlags = CameraClearFlags.Depth;

                    // Set the layer mask for gizmo rendering
                    gizmoCamera.cullingMask = 1 << 28;

                }
                else
                {
                    return;
                }
            }

            if (InputManager.getKeyDown("toggleGizmoOn") && CurrentBuilder != null && !_alreadyToggledThisFrame && !UIUtility.isInputFieldFocused())
            {
                Debug.Log("TA: Toggled building mode");
                SetGizmoEnabled(!GizmoEnabled);
                _dontUpdateGrid = true;
                GridSubdivision = GameController.Instance.terrainGridBuilderProjector.gridSubdivision;
            }
            else if (CurrentBuilder == null)
            {
                OnBuilderDisable();
            }
            else if (!GizmoEnabled)
            {
                positionalGizmo.SetActiveGizmo(false);
                rotationalGizmo.SetActiveGizmo(false);
            }
        }

        public void OnBuilderUpdate()
        {

            bool gridMode = InputManager.getKey("BuildingSnapToGrid");
            bool updateGizmo = ShouldSnap != gridMode || _dontUpdateGrid;
            ShouldSnap = gridMode;

            if (gridMode && !_dontUpdateGrid)
            {

                float currentSub = GridSubdivision;

                if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    GridSubdivision = 10f;
                }
                else
                {
                    for (int i = 1; i <= 9; i++)
                    {
                        if (Input.GetKeyDown(i.ToString() ?? "") || Input.GetKeyDown("[" + i.ToString() + "]"))
                        {
                            GridSubdivision = (float)i;
                        }
                    }
                }

                if (currentSub != GridSubdivision)
                {
                    updateGizmo = true;
                }
            }
            else
            {
                ClearBuilderGrid();
            }

            if (updateGizmo)
            {
                GameController.Instance.terrainGridBuilderProjector.setGridSubdivision(GridSubdivision);
                UpdateBuilderGridToGizmo();
            }

            if (_dontUpdateGrid)
            {
                _dontUpdateGrid = false;
            }

            // Reimplement size hotkeys directly
            if (InputManager.getKey("BuildingIncreaseObjectSize") && !UIUtility.isInputFieldFocused())
            {
                BuilderFunctions.changeSize.Invoke(CurrentBuilder, new object[] { 0.01f });


            }
            else if (InputManager.getKey("BuildingDecreaseObjectSize") && !UIUtility.isInputFieldFocused())
            {
                BuilderFunctions.changeSize.Invoke(CurrentBuilder, new object[] { -0.01f });
            }

            // Keybinds
            if (InputManager.getKeyDown("toggleGizmoSpace") && !gridMode && !UIUtility.isInputFieldFocused())
            {
                ToggleGizmoSpace();
            }
            if (InputManager.getKeyDown("toggleGizmoTool") && !gridMode && !UIUtility.isInputFieldFocused())
            {
                ToggleGizmoTool();
            }
            if (InputManager.getKeyDown("resetGizmoTool") && !gridMode && !UIUtility.isInputFieldFocused())
            {
                ResetGizmoRotation();
            }
            if (InputManager.getKeyDown("togglePivotEdit") && !gridMode && !UIUtility.isInputFieldFocused())
            {
                TogglePivotEdit();
            }
            if (InputManager.getKeyDown("cancelPivotEdit") && !gridMode && !UIUtility.isInputFieldFocused())
            {
                ResetPivot();
            }

            // Toggle tools based on the current state
            if (CurrentTool == Tool.MOVE && GizmoEnabled)
            {
                positionalGizmo.SetActiveGizmo(true);
                rotationalGizmo.SetActiveGizmo(false);
                positionalGizmo.CurrentRotationMode = CurrentSpace;
                rotationalGizmo.CurrentRotationMode = CurrentSpace;

                // cache position
                Vector3 currentPosition = positionalGizmo.transform.position;

                positionalGizmo.OnDragCheck();

                if (IsEditingOrigin)
                {
                    _gizmoHelperParent.transform.position = positionalGizmo.transform.position;
                    _gizmoHelperChild.transform.position -= (positionalGizmo.transform.position - currentPosition);
                }
            }
            else if (CurrentTool == Tool.ROTATE && GizmoEnabled)
            {
                positionalGizmo.SetActiveGizmo(false);
                rotationalGizmo.SetActiveGizmo(true);
                positionalGizmo.CurrentRotationMode = CurrentSpace;
                rotationalGizmo.CurrentRotationMode = CurrentSpace;

                Quaternion lastFullRotation = _gizmoHelperChild.transform.rotation;
                Vector3 lastFullPosition = _gizmoHelperChild.transform.position;

                rotationalGizmo.OnDragCheck();

                if (IsEditingOrigin)
                {
                    _gizmoHelperParent.transform.rotation = rotationalGizmo.transform.rotation;
                    _gizmoHelperChild.transform.position = lastFullPosition;
                    _gizmoHelperChild.transform.rotation = lastFullRotation;
                }
            }

            // Update gizmo size
            UpdateGizmoSize();

            // Sync gizmos
            UpdateGizmoTransforms();

            // Update UI position
            UpdateUIPosition();

            // Feed current gizmo transform into the coordinate display
            if (_coordDisplay != null && _coordDisplayGO != null && _coordDisplayGO.activeSelf)
            {
                if (CurrentSpace == ToolSpace.LOCAL)
                {
                    var rot = rotationalGizmo.transform.rotation;
                    _coordDisplay.UpdatePosition(Quaternion.Inverse(rot) * positionalGizmo.transform.position);
                }
                else
                {
                    _coordDisplay.UpdatePosition(positionalGizmo.transform.position);
                }
                _coordDisplay.UpdateRotation(rotationalGizmo.transform.rotation.eulerAngles);
            }

        }
    }
}
