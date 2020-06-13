using System.IO;
using System.Linq;

using StardewValley;
using StardewModdingAPI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

using xTile;
using xTile.Dimensions;

namespace SecretBase.Editors
{
	internal class WorldEditor : IAssetEditor
	{
		private readonly IModHelper _helper;
		internal static readonly Location IconLocation = new Location(506, 372);
		
		public WorldEditor(IModHelper helper)
		{
			_helper = helper;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return asset.DataType == typeof(Map) || asset.AssetNameEquals("LooseSprites\\Cursors");
		}

		public void Edit<T>(IAssetData asset)
		{
			if (asset.DataType == typeof(Map))
			{
				var map = asset.GetData<Map>();
				var nameSplit = asset.AssetName.Split('\\');
				var name = nameSplit[nameSplit.Length - 1];

				// Edit maps containing secret base entrances
				var numBasesInThisLocation = ModConsts.BaseEntryLocations.Count(_ => _.Value.Equals(name));
				if (numBasesInThisLocation <= 0)
					return;
				Log.D($"Patching in {numBasesInThisLocation} secret bases to {name}.",
					ModEntry.Instance.Config.DebugMode);
				ModEntry.EditVanillaMap(map, name);
			}
			else if (!asset.AssetNameEquals("LooseSprites\\Cursors"))
			{}
			else
			{
				// Home-cook a notification icon for under the HUD money tray:

				// Prime a canvas as a clipboard to hold each a copy of the vanilla icon
				// and our custom icon to merge together into a target open space in Cursors
				const int iconW = 11;
				const int iconH = 14;
				var data = asset.AsImage().Data;
				var canvas = new Color[iconW * iconH];
				var texture = new Texture2D(Game1.graphics.GraphicsDevice, iconW, iconH);
				var vanillaIconArea = new Rectangle(383, 493, iconW, iconH);
				var targetArea = new Rectangle(IconLocation.X, IconLocation.Y, iconW, iconH);

				// Patch in a copy of the vanilla quest log icon
				data.GetData(0, vanillaIconArea, canvas, 0, canvas.Length);
				texture.SetData(canvas);
				asset.AsImage().PatchImage(texture, null, targetArea, PatchMode.Replace);
				
				// Chroma-key our custom icon with colours from the vanilla icon
				var colorSampleA = canvas[iconW * 5 + 1];
				var colorSampleB = canvas[iconW * 11 + 1];

				var colorR = new Color(255, 0, 0);
				var colorC = new Color(255, 0, 255);
				var colorG = new Color(0, 255, 0);
				var colorA = new Color(0, 0, 0, 0);

				var icon = _helper.Content.Load<Texture2D>(Path.Combine(
					ModConsts.AssetsPath, ModConsts.OutdoorsStuffTilesheetId + ".png"));
				icon.GetData(0, new Rectangle(0, 0, iconW, iconH),
					canvas, 0, canvas.Length);

				for (var i = 0; i < canvas.Length; ++i)
				{
					if (canvas[i] == colorC)
						canvas[i] = colorA;
					else if (canvas[i] == colorG)
						canvas[i] = colorSampleA;
					else if (canvas[i] == colorR)
						canvas[i] = colorSampleB;
				}
				
				// Patch in the custom icon over the vanilla icon copy
				texture.SetData(canvas);
				asset.AsImage().PatchImage(texture, null, targetArea, PatchMode.Overlay);

				// Patch in an alpha-shaded copy of the custom icon to use for the pulse animation
				var colorShade = new Color(0, 0, 0, 0.35f);

				for (var i = 0; i < canvas.Length; ++i)
				{
					if (canvas[i] == colorSampleB)
						canvas[i] = colorShade;
					else if (canvas[i] == colorSampleA)
						canvas[i] = colorA;
				}

				texture.SetData(canvas);
				asset.AsImage().PatchImage(texture, null,
					new Rectangle(targetArea.X - targetArea.Width, targetArea.Y, targetArea.Width, targetArea.Height),
					PatchMode.Overlay);
			}
		}

		private void EditTilesheet() {
			var season = Game1.currentSeason switch
			{
				"summer" => 1,
				"fall" => 2,
				"winter" => 3,
				_ => 0
			};

		}
	}
}
