using System;
using System.Reflection;

namespace HalfGripSlapPack
{
	/// <summary>Binds the public v1 weather contract once, with no dependency on the publisher's assembly.</summary>
	internal sealed class OptionalWeatherApi<TWind>
	{
		internal delegate bool Query(out float rain, out float snow, out TWind wind);
		private readonly Query query;

		private OptionalWeatherApi(Query query) { this.query = query; }

		/// <summary>Accepts only a public, correctly versioned delegate-compatible contract.</summary>
		internal static bool TryBind(Type type, out OptionalWeatherApi<TWind> api, out string reason)
		{
			api = null; reason = null;
			try
			{
				if (type == null || !type.IsVisible) throw new MissingMemberException("Public WeatherView type is unavailable.");
				const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
				MethodInfo version = type.GetProperty("ApiVersion", flags)?.GetGetMethod(false);
				if (version == null || version.ReturnType != typeof(int)) throw new MissingMemberException("Public ApiVersion getter is missing.");
				var readVersion = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), version);
				if (readVersion() != 1) throw new NotSupportedException("WeatherView API version is not supported (requires v1).");
				MethodInfo method = type.GetMethod("TryGetLocalWeather", flags, null,
					new[] { typeof(float).MakeByRefType(), typeof(float).MakeByRefType(), typeof(TWind).MakeByRefType() }, null);
				if (method == null || method.ReturnType != typeof(bool)) throw new MissingMemberException("Public TryGetLocalWeather signature does not match v1.");
				api = new OptionalWeatherApi<TWind>((Query)Delegate.CreateDelegate(typeof(Query), method));
				return true;
			}
			catch (Exception error) { reason = error.Message; return false; }
		}

		/// <summary>Polls the already-bound public method without reflection, boxing or an argument array.</summary>
		internal bool TryRead(out float rain, out float snow, out TWind wind) => query(out rain, out snow, out wind);
	}
}
