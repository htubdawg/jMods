using System;
using UnityEngine;

namespace HalfGripStereo
{
	/// <summary>
	/// The volume knob as the cockpit hand holds it: the game's own Grabbable (the hand grabs whatever Grabbable is over the collider it
	/// hovers, holds it while the button is down and moves it in its plane), so the knob is grabbed and turned like the wheel. The
	/// object never moves or turns (no rotated sprites: the casing is redrawn with the knob's mark at the new place); it only measures
	/// how far the hand has gone round its middle. <see cref="Grabbable.angle"/> keeps up with the turn, so the point the hand holds
	/// goes round with it and the game keeps the cursor near it, and stops at the ends of the knob's travel.
	/// </summary>
	internal sealed class KnobGrab : Grabbable
	{
		/// <summary>The knob's travel, 7 o'clock to 5 o'clock.</summary>
		internal const float Sweep = Mathf.PI * 1.5f;

		/// <summary>The knob's turn now (0 to 1), and what to do with a new one.</summary>
		internal Func<float> Read;
		internal Action<float> Turned;
		internal Action RightClicked;

		private float startTurn;
		private float lastHand;
		private float turned;

		protected override void OnGrab()
		{
			startTurn = Read != null ? Read() : 0f;
			lastHand = Mathf.Atan2(grabbedPoint.y, grabbedPoint.x);
			turned = 0f;
			angle = 0f;
		}

		protected override void OnHandMoved()
		{
			Vector2 d = constrainedPoint - position;
			if (d.sqrMagnitude < 1e-4f)
			{
				return;
			}
			float a = Mathf.Atan2(d.y, d.x);
			turned += Mathf.DeltaAngle(lastHand * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad;
			lastHand = a;
			// Counter-clockwise turns it down. Past an end it stops, and turning back moves it at once.
			float t = Mathf.Clamp01(startTurn - turned / Sweep);
			turned = (startTurn - t) * Sweep;
			angle = turned;
			Turned?.Invoke(t);
		}

		protected override void OnRelease() => angle = 0f;

		protected override void OnRightClick() => RightClicked?.Invoke();

		protected override void StepPhysics(float dt)
		{
		}

		protected override void StepDirect(float dt)
		{
		}

		protected override void ApplyPose()
		{
		}
	}

	/// <summary>
	/// A sleeve of the CD wallet: what the hand grabs there is not the sleeve but a new loose disc taken out of it (the game's own
	/// grab redirect), which the hand then carries. An empty sleeve's collider is switched off.
	/// </summary>
	internal sealed class SleeveGrab : Grabbable
	{
		internal Func<Grabbable> Take;

		protected override void Awake()
		{
			base.Awake();
			grabRedirect = () => Take?.Invoke();
		}

		protected override void StepPhysics(float dt)
		{
		}

		protected override void StepDirect(float dt)
		{
		}

		protected override void ApplyPose()
		{
		}
	}

	/// <summary>
	/// A disc out of its sleeve: in the hand, falling, or going in or out of the slot. Held, it follows the hand exactly (no tilt, it is
	/// not turned: pixel art). Let go, the stereo decides where it goes (<see cref="Dropped"/>); falling, it drops under its own gravity
	/// in the cockpit's plane with the speed it was thrown with. Going in or out of the slot, the stereo moves it.
	/// </summary>
	internal sealed class DiscGrab : Grabbable
	{
		internal enum Mode { Held, Falling, Slot }

		internal int Index;
		internal Mode State = Mode.Slot;
		internal Action<DiscGrab> Dropped;
		internal Action<DiscGrab> Taken;
		/// <summary>Cockpit units per second squared, down the cockpit's plane.</summary>
		internal float Gravity = 30f;

		private Vector2 handLast;

		/// <summary>The disc's middle in its parent's plane.</summary>
		internal Vector2 Centre
		{
			get => position;
			set
			{
				position = value;
				velocity = Vector2.zero;
				ApplyPose();
			}
		}

		protected override void OnGrab()
		{
			State = Mode.Held;
			velocity = Vector2.zero;
			handLast = constrainedPoint;
			Taken?.Invoke(this);
		}

		protected override void OnRelease()
		{
			velocity *= directSpeedKeptOnRelease;
			Dropped?.Invoke(this);
		}

		protected override void StepPhysics(float dt) => StepDirect(dt);

		protected override void StepDirect(float dt)
		{
			if (IsGrabbed)
			{
				Vector2 now = constrainedPoint - grabbedPoint;
				velocity = (constrainedPoint - handLast) / dt;
				handLast = constrainedPoint;
				position = now;
			}
			else if (State == Mode.Falling)
			{
				velocity += new Vector2(0f, -Gravity) * dt;
				position += velocity * dt;
			}
			angle = 0f;
		}
	}
}
