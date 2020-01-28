using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using StardewValley;
using StardewModdingAPI;

using xTile;
using xTile.Dimensions;
using xTile.Display;
using xTile.Layers;
using xTile.Tiles;

using Rectangle = xTile.Dimensions.Rectangle;

namespace SecretBase.Editors
{
	internal class WorldEditor : IAssetEditor
	{
		private readonly IModHelper _helper;

		public WorldEditor(IModHelper helper)
		{
			_helper = helper;

		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return asset.DataType == typeof(Map);
		}

		public void Edit<T>(IAssetData asset)
		{
			var map = asset.GetData<Map>();
			var name = asset.AssetName.Split('\\')[1];
			var count = Const.BaseEntryLocations.Count(_ => _.Value.Equals(name));
			if (count == 0)
				return;

			// todo: add seasonal loading

			// todo: resolve beach/beach-nightmarket inconsistency

			Log.D($"Patching {count} secret bases into {name}");
			var path = _helper.Content.GetActualAssetKey(
				Path.Combine("Assets", "Maps", $"{Const.TilesheetId}.png"));
			var texture = _helper.Content.Load<Texture2D>(path);

			// Add secret base tilesheet
			var tilesheet = new TileSheet(Const.TilesheetId, map, path,
				new Size(texture.Width / 16, texture.Height / 16),
				new Size(16, 16));
			map.AddTileSheet(tilesheet);
			map.LoadTileSheets(Game1.mapDisplayDevice);

			// Add secret base entries for this map on an extra layer
			var layer = map.GetLayer("Buildings");
			layer = new Layer(Const.ExtraLayerId, map, layer.LayerSize, layer.TileSize);

			const int frameInterval = 150;
			const BlendMode blend = BlendMode.Additive;
			foreach (var baseLocation in Const.BaseEntryLocations.Where(_ => _.Value.Equals(name)))
			{
				var coords = Const.BaseEntryCoordinates[baseLocation.Key];
				var row = tilesheet.SheetWidth;

				// todo: patch in inactive entries

				if (baseLocation.Key.Contains("Tree"))
				{
					// exactly two (2) animated tiles
					var index = row;
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
				}
				else if (baseLocation.Key.Contains("Rock"))
				{
					var index = row * 3;
					if (map.GetLayer("Buildings").Tiles[(int) coords.X, (int) coords.Y + 1]?.TileIndex == 370)
						index = row * 5;
					layer.Tiles[(int)coords.X, (int)coords.Y] = new StaticTile(layer, tilesheet, blend, index);
					layer.Tiles[(int)coords.X, (int)coords.Y + 1] = new StaticTile(layer, tilesheet, blend, index + row);
				}
				else if (baseLocation.Key.Contains("Bush"))
				{
					var index = row * 7;
					layer.Tiles[(int)coords.X, (int)coords.Y] = new StaticTile(layer, tilesheet, blend, index);
					index += row;
					if (map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y] == null)
						layer.Tiles[(int)coords.X, (int)coords.Y + 1] = new StaticTile(layer, tilesheet, blend, index);
					else
						map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = new StaticTile(layer, tilesheet, blend, index);
				}

				// Enable player interactions
				map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1].Properties["Action"] = Const.BaseEntryAction;
			}

			// Draw the extra layer above Buildings layer
			layer.Properties["DrawAbove"] = "Buildings";
			map.AddLayer(layer);

			// PyTK stolen compatibility
			if (layer.Properties.ContainsKey("Draw") && map.GetLayer(layer.Properties["Draw"]) is Layer maplayer)
				maplayer.AfterDraw += (s, e) => DrawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));
			else if (layer.Properties.ContainsKey("DrawAbove") && map.GetLayer(layer.Properties["DrawAbove"]) is Layer maplayerAbove)
				maplayerAbove.AfterDraw += (s, e) => DrawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));
			else if (layer.Properties.ContainsKey("DrawBefore") && map.GetLayer(layer.Properties["DrawBefore"]) is Layer maplayerBefore)
				maplayerBefore.BeforeDraw += (s, e) => DrawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));

			layer.Properties["DrawChecked"] = true;
		}

		/* PyTK stolen compatibility */

		public static void DrawLayer(Layer layer, Location offset, bool wrap = false)
		{
			if (Game1.currentLocation is GameLocation location && location.map is Map map &&
			    !map.Layers.Contains(layer))
				return;
			DrawLayer(layer, Game1.mapDisplayDevice, Game1.viewport, Game1.pixelZoom, offset, wrap);
		}

		public static void DrawLayer(Layer layer, IDisplayDevice device, Rectangle viewport, int pixelZoom,
			Location offset, bool wrap = false)
		{
			if (layer.Properties.ContainsKey("offsetx") && layer.Properties.ContainsKey("offsety"))
			{
				offset = new Location(int.Parse(layer.Properties["offsetx"]), int.Parse(layer.Properties["offsety"]));
				if (!layer.Properties.ContainsKey("OffestXReset"))
				{
					layer.Properties["OffestXReset"] = offset.X;
					layer.Properties["OffestYReset"] = offset.Y;
				}
			}

			if (!layer.Properties.ContainsKey("StartX"))
			{
				var local = Game1.GlobalToLocal(new Vector2(offset.X, offset.Y));
				layer.Properties["StartX"] = local.X;
				layer.Properties["StartY"] = local.Y;
			}

			if (layer.Properties.ContainsKey("AutoScrollX"))
			{
				var ax = layer.Properties["AutoScrollX"].ToString().Split(',');
				var cx = int.Parse(ax[0]);
				var mx = 1;
				if (ax.Length > 1)
					mx = int.Parse(ax[1]);

				if (cx < 0)
					mx *= -1;

				if (Game1.currentGameTime.TotalGameTime.Ticks % cx == 0)
					offset.X += mx;
			}

			if (layer.Properties.ContainsKey("AutoScrollY"))
			{
				var ay = layer.Properties["AutoScrollY"].ToString().Split(',');
				var cy = int.Parse(ay[0]);
				var my = 1;
				if (ay.Length > 1)
					my = int.Parse(ay[1]);

				if (cy < 0)
					my *= -1;

				if (Game1.currentGameTime.TotalGameTime.Ticks % cy == 0)
					offset.Y += my;
			}

			layer.Properties["offsetx"] = offset.X;
			layer.Properties["offsety"] = offset.Y;

			if (layer.Properties.ContainsKey("tempOffsetx") && layer.Properties.ContainsKey("tempOffsety"))
				offset = new Location(int.Parse(layer.Properties["tempOffsetx"]), int.Parse(layer.Properties["tempOffsety"]));
			
			layer.Draw(device, viewport, offset, wrap, pixelZoom);
		}
	}
}
