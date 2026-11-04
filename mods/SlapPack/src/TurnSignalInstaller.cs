using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HalfGripTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    public sealed class TurnSignalInstaller : MonoBehaviour
    {
        private ModContext context;
        private ConfigEntry<bool> enabledSignals;
        private ConfigEntry<bool> clickSound;
        private ConfigEntry<float> clickVolume;
        private ConfigEntry<int> shortFlashes;
        private ConfigEntry<float> halfPeriod, armSteering, centeredSteering, minimumHeading;
        private ConfigEntry<float> stalkExtraLength, stalkUpAngle;
        private CarControllerNew car;
        private CarLink link;
        private HingedObject wheel;
        private TurnSignalStalk stalk;
        private TurnSignalState signal;
        private SignalDashLamp leftArrow, rightArrow;
        private TurnSignalAudio relay;
        private Material amber;
        private List<MeshRenderer> leftLamps = new List<MeshRenderer>(), rightLamps = new List<MeshRenderer>();
        private readonly List<Object> owned = new List<Object>(), lampOwned = new List<Object>();
        private Vector3 previousForward;
        private float nextScan;
        private int lampSignature;

        internal void Initialize(ModContext ctx)
        {
            context = ctx;
            enabledSignals = ctx.Config.Bind("TurnSignals", "Enabled", true, "Enable the turn-signal stalk, cockpit arrows and local exterior lamp overlays.");
            clickSound = ctx.Config.Bind("TurnSignals", "ClickSound", true, "Play synchronized relay tick/tock sounds in the cockpit, through the cabin/SFX mixer.");
            clickVolume = ctx.Config.Bind("TurnSignals", "ClickVolume", 0.35f, new ConfigDescription("Relay click volume, in addition to the game's audio mixer volume.", new AcceptableValueRange<float>(0, 1)));
            shortFlashes = ctx.Config.Bind("TurnSignals", "ShortFlashes", 3, new ConfigDescription("Flashes for a released halfway tap.", new AcceptableValueRange<int>(1, 6)));
            halfPeriod = ctx.Config.Bind("TurnSignals", "BlinkHalfPeriod", 0.4f, new ConfigDescription("Seconds per on/off phase.", new AcceptableValueRange<float>(0.2f, 1f)));
            armSteering = ctx.Config.Bind("TurnSignals", "CancelArmSteering", 12f, new ConfigDescription("Steering degrees into the signaled turn required to arm cancellation.", new AcceptableValueRange<float>(6, 45)));
            centeredSteering = ctx.Config.Bind("TurnSignals", "CancelCenteredSteering", 4f, new ConfigDescription("Steering within this many degrees of center completes cancellation.", new AcceptableValueRange<float>(1, 5)));
            minimumHeading = ctx.Config.Bind("TurnSignals", "CancelMinimumTurn", 15f, new ConfigDescription("Actual heading change into the turn required before cancellation. Parked wheel movement does not cancel.", new AcceptableValueRange<float>(5, 90)));
            stalkExtraLength = ctx.Config.Bind("TurnSignals", "StalkExtraLength", 1.8f, new ConfigDescription("Stalk length beyond the steering wheel radius, in cockpit units. Restart after changing.", new AcceptableValueRange<float>(0.5f, 3f)));
            stalkUpAngle = ctx.Config.Bind("TurnSignals", "StalkUpAngle", 30f, new ConfigDescription("Upward neutral stalk angle in degrees. Restart after changing.", new AcceptableValueRange<float>(0, 45)));
            ctx.OnSceneLoaded(scene => { RemoveSignals(); nextScan = Time.unscaledTime + 0.5f; });
        }

        private void Update()
        {
            if (context == null) return;
            if (!enabledSignals.Value) { RemoveSignals(); return; }
            if (car && link && wheel && stalk && link.sw && link.sw.wheel == wheel)
            {
                stalk.gameObject.SetActive(car.fpvCamera && car.fpvCamera.isActiveAndEnabled);
                stalk.CheckInteraction();
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 1;
                    if (LampSignature() != lampSignature) RebuildLamps();
                }
                return;
            }
            if (owned.Count > 0) RemoveSignals();
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1;
            foreach (CarLink candidate in Object.FindObjectsByType<CarLink>(FindObjectsSortMode.None))
            {
                if (!candidate.cc || !candidate.cc.isPlayer || !candidate.sw || !candidate.sw.wheel) continue;
                Grabber grabber = Object.FindObjectsByType<Grabber>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => g.gameObject.scene == candidate.gameObject.scene && g.viewCamera);
                if (!grabber) continue;
                try { Attach(candidate, grabber); }
                catch (Exception error) { RemoveSignals(); context.Log.LogError("Turn signals could not initialize: " + error); nextScan = float.PositiveInfinity; }
                return;
            }
        }

        private void Attach(CarLink target, Grabber grabber)
        {
            link = target; car = target.cc; wheel = target.sw.wheel;
            signal = new TurnSignalState(shortFlashes.Value, halfPeriod.Value, armSteering.Value, centeredSteering.Value, minimumHeading.Value);
            previousForward = car.transform.forward;
            Transform parent = wheel.transform.parent;
            int layer = wheel.gameObject.layer;
            // Measure the wheel in its parent's coordinates without inheriting wheel rotation.
            float radius = 2.4f;
            SpriteRenderer sprite = wheel.GetComponentsInChildren<SpriteRenderer>(false).FirstOrDefault(s => s.sprite && s.name.IndexOf("SteeringWheel", StringComparison.OrdinalIgnoreCase) >= 0);
            if (sprite)
            {
                Vector3 axis = parent ? parent.InverseTransformVector(sprite.transform.TransformVector(Vector3.right * sprite.sprite.bounds.extents.x)) : sprite.transform.TransformVector(Vector3.right * sprite.sprite.bounds.extents.x);
                radius = Mathf.Clamp(axis.magnitude, 1.5f, 3.5f);
            }
            float length = radius + stalkExtraLength.Value;
            Vector3 normal = wheel.movementPlane.normalized;
            if (normal.sqrMagnitude < 0.5f) normal = Vector3.forward;
            var root = new GameObject("SlapPack.TurnSignalStalk");
            owned.Add(root); root.layer = layer;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = wheel.transform.localPosition + normal * 0.15f;
            root.transform.localRotation = Quaternion.AngleAxis(-stalkUpAngle.Value, normal) * Quaternion.LookRotation(normal, Vector3.up);
            SignalStalkArtwork.Attach(root.transform, length, layer, owned);
            var hit = new GameObject("SignalGrip"); owned.Add(hit); hit.layer = layer;
            hit.transform.SetParent(root.transform, false); hit.transform.localPosition = new Vector3(-length, 0, -0.03f);
            InteriorCircleCollider collider = hit.AddComponent<InteriorCircleCollider>();
            collider.radius = 0.38f; collider.hitBoxExpand = 0.08f;
            InteriorCollider wheelCollider = wheel.attachedCollider ? wheel.attachedCollider : wheel.GetComponentInChildren<InteriorCollider>();
            int wheelPriority = wheelCollider ? wheelCollider.sortingLayer : 0;
            collider.sortingLayer = wheelPriority < int.MaxValue ? wheelPriority + 1 : wheelPriority;
            stalk = root.AddComponent<TurnSignalStalk>();
            stalk.attachedCollider = collider;
            stalk.Initialize(signal, grabber, car.fpvCamera, normal, length);
            const float lampScale = 0.8f, lampSpacing = 0.46f;
            Vector3 dash = IndicatorPosition(target, parent, wheel.transform.localPosition);
            leftArrow = TurnSignalVisuals.Arrow(parent, dash + Vector3.left * (lampSpacing * 0.5f), false, layer, owned, lampScale);
            rightArrow = TurnSignalVisuals.Arrow(parent, dash + Vector3.right * (lampSpacing * 0.5f), true, layer, owned, lampScale);
            TurnSignalVisuals.SetArrow(leftArrow, false, true); TurnSignalVisuals.SetArrow(rightArrow, false, true);
            try { relay = new TurnSignalAudio(transform, grabber.gameObject, owned); }
            catch (Exception error) { context.Log.LogWarning("Relay sound unavailable; turn signals remain active: " + error.Message); }
            amber = TurnSignalVisuals.Material(new Color(1f, 0.4f, 0.015f, 1), owned);
            RebuildLamps();
            ModLog.Info(context.Log, "Turn-signal stalk attached left of steering column. Down=left, up=right; halfway=tap/hold, full=latch. Cockpit arrows and amber lamps active.");
        }

        private static Vector3 IndicatorPosition(CarLink target, Transform parent, Vector3 wheelPosition)
        {
            // Mount the compact pair in the marked dashboard space immediately left of the gear
            // display. Resolve its actual frame rather than positioning relative to the wheel rim.
            const float panelHalfWidth = 0.43f, gap = 0.25f;
            if (target.gearIndicator)
            {
                SpriteRenderer frame = target.gearIndicator.GetComponentInParent<SpriteRenderer>();
                if (frame && frame.sprite)
                {
                    Bounds bounds = frame.sprite.bounds;
                    Vector3 center = frame.transform.TransformPoint(bounds.center);
                    center = parent ? parent.InverseTransformPoint(center) : center;
                    float left = float.PositiveInfinity;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 corner = bounds.center + new Vector3((i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                            (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y, 0);
                        Vector3 point = frame.transform.TransformPoint(corner);
                        if (parent) point = parent.InverseTransformPoint(point);
                        left = Mathf.Min(left, point.x);
                    }
                    return new Vector3(left - gap - panelHalfWidth, center.y, center.z - 0.08f);
                }
                Vector3 text = target.gearIndicator.transform.position;
                if (parent) text = parent.InverseTransformPoint(text);
                return text + new Vector3(-1.4f, 0.07f, -0.03f);
            }
            return wheelPosition + new Vector3(2.15f, 2.1f, 0.3f);
        }

        private void LateUpdate()
        {
            if (!car || signal == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
            float delta = Vector3.SignedAngle(previousForward, forward, Vector3.up);
            previousForward = forward;
            if (Time.timeScale > 0)
            {
                signal.ObserveTurn(car.steeringAngleDeg, delta, car.forwardSpeed > 1f);
                signal.Tick(Time.unscaledDeltaTime);
            }
            if (stalk) stalk.RefreshPose();
            bool left = signal.Direction < 0 && signal.FlashOn, right = signal.Direction > 0 && signal.FlashOn;
            bool cockpit = car.fpvCamera && car.fpvCamera.isActiveAndEnabled;
            TurnSignalVisuals.SetArrow(leftArrow, left, cockpit); TurnSignalVisuals.SetArrow(rightArrow, right, cockpit);
            if (relay != null) relay.Step(signal.Direction, signal.FlashOn, clickSound.Value && cockpit && Time.timeScale > 0, clickVolume.Value);
            foreach (MeshRenderer lamp in leftLamps) if (lamp) lamp.enabled = left;
            foreach (MeshRenderer lamp in rightLamps) if (lamp) lamp.enabled = right;
        }

        private int LampSignature()
        {
            int signature = 17;
            foreach (MeshRenderer renderer in car.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.gameObject.activeInHierarchy && (renderer.name == "BecuriFata" || renderer.name == "BecuriSpate" || renderer.name == "HeadlightsCar2" || renderer.name == "TailLightsCar2"))
                    signature = unchecked(signature * 31 + renderer.GetInstanceID());
            return signature;
        }

        private void RebuildLamps()
        {
            DestroyOwned(lampOwned);
            leftLamps = TurnSignalVisuals.Exterior(car, true, amber, lampOwned);
            rightLamps = TurnSignalVisuals.Exterior(car, false, amber, lampOwned);
            lampSignature = LampSignature();
            if (leftLamps.Count < 2 || rightLamps.Count < 2)
                context.Log.LogWarning("Some exterior signal lamp locations were not found for this car body; cockpit indicators remain available.");
        }

        internal void RemoveSignals()
        {
            if (stalk) stalk.gameObject.SetActive(false);
            DestroyOwned(lampOwned); DestroyOwned(owned);
            leftLamps.Clear(); rightLamps.Clear();
            car = null; link = null; wheel = null; stalk = null; signal = null; leftArrow = null; rightArrow = null;
            relay = null;
        }

        private static void DestroyOwned(List<Object> objects)
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i]) { if (objects[i] is GameObject go) go.SetActive(false); Object.Destroy(objects[i]); }
            objects.Clear();
        }

        private void OnDestroy() { RemoveSignals(); }
    }
}
