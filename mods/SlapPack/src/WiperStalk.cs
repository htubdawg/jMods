using UnityEngine;

namespace HalfGripSlapPack
{
    public sealed class WiperStalk : Grabbable
    {
        private WiperState state;
        private Grabber hand;
        private Camera drivingCamera;
        private MeshRenderer[] settings;
        private TurnSignalAudio detentAudio;
        private readonly WiperKnobRotation knob = new WiperKnobRotation();
        private Vector2 previousKnobHand;
        internal bool RotatingKnob { get; private set; }
        private float startAngle, startPull;
        private const float TravelDegrees = 16;
        private bool CanInteract => hand && hand.isActiveAndEnabled && hand.viewCamera && hand.viewCamera.isActiveAndEnabled &&
            drivingCamera && drivingCamera.isActiveAndEnabled && Application.isFocused && !GameInput.Muted && Time.timeScale > 0;

        protected override void Awake() { }

        internal void Initialize(WiperState value, Grabber grabber, Camera camera, Vector3 normal, float length, MeshRenderer[] artwork, TurnSignalAudio clicks)
        {
            state = value; hand = grabber; drivingCamera = camera; settings = artwork;
            detentAudio = clicks;
            movementPlane = normal;
            keepPhysicalCursorClose = true;
            keepPhysicalCursorRange = Mathf.Max(0.6f, length * 0.16f);
            handIndicatorCameraOffset = 0.2f;
            releaseCursorBlend = 0;
            ResetRestPose(transform.localPosition, transform.localRotation);
            RefreshPose();
        }

        internal void CheckInteraction()
        {
            if (!enabled && CanInteract && hand.Held != this) enabled = true;
            canBeGrabbed = CanInteract;
            if (IsGrabbed && !CanInteract) CancelHold();
        }

        protected override void OnGrab()
        {
            startAngle = HandAngle(); startPull = state.Pull;
            if (RotatingKnob)
            {
                previousKnobHand = constrainedPoint; knob.Begin(state.Speed);
                // The visible hand remains at the sleeve while input travels through detents.
                // Native releaseCursorBlend=0 returns the cursor to the grip on release.
                keepPhysicalCursorClose = false;
            }
        }
        protected override void OnHandMoved()
        {
            if (!CanInteract) { CancelHold(); return; }
            if (RotatingKnob)
            {
                Vector2 delta = constrainedPoint - previousKnobHand; previousKnobHand = constrainedPoint;
                if (Mathf.Abs(delta.y) < 0.00001f) return;
                WiperSpeed previous = knob.Setting;
                knob.Move(delta.y); state.SetSpeed(knob.Setting);
                if (previous != knob.Setting)
                {
                    if (detentAudio != null) detentAudio.Click(0.25f);
                    RefreshPose();
                }
                return;
            }
            if ((constrainedPoint - position).sqrMagnitude < 0.01f) return;
            state.SetPull(startPull - Mathf.DeltaAngle(startAngle, HandAngle()) / TravelDegrees);
            RefreshPose();
        }

        private float HandAngle()
        {
            Vector2 offset = constrainedPoint - position;
            return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        protected override void OnRightClick()
        {
            if (!CanInteract || hand.Held || !hand.Hovered || hand.Hovered.GetComponentInParent<WiperStalk>() != this) return;
            RotatingKnob = true;
            WiperKnobGrab.Begin(hand);
            if (hand.Held != this) RotatingKnob = false;
        }

        protected override void OnRelease()
        {
            RotatingKnob = false; keepPhysicalCursorClose = true; knob.Release();
            if (state != null) { state.Release(); RefreshPose(); }
        }

        internal void RefreshPose()
        {
            if (state == null) return;
            angle = -state.Pull * TravelDegrees * Mathf.Deg2Rad;
            base.ApplyPose();
            for (int i = 0; i < settings.Length; i++)
                if (settings[i])
                {
                    settings[i].enabled = i == (int)state.Speed;
                }
        }

        protected override void Update() { if (IsGrabbed && (!CanInteract || !GameInput.Hand(RotatingKnob ? 1 : 0))) CancelHold(); }
        protected override void StepPhysics(float dt) { }
        protected override void StepDirect(float dt) { }
        protected override void ApplyPose() { RefreshPose(); }
        private void CancelHold() { blockRelease = false; Release(); enabled = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) { if (IsGrabbed) CancelHold(); if (detentAudio != null) detentAudio.Stop(); } }
        private void OnApplicationPause(bool paused) { if (paused) { if (IsGrabbed) CancelHold(); if (detentAudio != null) detentAudio.Stop(); } }
        private void OnDisable() { if (IsGrabbed) Release(); if (detentAudio != null) detentAudio.Stop(); }
    }
}
