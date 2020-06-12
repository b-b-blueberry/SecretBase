﻿using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SecretBase
{
	public class ModConsts
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
		internal const string HoleAction = ModId + "Hole";
		internal const string ExtraLayerId = ModId + "ExtraStuffLayer";
		internal const string AssetsPath = "assets";
		internal const string OutdoorsStuffTilesheetId = "z_secretbase_outdoors";
		internal const string IndoorsStuffTilesheetId = "z_secretbase_indoors";
		internal const string DataFile = "moddata_{0}.json";
		internal const int DummyChestCoords = -100;

		// TODO: ASSETS: SVE-compatible dictionaries
		// TODO: ASSETS: Stardew Reimagined 2 dictionaries
		
		internal static readonly List<string> AcceptableMapTypes = new List<string>
		{
			"Default",
			"Expanded",
			"Reimagined"
		};

		// Note: Secret Bases are only passed around by name in the system, so swapping between map
		// overhauls shouldn't have any unwanted side effects. It'll pick the base nearest to the
		// player's cursor interactions and fetch the data from the dictionary.

		// TODO: BUGS: Test this theory, see whether base entry tiles start appearing in odd places

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
			//{ "SecretBaseBush1", "Backwoods" },
			//{ "SecretBaseBush2", "Forest" },

			{ "SecretBaseDesert0", "Desert" },
			{ "SecretBaseDesert1", "Desert" },
			{ "SecretBaseDesert2", "Desert" },
		};

		internal static readonly Dictionary<string, Vector2> BaseEntryCoordinates = new Dictionary<string, Vector2>
		{
			{ "SecretBaseTree0", new Vector2(41, 8) },
			{ "SecretBaseTree1", new Vector2(62, 8) },
			{ "SecretBaseTree2", new Vector2(48, 3) },
			{ "SecretBaseTree3", new Vector2(9, 1) },
			{ "SecretBaseTree4", new Vector2(45, 12) },
			{ "SecretBaseTree5", new Vector2(27, 95) },

			{ "SecretBaseRock0", new Vector2(35, 18) },
			{ "SecretBaseRock1", new Vector2(109, 8) },
			{ "SecretBaseRock2", new Vector2(29, 7) },
			{ "SecretBaseRock3", new Vector2(31, 34) },

			{ "SecretBaseBush0", new Vector2(86, 1) },
			//{ "SecretBaseBush1", new Vector2(30, 22) },
			//{ "SecretBaseBush2", new Vector2(3, 26) },

			{ "SecretBaseDesert0", new Vector2(2, 42) },
			{ "SecretBaseDesert1", new Vector2(19, 6) },
			{ "SecretBaseDesert2", new Vector2(31, 41) },
		};
		
		// Planks usually match the orientation of the corridor they're found in
		internal static readonly Dictionary<string, List<Rectangle>> BaseHoleCoordinates = new Dictionary<string, List<Rectangle>>
		{
			{ "SecretBaseTree3", new List<Rectangle>
				{
					new Rectangle(4, 10, 2, 3),
					new Rectangle(13, 11, 2, 3)
				}
			},
			{ "SecretBaseDesert2", new List<Rectangle>
				{
					new Rectangle(12, 7, 3, 2)
				}
			}
		};
	}
}
