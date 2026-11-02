using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// Flies the car. Every number is worked out from the car's own mass, the scene's gravity and the config, so it needs no tuning per
	/// car or world scale:
	/// <list type="bullet">
	/// <item><b>Rotor speed</b> (0..1) follows the throttle, spinning up over <c>SpoolSeconds</c> and down over half as long again.</item>
	/// <item><b>Thrust</b> along the car's up is <c>m g · 2 · collective · rpm²</c>: at full rotor speed the collective half way up
	/// hovers, the top doubles the weight. Within a rotor's width of the ground the cushion adds up to 12%.</item>
	/// <item><b>Vertical damping</b> along the car's up makes the collective a climb-rate control: <c>k = g / ClimbRate</c>, so a full
	/// collective (a net g upwards) climbs at <c>ClimbRate</c> and half is a steady hover.</item>
	/// <item><b>Stick</b>: an attitude hold. It asks for a pitch and a bank of up to <c>MaxTilt</c> and a critically damped spring
	/// (natural frequency <c>TiltResponse</c>, its turning speed capped at <c>TiltRate</c>) turns the car there; let go, it levels and the helicopter settles into a hover. Sideways also
	/// yaws (up to 1 rad/s), so it turns on the spot in a hover; at speed a tail fin swings the nose into the wind, so a banked turn
	/// carries the nose round.</item>
	/// <item><b>Drag</b> across the ground: quadratic, sized so that a full tilt's push (<c>g tan MaxTilt</c>) balances it at
	/// <c>TopSpeed</c>, plus a little linear drag so a hover comes to rest.</item>
	/// </list>
	/// Torques are angular accelerations (<see cref="ForceMode.Acceleration"/>), so the car's inertia does not change the handling.
	/// On the ground with less than 90% of the weight lifted the car's brakes hold it (<see cref="Parking"/>, applied by
	/// <see cref="HeliDriver.DriveCar"/>), so it sits still on its wheels like skids.
	/// </summary>
	internal sealed class Flight : MonoBehaviour
	{
		private const float MaxYawRate = 1f;
		private const float Weathervane = 1.5f;
		private const float GroundCushion = 0.12f;
		private const float LowSpeedDrag = 0.1f;

		/// <summary>Rotor speed, 0 (stopped) to 1 (full).</summary>
		internal float Rpm;
		/// <summary>Thrust over weight, last physics step.</summary>
		internal float Lift;
		/// <summary>1 while the car should hold still on the ground (brakes on), 0 in the air.</summary>
		internal float Parking = 1f;
		/// <summary>The car's length, measured from its body; the rotor's size and the ground cushion's height.</summary>
		internal float Length = 13f;

		private CarLink link;
		private Rigidbody rb;
		private readonly RaycastHit[] hits = new RaycastHit[16];
		private float chop;

		internal void Init(CarLink link, float length)
		{
			this.link = link;
			rb = link.cc != null ? link.cc.rb : null;
			if (rb == null)
			{
				rb = link.cc != null ? link.cc.GetComponent<Rigidbody>() : null;
			}
			Length = Mathf.Max(4f, length);
		}

		private void FixedUpdate()
		{
			Cockpit cockpit = Cockpit.Instance;
			if (rb == null || cockpit == null || rb.isKinematic)
			{
				return;
			}
			float dt = Time.fixedDeltaTime;
			float g = Mathf.Max(0.1f, -Physics.gravity.y);

			// Rotor speed after the throttle.
			float throttle = cockpit.ThrottleValue;
			float spool = Mathf.Max(0.2f, Mod.SpoolSeconds.Value);
			Rpm = Mathf.MoveTowards(Rpm, throttle, dt / (throttle > Rpm ? spool : spool * 1.5f));

			// No lift before a race's start (the car's engine gives no power then either).
			bool started = link.tm == null || link.tm.raceStarted;
			float collective = started ? cockpit.CollectiveValue : 0f;
			float height = Height();
			float cushion = 1f + GroundCushion * Mathf.Clamp01(1f - height / Length);
			Lift = 2f * collective * Rpm * Rpm * cushion;

			Vector3 up = rb.rotation * Vector3.up;
			rb.AddForce(up * (Lift * g), ForceMode.Acceleration);

			// The collective sets the climb rate: damping along the rotor's axis.
			float climbRate = Mathf.Max(1f, Mod.ClimbRate.Value);
			Vector3 v = rb.linearVelocity;
			float vUp = Vector3.Dot(v, up);
			bool grounded = link.cc != null && !link.cc.isAirborne;
			if (!grounded || Lift > 1f)
			{
				rb.AddForce(-up * (vUp * g / climbRate), ForceMode.Acceleration);
			}

			// Drag across the ground: balances a full tilt at the top speed.
			Vector3 vh = new Vector3(v.x, 0f, v.z);
			float speed = vh.magnitude;
			float top = Mathf.Max(5f, Mod.TopSpeed.Value);
			float tilt = Mathf.Clamp(Mod.MaxTilt.Value, 5f, 60f);
			float kQuad = g * Mathf.Tan(tilt * Mathf.Deg2Rad) / (top * top);
			if (!grounded && speed > 0.01f)
			{
				rb.AddForce(-vh * (kQuad * speed + LowSpeedDrag), ForceMode.Acceleration);
			}

			// The stick: attitude hold, and yaw.
			float authority = Rpm * (grounded ? Mathf.Clamp01(Lift) : 1f);
			if (authority > 0.001f)
			{
				Steer(cockpit.Stick, tilt, vh, speed, authority);
			}

			Parking = grounded && Lift < 0.9f ? 1f : 0f;
		}

		/// <summary>Pitch and bank towards the stick's, the nose's heading kept; yaw at the stick's rate plus the tail fin's.</summary>
		private void Steer(Vector2 stick, float tilt, Vector3 vh, float speed, float authority)
		{
			Vector3 forward = rb.rotation * Vector3.forward;
			Vector3 flat = new Vector3(forward.x, 0f, forward.z);
			if (flat.sqrMagnitude < 1e-4f)
			{
				flat = rb.rotation * Vector3.up;
				flat.y = 0f;
			}
			float heading = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
			// Positive x on Euler pitches the nose down (forward), negative z banks to the right.
			Quaternion wanted = Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(stick.y * tilt, 0f, -stick.x * tilt);
			Quaternion error = wanted * Quaternion.Inverse(rb.rotation);
			error.ToAngleAxis(out float angle, out Vector3 axis);
			if (angle > 180f)
			{
				angle -= 360f;
			}
			if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-6f)
			{
				axis = Vector3.zero;
			}
			Vector3 w = rb.angularVelocity;
			Vector3 wYaw = Vector3.up * w.y;
			// The critically damped spring kp e - kd w (kp = f², kd = 2f) written as kd (f/2 e - w): a turning rate wanted, then
			// damped towards it. Capping that rate keeps a full stick throw from flipping the nose over at the spring's own speed.
			float f = Mathf.Clamp(Mod.TiltResponse.Value, 0.5f, 10f);
			float maxRate = Mathf.Clamp(Mod.TiltRate.Value, 5f, 360f) * Mathf.Deg2Rad;
			Vector3 rate = axis.normalized * (angle * Mathf.Deg2Rad) * (0.5f * f);
			rate.y = 0f;
			rate = Vector3.ClampMagnitude(rate, maxRate);
			Vector3 level = (rate - (w - wYaw)) * (2f * f);
			level.y = 0f;

			float yawRate = stick.x * MaxYawRate;
			if (speed > 5f)
			{
				float slip = Vector3.SignedAngle(flat, vh, Vector3.up) * Mathf.Deg2Rad;
				yawRate += slip * Weathervane * Mathf.Clamp01((speed - 5f) / 30f);
			}
			float yaw = (yawRate - w.y) * 4f;
			rb.AddTorque((level + Vector3.up * yaw) * authority, ForceMode.Acceleration);
		}

		/// <summary>Height of the car above whatever is under it (its own colliders skipped), up to two lengths.</summary>
		private float Height()
		{
			float reach = Length * 2f;
			int n = Physics.RaycastNonAlloc(rb.position, Vector3.down, hits, reach, -5, QueryTriggerInteraction.Ignore);
			float best = reach;
			for (int i = 0; i < n; i++)
			{
				if (hits[i].rigidbody == rb)
				{
					continue;
				}
				best = Mathf.Min(best, hits[i].distance);
			}
			return best;
		}

		/// <summary>The blades' beat on the engine sound (which follows the rotor speed through <see cref="CarLink.engineIndicator"/>):
		/// CarLink sets the volume in its Update, this lowers it on every other half beat.</summary>
		private void LateUpdate()
		{
			if (link == null || link.engineaudio == null)
			{
				return;
			}
			chop += Time.deltaTime * Rpm * 7f;
			chop -= Mathf.Floor(chop);
			link.engineaudio.volume *= chop < 0.5f ? 1f : 1f - 0.35f * Rpm;
		}
	}
}
