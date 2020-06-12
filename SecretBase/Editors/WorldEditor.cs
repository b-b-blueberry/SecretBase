using System;
using System.IO;
using System.Linq;

using StardewValley;
using StardewModdingAPI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

using xTile;
using xTile.Dimensions;
using xTile.Layers;
using xTile.Tiles;

using PyTK.Extensions;

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
				EditVanillaMap(map, name);
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

		private void EditVanillaMap(Map map, string name) {
			// TODO: METHOD: Add seasonal loading for all entry themes once assets are ready
			// TODO: BUGS: Resolve beach/beach-nightmarket entry patching inconsistency when Night Markets are active

			var path = _helper.Content.GetActualAssetKey(
				Path.Combine(ModConsts.AssetsPath, $"{ModConsts.OutdoorsStuffTilesheetId}.png"));
			var texture = _helper.Content.Load<Texture2D>(path);

			// Add secret base tilesheet
			var tilesheet = new TileSheet(ModConsts.OutdoorsStuffTilesheetId, map, path,
				new Size(texture.Width / 16, texture.Height / 16),
				new Size(16, 16));
			map.AddTileSheet(tilesheet);
			map.LoadTileSheets(Game1.mapDisplayDevice);

			// Add secret base entries for this map
			var layer = map.GetLayer("Buildings");
			layer = new Layer(ModConsts.ExtraLayerId, map, layer.LayerSize, layer.TileSize);

			const int frameInterval = 150;
			const BlendMode blend = BlendMode.Additive;
			foreach (var baseLocation in ModConsts.BaseEntryLocations
				.Where(_ => _.Value.Equals(name)))
			{
				var coords = ModConsts.BaseEntryCoordinates[baseLocation.Key];
				var row = tilesheet.SheetWidth;

				// TODO: ASSETS: Patch in inactive assets once they've been made

				var index = 0;
				switch (ModEntry.GetSecretBaseTheme(baseLocation.Key))
				{
					case ModConsts.Theme.Tree:
						// exactly two (2) animated tiles
						index = row * 2;
						map.GetLayer("Front").Tiles[(int)coords.X, (int)coords.Y] = new AnimatedTile(layer, new[]
						{
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index + 1),
							new StaticTile(layer, tilesheet, blend, index + 2),
							new StaticTile(layer, tilesheet, blend, index + 3),
							new StaticTile(layer, tilesheet, blend, index + 1),
						}, frameInterval);
						index += row;
						map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = new AnimatedTile(layer, new[]
						{
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index), new StaticTile(layer, tilesheet, blend, index),
							new StaticTile(layer, tilesheet, blend, index + 1),
							new StaticTile(layer, tilesheet, blend, index + 2),
							new StaticTile(layer, tilesheet, blend, index + 3),
							new StaticTile(layer, tilesheet, blend, index + 1),
						}, frameInterval);
						break;

					case ModConsts.Theme.Bush:
						// 2 static tiles
						index = row * 4;
						layer.Tiles[(int)coords.X, (int)coords.Y] = new StaticTile(layer, tilesheet, blend, index);
						index += row;
						if (map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y] == null)
							layer.Tiles[(int)coords.X, (int)coords.Y + 1] = new StaticTile(layer, tilesheet, blend, index);
						else
							map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1]
								= new StaticTile(layer, tilesheet, blend, index);
						break;
						
					case ModConsts.Theme.Cave:
					case ModConsts.Theme.Desert:
					case ModConsts.Theme.Rock:
						// 2 static tiles
						index = row * 6;
						if (map.GetLayer("Buildings").Tiles[(int) coords.X, (int) coords.Y + 1]?.TileIndex == 370)
							index = row * 8;
						layer.Tiles[(int)coords.X, (int)coords.Y]
							= new StaticTile(layer, tilesheet, blend, index);
						layer.Tiles[(int)coords.X, (int)coords.Y + 1]
							= new StaticTile(layer, tilesheet, blend, index + row);
						break;

					default:
						// and 1 duck egg
						throw new NotImplementedException($"No theme handling for secret base {name}.");
				}

				// Enable player interactions
				map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1].Properties["Action"]
					= ModConsts.BaseEntryAction;
			}

			// Draw the extra layer above Buildings layer
			layer.Properties["DrawAbove"] = "Buildings";
			map.AddLayer(layer);
			map.enableMoreMapLayers();
		}
	}
}
