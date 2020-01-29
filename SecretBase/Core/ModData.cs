using System.Collections.Generic;
using StardewValley.Objects;

namespace SecretBase
{
	internal class ModData
	{
		internal Dictionary<string, long> SecretBaseOwnership { get; set; }
		internal Dictionary<long, Chest> GlobalStorage { get; set; }

		internal ModData()
		{
			SecretBaseOwnership = new Dictionary<string, long>();
			GlobalStorage = new Dictionary<long, Chest>();
		}
	}
}
