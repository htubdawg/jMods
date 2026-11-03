using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HalfGripTools;
using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// The helicopter as the other players in a Multiplayer room see it. The Multiplayer mod shows somebody else's car as a stand-in (an
	/// enemy car from the game's prefab, moved to the positions that player sends, dressed in their paint and body), and knows nothing of
	/// this mod, so without this the others saw the last car body that player had, flying. Each player's copy of this mod therefore tells
	/// the others, over HalfGripTools' mod channel (<c>ctx.Net</c>, 1.0.6+), whether the helicopter is on their car, the measures its shape
	/// was built from and the rotor's speed; the others put the same helicopter on that player's stand-in.
	///
	/// <para><c>ctx.Net</c> and <c>HalfGripTools.Net</c> are read by reflection, so the mod still compiles and runs on HalfGripTools
	/// 1.0.2 (solo, no Multiplayer). The stand-in is the Multiplayer mod's internal <c>Multiplayer.RemoteCar</c> (fields <c>PeerId</c>,
	/// <c>IsAi</c>), found by name the way the Open World finds it; if it is not there, a warning is logged once and nothing is shown.</para>
	///
	/// <para>The message (version 1): byte 1, then 1 if the helicopter is on (0 = off, nothing more), the rotor speed 0-255, and the
	/// shape's inputs as 8 little-endian floats (length, width, centre x, centre z, ground, head x, y, z; the car's space, which is
	/// the stand-in's too, since it is placed at the car's position). Sent four times a second while on (reliably once a second, so
	/// a player who joins or a lost packet is caught up), and reliably when it comes off.</para>
	/// </summary>
	internal static class Remote
	{
		private const byte Kind = 1;
		private const int OnLength = 3 + 8 * 4;
		/// <summary>No word from a player for this long and their helicopter is taken off (they left without a goodbye).</summary>
		private const float Silence = 10f;

		private sealed class State
		{
			internal Shape.Frame Look;
			internal float Rpm;
			internal float Shown;
			internal float Stamp;
			internal Airframe Airframe;
			internal CarControllerNew FailedOn;
		}

		private static object net;
		private static MethodInfo send;
		private static PropertyInfo inRoom;
		private static readonly Dictionary<ulong, State> peers = new Dictionary<ulong, State>();

		private static bool wasOn;
		private static int sends;

		private static bool searched;
		private static Type standInType;
		private static FieldInfo standInPeer;
		private static FieldInfo standInAi;
		private static bool warnedNoStandIn;

		/// <summary>Hooks up <c>ctx.Net</c> when this HalfGripTools has it (1.0.6+).</summary>
		internal static void Link(ModContext ctx)
		{
			try
			{
				PropertyInfo prop = ctx.GetType().GetProperty("Net");
				net = prop != null ? prop.GetValue(ctx) : null;
				if (net == null)
				{
					Mod.Info("Helicopter: this HalfGripTools has no mod network channel (1.0.6+): other players will not see the helicopter.");
					return;
				}
				send = net.GetType().GetMethod("Send", new[] { typeof(byte[]), typeof(ulong), typeof(bool) });
				MethodInfo onReceive = net.GetType().GetMethod("OnReceive", new[] { typeof(Action<ulong, byte[]>) });
				Type statics = ctx.GetType().Assembly.GetType("HalfGripTools.Net");
				inRoom = statics != null ? statics.GetProperty("InRoom", BindingFlags.Public | BindingFlags.Static) : null;
				if (send == null || onReceive == null || inRoom == null)
				{
					Mod.Log.LogWarning("Helicopter: HalfGripTools' mod network channel is not as expected (Send, OnReceive, Net.InRoom): other players will not see the helicopter.");
					net = null;
					return;
				}
				onReceive.Invoke(net, new object[] { new Action<ulong, byte[]>(Receive) });
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: could not hook up the mod network channel: " + e.Message);
				net = null;
			}
		}

		private static bool InRoom
		{
			get
			{
				try
				{
					return net != null && inRoom.GetValue(null) is bool b && b;
				}
				catch
				{
					return false;
				}
			}
		}

		/// <summary>Four times a second, from <see cref="HeliDriver"/>: tell the others about this car, and dress their stand-ins.</summary>
		internal static void Tick(bool on, Flight flight, Airframe airframe)
		{
			if (net == null)
			{
				return;
			}
			if (!InRoom)
			{
				wasOn = false;
				Forget();
				return;
			}
			try
			{
				Tell(on && airframe != null, flight, airframe);
				Dress();
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: the other players' helicopters: " + e.Message);
			}
		}

		private static void Tell(bool on, Flight flight, Airframe airframe)
		{
			if (!on)
			{
				if (wasOn)
				{
					wasOn = false;
					Send(new byte[] { Kind, 0 }, true);
				}
				return;
			}
			Shape.Frame f = airframe.Frame;
			byte[] data = new byte[OnLength];
			data[0] = Kind;
			data[1] = 1;
			data[2] = (byte)Mathf.RoundToInt(Mathf.Clamp01(flight != null ? flight.Rpm : 0f) * 255f);
			float[] values = { f.Length, f.Width, f.CentreX, f.CentreZ, f.Ground, f.Head.x, f.Head.y, f.Head.z };
			for (int i = 0; i < values.Length; i++)
			{
				Write(data, 3 + 4 * i, values[i]);
			}
			bool reliable = !wasOn || sends % 4 == 0;
			wasOn = true;
			sends++;
			Send(data, reliable);
		}

		private static void Send(byte[] data, bool reliable)
		{
			send.Invoke(net, new object[] { data, 0UL, reliable });
		}

		private static void Write(byte[] data, int at, float value)
		{
			byte[] b = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(b);
			}
			Array.Copy(b, 0, data, at, 4);
		}

		private static float Read(byte[] data, int at)
		{
			byte[] b = new byte[4];
			Array.Copy(data, at, b, 0, 4);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(b);
			}
			return BitConverter.ToSingle(b, 0);
		}

		/// <summary>A message from another player's copy of this mod (main thread). Anything malformed is dropped.</summary>
		private static void Receive(ulong from, byte[] data)
		{
			try
			{
				if (data == null || data.Length < 2 || data[0] != Kind)
				{
					return;
				}
				if (data[1] == 0)
				{
					if (peers.TryGetValue(from, out State gone))
					{
						TakeOff(gone);
						peers.Remove(from);
						Mod.Info("Helicopter: player " + from + " took the helicopter off.");
					}
					return;
				}
				if (data.Length < OnLength)
				{
					return;
				}
				float[] v = new float[8];
				for (int i = 0; i < 8; i++)
				{
					v[i] = Read(data, 3 + 4 * i);
					if (float.IsNaN(v[i]) || float.IsInfinity(v[i]) || Mathf.Abs(v[i]) > 100f)
					{
						return;
					}
				}
				if (v[0] <= 0.1f || v[1] <= 0.1f)
				{
					return;
				}
				Shape.Frame look = new Shape.Frame { Length = v[0], Width = v[1], CentreX = v[2], CentreZ = v[3], Ground = v[4], Head = new Vector3(v[5], v[6], v[7]) };
				if (!peers.TryGetValue(from, out State s))
				{
					s = new State();
					peers[from] = s;
					Mod.Info("Helicopter: player " + from + " flies a helicopter (car length " + v[0].ToString("0.00") + ").");
				}
				else if (!Same(s.Look, look))
				{
					TakeOff(s); // another car: built again to its measures
				}
				s.Look = look;
				s.Rpm = data[2] / 255f;
				s.Stamp = Time.unscaledTime;
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: a message from another player could not be read: " + e.Message);
			}
		}

		private static bool Same(Shape.Frame a, Shape.Frame b)
		{
			return Mathf.Abs(a.Length - b.Length) < 1e-3f && Mathf.Abs(a.Width - b.Width) < 1e-3f && Mathf.Abs(a.CentreX - b.CentreX) < 1e-3f
				&& Mathf.Abs(a.CentreZ - b.CentreZ) < 1e-3f && Mathf.Abs(a.Ground - b.Ground) < 1e-3f && (a.Head - b.Head).sqrMagnitude < 1e-6f;
		}

		/// <summary>
		/// Puts the helicopter on each flying player's stand-in that has none yet (the stand-in is made at the race's start and again on
		/// some respawns; the airframe goes with it), and takes off those of players gone quiet.
		/// </summary>
		private static void Dress()
		{
			if (peers.Count == 0)
			{
				return;
			}
			List<ulong> quiet = null;
			Dictionary<ulong, CarControllerNew> standIns = null;
			foreach (KeyValuePair<ulong, State> kv in peers)
			{
				State s = kv.Value;
				if (Time.unscaledTime - s.Stamp > Silence)
				{
					(quiet ??= new List<ulong>()).Add(kv.Key);
					continue;
				}
				if (s.Airframe != null)
				{
					continue;
				}
				standIns ??= StandIns();
				if (!standIns.TryGetValue(kv.Key, out CarControllerNew car) || car == null || car == s.FailedOn)
				{
					continue;
				}
				try
				{
					State held = s;
					s.Airframe = Airframe.BuildRemote(car, s.Look, () => held.Shown = Mathf.MoveTowards(held.Shown, held.Rpm, Time.deltaTime * 2f));
				}
				catch (Exception e)
				{
					s.FailedOn = car;
					Mod.Log.LogWarning("Helicopter: could not put the helicopter on another player's car (not tried again on that car): " + e);
				}
			}
			if (quiet != null)
			{
				foreach (ulong id in quiet)
				{
					TakeOff(peers[id]);
					peers.Remove(id);
				}
			}
		}

		/// <summary>The Multiplayer mod's stand-ins for other players (not its AI cars), by player id.</summary>
		private static Dictionary<ulong, CarControllerNew> StandIns()
		{
			Dictionary<ulong, CarControllerNew> found = new Dictionary<ulong, CarControllerNew>();
			if (!searched)
			{
				searched = true;
				foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
				{
					Type t;
					try
					{
						t = a.GetType("Multiplayer.RemoteCar", false);
					}
					catch
					{
						continue;
					}
					if (t != null && typeof(Component).IsAssignableFrom(t))
					{
						standInType = t;
						standInPeer = AccessTools.Field(t, "PeerId");
						standInAi = AccessTools.Field(t, "IsAi");
						break;
					}
				}
			}
			if (standInType == null || standInPeer == null || standInAi == null)
			{
				if (!warnedNoStandIn)
				{
					warnedNoStandIn = true;
					Mod.Log.LogWarning("Helicopter: the Multiplayer mod's RemoteCar (PeerId, IsAi) was not found (another version?): other players' helicopters are not shown.");
				}
				return found;
			}
			foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(standInType, FindObjectsSortMode.None))
			{
				Component c = o as Component;
				if (c == null || !(standInPeer.GetValue(c) is ulong id) || (standInAi.GetValue(c) is bool ai && ai))
				{
					continue;
				}
				CarControllerNew car = c.GetComponent<CarControllerNew>();
				if (car != null)
				{
					found[id] = car;
				}
			}
			return found;
		}

		private static void TakeOff(State s)
		{
			if (s.Airframe != null)
			{
				UnityEngine.Object.Destroy(s.Airframe.gameObject);
			}
			s.Airframe = null;
			s.FailedOn = null;
		}

		/// <summary>Out of the room: every other player's helicopter off and forgotten.</summary>
		internal static void Forget()
		{
			if (peers.Count == 0)
			{
				return;
			}
			foreach (State s in peers.Values)
			{
				TakeOff(s);
			}
			peers.Clear();
		}
	}
}
