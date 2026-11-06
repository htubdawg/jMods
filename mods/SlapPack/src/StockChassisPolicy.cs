using System;
using System.Collections.Generic;

namespace HalfGripSlapPack
{
	/// <summary>Retains the original chassis IDs; later catalog additions never become supported implicitly.</summary>
	internal sealed class StockChassisPolicy
	{
		private readonly HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

		internal StockChassisPolicy(IEnumerable<string> stockIds)
		{
			foreach (string id in stockIds)
				if (!string.IsNullOrEmpty(id)) ids.Add(id);
		}

		internal bool Ready => ids.Count > 0;
		internal bool Includes(string id) => id != null && ids.Contains(id);
	}
}
