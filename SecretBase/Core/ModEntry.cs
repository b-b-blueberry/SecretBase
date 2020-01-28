using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Tools;

using Microsoft.Xna.Framework;
using xTile;
using xTile.Dimensions;
using xTile.ObjectModel;

namespace SecretBase
{
	/* todo:
		linus event with introduction to opening secret bases
		vincent dialogue with your secret base location
	*/

	public class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal Config Config;
		internal ITranslationHelper i18n => Helper.Translation;

		internal static ModData ModData;

		private List<string> _maps;

		public override void Entry(IModHelper helper)
		{
			Instance = this;

			Config = helper.ReadConfig<Config>();

			helper.Content.AssetEditors.Add(new Editors.TestEditor());

			helper.Content.AssetEditors.Add(new Editors.WorldEditor(helper));
			helper.Events.Input.ButtonReleased += OnButtonReleased;
			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.GameLoop.Saved += OnSaved;
			helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			helper.Events.GameLoop.Saving += OnSaving;
		}

		private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			LoadMaps();
		}
		
		private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			AddLocations();
			LoadStorage();
		}
		
		private void OnSaved(object s, EventArgs e)
		{
			AddLocations();
			SaveStorage();
		}

		private void OnSaving(object sender, SavingEventArgs e)
		{
			RemoveLocations();
		}

		private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
		{
			if (Game1.activeClickableMenu != null || Game1.player.UsingTool || Game1.pickingTool || Game1.menuUp
			    || (Game1.eventUp && !Game1.currentLocation.currentEvent.playerControlSequence)
			    || Game1.nameSelectUp || Game1.numberOfSelectedItems != -1)
				return;

			if (e.Button.IsActionButton())
				CheckForAction();

			if (Config.DebugMode && e.Button.Equals(Config.DebugWarpKey))
				DebugWarp();
		}

		private void LoadMaps()
		{
			var maps = new List<string>();
			foreach (var file in Directory.EnumerateFiles(
				Path.Combine(Helper.DirectoryPath, "Assets", "Maps")))
			{
				var ext = Path.GetExtension(file);
				if (ext == null || !ext.Equals(".tbin"))
					continue;
				var map = Path.GetFileName(file);
				if (map == null)
					continue;
				try
				{
					Helper.Content.Load<Map>($@"Assets/Maps/{map}");
					maps.Add(map);
				}
				catch (Exception ex)
				{
					Log.E($"Unable to load {map}.\n" + ex);
				}
			}
			_maps = maps;
		}

		private void AddLocations()
		{
			foreach (var map in _maps)
			{
				try
				{
					var mapAssetKey = Helper.Content.GetActualAssetKey(
						$@"Assets/Maps/{map}");
					var loc = new DecoratableLocation(
						mapAssetKey,
						Path.GetFileNameWithoutExtension(map));

					// Tag maps.
					if (!loc.Map.Properties.ContainsKey(Const.ModId))
						loc.Map.Properties.Add(Const.ModId, true);

					// Add return warps.
					loc.Map.Properties["Warp"] += $" {Const.BaseEntryLocations[map]}"
					                          + $" {Const.BaseEntryCoordinates[map].X}"
					                          + $" {Const.BaseEntryCoordinates[map].Y}";

					// Add new maps to game locations.
					Game1.locations.Add(loc);
				}
				catch (Exception ex)
				{
					Log.E($"Failed to add {map}\n" + ex);
				}
			}
		}

		private void RemoveLocations()
		{
			foreach (var location in Game1.locations.Where(
				_ => _.map.Properties.ContainsKey(Const.ModId)).ToArray())
				Game1.locations.Remove(location);
		}
		
		private void LoadStorage()
		{
			var datafile = string.Format(Const.DataFile, Game1.GetSaveGameName());
			ModData = Helper.Data.ReadJsonFile<ModData>(datafile) ?? new ModData();
			
			if (ModData.GlobalStorage?.Count == 0)
				foreach (var player in Game1.getAllFarmers())
					ModData.GlobalStorage.Add(player.UniqueMultiplayerID, new Chest(playerChest:true));
		}

		private void SaveStorage()
		{
			var datafile = string.Format(Const.DataFile, Game1.GetSaveGameName());
			// . . ? ?
			Helper.Data.WriteJsonFile(datafile, ModData);
		}

		private void CheckForAction()
		{
			var grabTile = new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y)
			               / Game1.tileSize;
			if (!Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
				grabTile = Game1.player.GetGrabTile();
			var tile = Game1.currentLocation.map.GetLayer("Buildings").PickTile(new Location(
				(int)grabTile.X * Game1.tileSize, (int)grabTile.Y * Game1.tileSize), Game1.viewport.Size);

			var action = (PropertyValue)null;
			tile?.Properties.TryGetValue("Action", out action);
			if (action == null)
				return;

			var strArray = ((string)action).Split(' ');
			var args = new string[strArray.Length - 1];
			Array.Copy(strArray, 1, args, 0, args.Length);

			if (strArray[0].Equals(Const.BaseEntryAction))
				// Actions on each Base Entry building will open a contextual dialogue tree.
				SecretBaseEntryDialogue(Game1.player, grabTile);
			else if (strArray[0].Equals(Const.LaptopAction))
				// Actions on the Laptop building will open a dialogue tree.
				LaptopRootDialogue();
		}

		private void DialogueAnswers(Farmer who, string answer)
		{
			var ans = answer.Split(' ')[0];
			
			// Secret base entry dialogue
			if (ans.Equals("activate"))
				SecretBaseActivate(who);

			// Laptop root dialogue
			else if (ans.Equals("storage"))
				LaptopStorageDialogue();
			else if (ans.Equals("packprompt"))
				Helper.Events.GameLoop.OneSecondUpdateTicked += HahahaHohoho;

			// Laptop packup dialogue
			else if (ans.Equals("packup"))
				SecretBasePackUp(who);
		}

		public static string GetSecretBaseForFarmer(Farmer who)
		{
			return ModData.SecretBaseOwnership.FirstOrDefault(b => b.Value.Equals(who.UniqueMultiplayerID)).Key;
		}

		public static Vector2 GetSecretBaseCoordinatesForFarmer(Farmer who)
		{
			return Const.BaseEntryCoordinates.FirstOrDefault(a => a.Key.Equals(GetSecretBaseForFarmer(who))).Value;
		}

		private void SecretBaseEntryDialogue(Farmer who, Vector2 tilePos)
		{
			var whichBase = GetSecretBaseForFarmer(who);
			var coords = GetSecretBaseCoordinatesForFarmer(who);
			var options = new List<Response>();

			if (whichBase == null)
			{
				options.Add(new Response("activate", i18n.Get("entry.activate")));
			}
			else if (ModData.SecretBaseOwnership[whichBase] == who.UniqueMultiplayerID)
			{
				// todo: play entry sound effect for all secret base themes
				//if (whichBase.Contains("Tree"))
					who.currentLocation.playSound("stairsdown");
				// . . .

				// Warp the player inside the secret base.
				var dest = ((string)Game1.getLocationFromName(whichBase).Map.Properties["Warp"]).Split(' ');
				who.warpFarmer(new Warp(0, 0, whichBase, 
					int.Parse(dest[0]), int.Parse(dest[1]) - 1, false));
			}
			else
			{
				// todo: if this base already owned, offer entry confirm/deny to the owner
				//		(just once/always/not today/never

				// todo: block guests from interacting with furniture in other bases
			}

			if (options.Count > 0)
				who.currentLocation.createQuestionDialogue(
					i18n.Get("laptop.menu"), options.ToArray(), DialogueAnswers);
		}

		/// <summary>
		/// Performs setup for secret bases in the overworld.
		/// </summary>
		private void SecretBaseActivate(Farmer who)
		{
			// Mark the base identified closest to the player as active, tying it to this player
			var nearbyBases = Const.BaseEntryCoordinates.Where(
				_ => Const.BaseEntryLocations.ContainsKey(_.Key));
			
			var nearestBase = nearbyBases.OrderBy(
				_ => Math.Abs(_.Value.X - who.getTileX()) + Math.Abs(_.Value.Y - who.getTileY())).First();
				
			ModData.SecretBaseOwnership[nearestBase.Key] = who.UniqueMultiplayerID;

			// todo: animate the farmer with a certain tool
			// todo: play tool-appropriate sound effect for all secret base themes
			if (nearestBase.Key.Contains("Tree") || nearestBase.Key.Contains("Bush"))
			{
				var scythe = Game1.player.Items.FirstOrDefault(item =>
					item is MeleeWeapon weapon && weapon.InitialParentTileIndex == 47);
				if (scythe != null)
				{
					var lastTool = Game1.player.CurrentTool;
					Game1.player.CurrentTool = (Tool)scythe;
					Game1.player.FireTool();
					Game1.player.CurrentTool = lastTool;
				}
			}
			// . . .
			
			// todo: play a dust puff effect (eg. coal from stone) for all secret base themes
			Game1.player.currentLocation.TemporarySprites.Add(
				new TemporaryAnimatedSprite(
					@"TileSheets/animations",
					new Microsoft.Xna.Framework.Rectangle(0, 1088, 64, 64),
					100f,
					8,
					0,
					new Vector2(nearestBase.Value.X * 64, nearestBase.Value.Y * 64),
					false,
					false));
			// . . .

			// todo: play terrain-appropriate sound effect for all secret base themes
			if (nearestBase.Key.Contains("Tree"))
				who.currentLocation.playSound("cut");
			else if (nearestBase.Key.Contains("Rock"))
				who.currentLocation.playSound("boulderBreak");
			else if (nearestBase.Key.Contains("Bush"))
				who.currentLocation.playSound("leafrustle");

			// . . .

			Log.D($@"Invalidating cache for Maps/{Game1.player.currentLocation.Name}");
			Helper.Content.InvalidateCache($@"Maps/{Game1.player.currentLocation.Name}");
		}
		
		private void HahahaHohoho(object sender, OneSecondUpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked -= HahahaHohoho;
			LaptopPackUpDialogue();
		}

		// todo: add laptop menu for allowed/blocked players, their name, their portrait, their base location if visited, allow/block menu

		private void LaptopRootDialogue()
		{
			var location = Game1.player.currentLocation;
			var options = new List<Response>();
			if (ModData.GlobalStorage[Game1.player.UniqueMultiplayerID].items.Count > 0)
				options.Add(new Response("storage", i18n.Get("laptop.storage")));
			options.Add(new Response("packprompt", i18n.Get("laptop.packup")));
			options.Add(new Response("cancel", i18n.Get("dialogue.cancel")));

			location.createQuestionDialogue(
				i18n.Get("laptop.menu"), options.ToArray(), DialogueAnswers);
		}

		private void LaptopPackUpDialogue()
		{
			var location = Game1.player.currentLocation;
			var options = new List<Response>
			{
				new Response("packup", i18n.Get("dialogue.y")),
				new Response("cancel", i18n.Get("dialogue.n")),
			};
			location.createQuestionDialogue(
				i18n.Get("laptop.packprompt"), options.ToArray(), DialogueAnswers);
		}

		/// <summary>
		/// Returns the interior of the secret base to its default inactive state
		/// and clears out all placed objects, saving them to global storage data.
		/// Will also warp out any players still inside.
		/// </summary>
		private void SecretBasePackUp(Farmer who)
		{
			var location = (DecoratableLocation) who.currentLocation;

			Log.D($"Packing up {location.Name}");

			// Invalidate the base entry map cache to remove the activated sprite
			Log.D($@"Invalidating cache for Maps/{Game1.player.currentLocation.Name}");
			Helper.Content.InvalidateCache($@"Maps/{Game1.player.currentLocation.Name}");

			// Evict all players from the secret base
			foreach (var player in Game1.getAllFarmers().Where(_ => _.currentLocation.Equals(location)))
				player.warpFarmer(new Warp(0, 0,
					Const.BaseEntryLocations[location.Name],
					(int)Const.BaseEntryCoordinates[location.Name].X,
					(int)Const.BaseEntryCoordinates[location.Name].Y + 1,
					false));

			// Remove all placed objects from the map and save them to the global storage data model
			foreach (var o in location.objects.Values)
				ModData.GlobalStorage[who.UniqueMultiplayerID].addItem(o);
			location.objects.Clear();

			foreach (var f in location.furniture)
				ModData.GlobalStorage[who.UniqueMultiplayerID].addItem(f);
			location.furniture.Clear();

			// Mark the secret base as inactive, allowing it to be used by other players
			ModData.SecretBaseOwnership[GetSecretBaseForFarmer(who)] = 0;
		}

		/// <summary>
		/// Opens a fishing chest-style grab dialogue taking from
		/// personal global storage data into the player's inventory.
		/// </summary>
		private void LaptopStorageDialogue()
		{
			Game1.activeClickableMenu = new ItemGrabMenu(
				ModData.GlobalStorage[Game1.player.UniqueMultiplayerID].items)
			{
				behaviorOnItemGrab = OnFurnitureGrabbed
			};
		}

		public void OnFurnitureGrabbed(Item item, Farmer who)
		{
			if (item != null)
				ModData.GlobalStorage[Game1.player.UniqueMultiplayerID].items.Remove(item);
		}

		private void DebugWarp()
		{
			var where = "SecretBaseTree0";
			Game1.player.warpFarmer(new Warp(0, 0, where, 7, 12, false));
		}
	}
}
