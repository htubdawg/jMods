using UnityEngine;

namespace HalfGripSlapPack
{
    public sealed class TurnSignalStalk : Grabbable
    {
        private TurnSignalState signal;
        private Grabber hand;
        private Camera drivingCamera;
        private const float TravelDegrees = 14f;
        private float startHandAngle, startPosition;
        internal bool CanInteract => hand && hand.isActiveAndEnabled && hand.viewCamera && hand.viewCamera.isActiveAndEnabled && drivingCamera && drivingCamera.isActiveAndEnabled &&
            Application.isFocused && !GameInput.Muted && Time.timeScale > 0;

        protected override void Awake() { }

        internal void Initialize(TurnSignalState state, Grabber grabber, Camera worldCamera, Vector3 plane, float length)
        {
            signal = state; hand = grabber;
            drivingCamera = worldCamera;
            movementPlane = plane;
            keepPhysicalCursorClose = true;
            keepPhysicalCursorRange = Mathf.Max(0.6f, length * 0.16f);
            handIndicatorCameraOffset = 0.2f;
            releaseCursorBlend = 0;
            ResetRestPose(transform.localPosition, transform.localRotation);
        }

        internal void CheckInteraction()
        {
            if (!enabled && CanInteract && hand.Held != this) enabled = true;
            canBeGrabbed = CanInteract;
            if (IsGrabbed && !CanInteract) CancelHold();
        }

        protected override void OnGrab()
        {
            signal.BeginHold();
            startHandAngle = HandAngle();
            startPosition = signal.StalkPosition;
        }

        protected override void OnHandMoved()
        {
            if (!CanInteract) { CancelHold(); return; }
            Vector2 offset = constrainedPoint - position;
            if (offset.sqrMagnitude < 0.01f) return;
            // Follow the physical lever arc, including its upward rest angle. Mapping raw Y
            // displacement would give different sensitivity at the upper and lower detents.
            signal.Move(startPosition - Mathf.DeltaAngle(startHandAngle, HandAngle()) / TravelDegrees);
            RefreshPose();
        }

        private float HandAngle()
        {
            Vector2 offset = constrainedPoint - position;
            return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        protected override void OnRelease() { if (signal != null) { signal.Release(); RefreshPose(); } }

        internal void RefreshPose()
        {
            if (signal == null) return;
            // The stalk extends left from its pivot: positive roll moves its tip down.
            angle = -signal.StalkPosition * TravelDegrees * Mathf.Deg2Rad;
            base.ApplyPose();
        }

        protected override void Update() { if (IsGrabbed && (!CanInteract || !GameInput.Hand(0))) CancelHold(); }
        protected override void StepPhysics(float dt) { }
        protected override void StepDirect(float dt) { }
        protected override void ApplyPose() { RefreshPose(); }
        private void CancelHold() { blockRelease = false; Release(); enabled = false; }
        private void OnApplicationFocus(bool focused) { if (!focused && IsGrabbed) CancelHold(); }
        private void OnApplicationPause(bool paused) { if (paused && IsGrabbed) CancelHold(); }
        private void OnDisable() { if (IsGrabbed) Release(); }
    }
}
