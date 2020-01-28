using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley.Objects;

namespace SecretBase
{
	internal class ModData
	{
		public Dictionary<string, long> SecretBaseOwnership { get; set; }
		public Dictionary<long, Chest> GlobalStorage { get; set; }

		public ModData()
		{
			SecretBaseOwnership = new Dictionary<string, long>();
			GlobalStorage = new Dictionary<long, Chest>();
		}
	}
}
