using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SecretBase
{
	public class Const
	{
		public enum Theme
		{
			Tree,
			Rock,
			Bush,
			Desert,
			Cave
		}

		internal const string ModId = "SecretBase";
		internal const string BaseEntryAction = ModId + "Entry";
		internal const string LaptopAction = ModId + "Laptop";
		internal const string ExtraLayerId = ModId + "Stuff";
		internal const string TilesheetId = "z_secretbase_stuff";
		internal const string DataFile = "moddata_{0}.json";
		internal const int DummyChestCoords = -100;

		// todo: make sve-compatible dictionaries
		// todo: make stardew reimagined 2 dictionaries

		internal static readonly Dictionary<string, string> BaseEntryLocations = new Dictionary<string, string>
		{
			{ "SecretBaseTree0", "Forest" },
			{ "SecretBaseTree1", "Mountain" },
			{ "SecretBaseTree2", "Woods" },
			{ "SecretBaseTree3", "BusStop" },
			{ "SecretBaseTree4", "Town" },
			{ "SecretBaseTree5", "Town" },

			{ "SecretBaseRock0", "Mountain" },
			{ "SecretBaseRock1", "Mountain" },
			{ "SecretBaseRock2", "Backwoods" },
			{ "SecretBaseRock3", "Railroad" },

			{ "SecretBaseBush0", "Beach" },
			{ "SecretBaseBush1", "Backwoods" },
			{ "SecretBaseBush2", "Forest" },

			{ "SecretBaseDesert0", "Desert" },
			{ "SecretBaseDesert1", "Desert" },
			{ "SecretBaseDesert2", "Desert" },
		};

		internal static readonly Dictionary<string, Vector2> BaseEntryCoordinates = new Dictionary<string, Vector2>
		{
			{ "SecretBaseTree0", new Vector2(41, 8) },
			{ "SecretBaseTree1", new Vector2(62, 8) },
			{ "SecretBaseTree2", new Vector2(48, 3) },
			{ "SecretBaseTree3", new Vector2(29, 10) },
			{ "SecretBaseTree4", new Vector2(45, 12) },
			{ "SecretBaseTree5", new Vector2(27, 95) },

			{ "SecretBaseRock0", new Vector2(35, 18) },
			{ "SecretBaseRock1", new Vector2(109, 8) },
			{ "SecretBaseRock2", new Vector2(29, 7) },
			{ "SecretBaseRock3", new Vector2(31, 34) },

			{ "SecretBaseBush0", new Vector2(86, 1) },
			{ "SecretBaseBush1", new Vector2(30, 22) },
			{ "SecretBaseBush2", new Vector2(3, 26) },

			{ "SecretBaseDesert0", new Vector2(2, 42) },
			{ "SecretBaseDesert1", new Vector2(19, 6) },
			{ "SecretBaseDesert2", new Vector2(31, 41) },
		};
	}
}
