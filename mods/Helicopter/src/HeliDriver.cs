using System;
using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// Puts the helicopter on the player's car and takes it off. Four times a second it looks for the player's <see cref="CarLink"/> and
	/// whether the loadout carries the Helicopter, so it follows race starts, scene changes and the Open World's live refits alike.
	/// While it is on, <see cref="Active"/> tells the patches in <see cref="Lock"/> to stop the car's own driving
	/// (<see cref="DriveCar"/> runs instead), to keep the Big Air achievement back and to let the collective open garages.
	/// </summary>
	internal sealed class HeliDriver : MonoBehaviour
	{
		internal static bool Active;
		internal static CarLink Link;

		private static Cockpit cockpit;
		private static Flight flight;
		private static Airframe airframe;
		/// <summary>The car a build failed on: not tried again until there is another car (a new race), so a failure is logged once.</summary>
		private static CarLink failedOn;
		private float next;

		private void Update()
		{
			if (Active && Link == null)
			{
				// The race's scene is gone, and the helicopter with it.
				Clear();
			}
			if (Time.unscaledTime < next)
			{
				return;
			}
			next = Time.unscaledTime + 0.25f;
			try
			{
				CarLink link = Rig.First<CarLink>();
				bool want = link != null && link.cc != null && HeliItem.Fitted;
				if (want && !Active && link != failedOn)
				{
					Install(link);
				}
				else if (!want && Active)
				{
					Remove();
				}
				else if (Active)
				{
					RefitForGps();
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: " + e);
				failedOn = Link;
			}
		}

		private static void Install(CarLink link)
		{
			try
			{
				Link = link;
				cockpit = Cockpit.Build(link);
				if (cockpit == null)
				{
					failedOn = link;
					Remove();
					return;
				}
				Bounds body = Airframe.Body(link.cc);
				flight = link.cc.gameObject.AddComponent<Flight>();
				flight.Init(link, body.size.z * Mathf.Abs(link.cc.transform.lossyScale.z));
				airframe = Airframe.Build(link.cc, flight, body);
				try
				{
					cockpit.FindGps();
					airframe.FitPanel(cockpit);
				}
				catch (Exception e)
				{
					Mod.Log.LogWarning("Helicopter: the instrument panel could not be fitted round the controls (left as guessed): " + e.Message);
				}
				Active = true;
				Mod.Info("Helicopter: on the car (body " + body.size + ", mass " + (link.cc.rb != null ? link.cc.rb.mass : 0f) + ", gravity " + Physics.gravity.y + ").");
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: could not put the helicopter on the car (not tried again this race): " + e);
				failedOn = link;
				Remove();
			}
		}

		/// <summary>True while the GPS has changed (fitted, built, moved, taken off) and the panel's face is not yet drawn round it.</summary>
		private static bool gpsPending;

		/// <summary>
		/// The panel's face drawn again when the MiniMap's GPS comes, moves or goes (it builds itself after the cockpit, and live refits
		/// add or take it off); held back while the head is turned, since the fit looks through the cameras.
		/// </summary>
		private static void RefitForGps()
		{
			if (cockpit == null || airframe == null)
			{
				return;
			}
			gpsPending |= cockpit.FindGps();
			if (!gpsPending || !airframe.HeadAsFitted(cockpit))
			{
				return;
			}
			gpsPending = false;
			try
			{
				airframe.FitPanel(cockpit);
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: the instrument panel could not be fitted round the GPS: " + e.Message);
			}
		}

		private static void Remove()
		{
			gpsPending = false;
			if (cockpit != null)
			{
				Destroy(cockpit.gameObject);
			}
			if (flight != null)
			{
				Destroy(flight);
			}
			if (airframe != null)
			{
				Destroy(airframe.gameObject);
			}
			if (Active)
			{
				Mod.Info("Helicopter: off the car.");
			}
			Clear();
		}

		private static void Clear()
		{
			Active = false;
			Link = null;
			cockpit = null;
			flight = null;
			airframe = null;
		}

		/// <summary>
		/// In place of the car's own driving (<see cref="CarLink"/>'s FixedUpdate): no engine, no steering, the brakes on while parked,
		/// and the engine's sound and rev dial following the rotor speed.
		/// </summary>
		internal static void DriveCar(CarLink link)
		{
			CarControllerNew cc = link.cc;
			if (cc == null)
			{
				return;
			}
			float park = flight != null ? flight.Parking : 1f;
			float rpm = flight != null ? flight.Rpm : 0f;
			cc.engineAccel = 0f;
			cc.pedalInput = 0f;
			cc.steeringAngleDeg = 0f;
			cc.brakeAccel = link.brakeDeccel * park;
			cc.handBrake = link.brakeDeccel * park;
			link.engineIndicator = 0.1f + rpm;
		}

		private void OnDestroy()
		{
			Remove();
		}
	}
}
