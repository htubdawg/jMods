using System;
using UnityEngine;

namespace HalfGripStereo
{
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
