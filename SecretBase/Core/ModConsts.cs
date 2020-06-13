using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SecretBase
{
	public class ModConsts
	{
		internal const string ModId = "blueberry.SecretBase";
		internal const string AssetPrefix = ModId + ".";
		internal const string AssetsPath = "assets";
		internal const string BaseEntryAction = AssetPrefix + "Entry";
		internal const string LaptopAction = AssetPrefix + "Laptop";
		internal const string HoleAction = AssetPrefix + "Hole";
		internal const string ExtraLayerId = AssetPrefix + "ExtraStuffLayer";
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
			{ "Tree0", "Forest" },
			{ "Tree1", "Mountain" },
			{ "Tree2", "Woods" },
			{ "Tree3", "BusStop" },
			{ "Tree4", "Town" },
			{ "Tree5", "Town" },

			{ "Rock0", "Mountain" },
			{ "Rock1", "Mountain" },
			{ "Rock2", "Backwoods" },
			{ "Rock3", "Railroad" },

			{ "Bush0", "Beach" },
			//{ "Bush1", "Backwoods" },
			//{ "Bush2", "Forest" },

			{ "Desert0", "Desert" },
			{ "Desert1", "Desert" },
			{ "Desert2", "Desert" },
		};

		internal static readonly Dictionary<string, Vector2> BaseEntryCoordinates = new Dictionary<string, Vector2>
		{
			{ "Tree0", new Vector2(41, 8) },
			{ "Tree1", new Vector2(62, 8) },
			{ "Tree2", new Vector2(48, 3) },
			{ "Tree3", new Vector2(9, 1) },
			{ "Tree4", new Vector2(45, 12) },
			{ "Tree5", new Vector2(27, 95) },

			{ "Rock0", new Vector2(35, 18) },
			{ "Rock1", new Vector2(109, 8) },
			{ "Rock2", new Vector2(29, 7) },
			{ "Rock3", new Vector2(31, 34) },

			{ "Bush0", new Vector2(86, 1) },
			//{ "Bush1", new Vector2(30, 22) },
			//{ "Bush2", new Vector2(3, 26) },

			{ "Desert0", new Vector2(2, 42) },
			{ "Desert1", new Vector2(19, 6) },
			{ "Desert2", new Vector2(31, 41) },
		};
		
		// Planks usually match the orientation of the corridor they're found in
		internal static readonly Dictionary<string, List<Rectangle>> BaseHoleCoordinates = new Dictionary<string, List<Rectangle>>
		{
			{
				"Tree3", new List<Rectangle>
				{
					new Rectangle(4, 10, 2, 3),
					new Rectangle(13, 11, 2, 3)
				}
			},
			{
				"Desert2", new List<Rectangle>
				{
					new Rectangle(12, 7, 3, 2)
				}
			}
		};
	}
}
