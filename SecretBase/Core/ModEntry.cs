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
			
			helper.Content.AssetEditors.Add(new Editors.WorldEditor(helper));

			helper.Events.Input.ButtonReleased += OnButtonReleased;
			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.GameLoop.Saved += OnSaved;
			helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			helper.Events.GameLoop.Saving += OnSaving;

			// todo: possibly have OnDayStarted check if the player is still able to have a base, and if not, do secretbasepackup
		}


		/* Game events */

		private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			LoadMaps();
		}
		
		private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			AddLocations();
			LoadModState();
		}

		private void OnSaving(object sender, SavingEventArgs e)
		{
			SaveModState();
			RemoveLocations();
		}

		private void OnSaved(object s, EventArgs e)
		{
			AddLocations();
			LoadModState();
		}

		private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
		{
			if (Game1.activeClickableMenu != null || Game1.player.UsingTool || Game1.pickingTool || Game1.menuUp
			    || (Game1.eventUp && !Game1.currentLocation.currentEvent.playerControlSequence)
			    || Game1.nameSelectUp || Game1.numberOfSelectedItems != -1)
				return;

			if (e.Button.IsActionButton())
				CheckForAction();

			if (Config.DebugMode && e.Button.Equals(Config.DebugWarpBaseKey))
				DebugWarpBase();

			if (Config.DebugMode && e.Button.Equals(Config.DebugWarpHomeKey))
				DebugWarpHome();
		}


		/* Public methods */

		public static bool CanFarmerHaveSecretBase(Farmer who)
		{
			// todo: determine conditions before farmer can have certain interactions

			return true;
		}

		public static Const.Theme GetSecretBaseTheme(string whichBase)
		{
			var theme = Const.Theme.Tree;
			
			if (whichBase.Contains("R"))
				theme = Const.Theme.Rock;
			else if (whichBase.Contains("Bu"))
				theme = Const.Theme.Bush;
			else if (whichBase.Contains("D"))
				theme = Const.Theme.Desert;
			else if (whichBase.Contains("C"))
				theme = Const.Theme.Cave;

			return theme;
		}

		public static string GetSecretBaseForFarmer(Farmer who)
		{
			return ModData.SecretBaseOwnership.FirstOrDefault(b => b.Value.Equals(who.UniqueMultiplayerID)).Key;
		}

		public static Vector2 GetSecretBaseCoordinatesForFarmer(Farmer who)
		{
			return Const.BaseEntryCoordinates.FirstOrDefault(a => a.Key.Equals(GetSecretBaseForFarmer(who))).Value;
		}

		public static Vector2 GetSecretBaseCoordinates(string whichBase)
		{
			return Const.BaseEntryCoordinates[whichBase];
		}

		public static string GetNearestSecretBase(Vector2 coords)
		{
			var nearbyBases = Const.BaseEntryCoordinates.Where(
				_ => Const.BaseEntryLocations.ContainsKey(_.Key));
			return nearbyBases.OrderBy(
				_ => Math.Abs(_.Value.X - coords.X) + Math.Abs(_.Value.Y - coords.Y)).First().Key;
		}

		public static bool DoesAnyoneOwnSecretBase(string whichBase)
		{
			return ModData.SecretBaseOwnership.ContainsKey(whichBase);
		}

		public static bool DoesFarmerOwnSecretBase(Farmer who, string whichBase)
		{
			return DoesAnyoneOwnSecretBase(whichBase)
			       && ModData.SecretBaseOwnership[whichBase] == who.UniqueMultiplayerID;
		}


		/* Private methods */
		
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
					var name = Path.GetFileNameWithoutExtension(map);
					var mapAssetKey = Helper.Content.GetActualAssetKey(
						$@"Assets/Maps/{map}");
					var loc = new DecoratableLocation(
						mapAssetKey,
						name);

					// Tag maps to this mod
					if (!loc.Map.Properties.ContainsKey(Const.ModId))
						loc.Map.Properties.Add(Const.ModId, true);

					// Update return warps
					var warp = ((string) loc.Map.Properties["Warp"]).Split(' ');
					warp[2] = Const.BaseEntryLocations[name];
					warp[3] = GetSecretBaseCoordinates(name).X.ToString();
					warp[4] = (GetSecretBaseCoordinates(name).Y + 2f).ToString();
					loc.Map.Properties["Warp"] = string.Join(" ", warp);
					loc.updateWarps();

;					// Add new maps to game locations
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

		/// <summary>
		/// Loads in the mod state from the data file for this savegame.
		/// </summary>
		private void LoadModState()
		{
			var datafile = string.Format(Const.DataFile, Game1.GetSaveGameName());
			ModData = Helper.Data.ReadJsonFile<ModData>(datafile) ?? new ModData();
			
			// todo: load furniture and objects from saved mod data

			if (ModData.GlobalStorage?.Count == 0)
				foreach (var player in Game1.getAllFarmers())
					ModData.GlobalStorage.Add(player.UniqueMultiplayerID, new Chest(playerChest:true));
		}

		/// <summary>
		/// Saves the current mod state for the next session.
		/// </summary>
		private void SaveModState()
		{
			var datafile = string.Format(Const.DataFile, Game1.GetSaveGameName());
			// . . ? ?
			// todo: save furniture and object contents of each secret base to be restored later

			Helper.Data.WriteJsonFile(datafile, ModData);
		}

		/// <summary>
		/// Player world interaction checks for tiles with custom routines.
		/// </summary>
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

		/// <summary>
		/// Control flow for all question dialogues in the mod.
		/// </summary>
		/// <param name="answer">Player's answer to the last question dialogue.</param>
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
				Helper.Events.GameLoop.UpdateTicked += OpenPackDialogueOnNextTick;

			// Laptop packup dialogue
			else if (ans.Equals("packup"))
				SecretBasePackUp(who);
		}

		/// <summary>
		/// Contextual dialogue menu for players interacting with secret base entry tiles.
		/// Allows players to claim, activate, and enter bases in the overworld.
		/// </summary>
		private void SecretBaseEntryDialogue(Farmer who, Vector2 actionCoords)
		{
			var whichBase = GetNearestSecretBase(actionCoords);
			var theme = GetSecretBaseTheme(whichBase);

			var options = new List<Response>();
			var question = i18n.Get("laptop.menuprompt");

			if (DoesFarmerOwnSecretBase(who, whichBase))
			{
				// todo: play entry sound effect for all secret base themes
				var sfx = "stairsdown";
				//if (theme == Const.Theme.Tree)
				//	sfx = "leafrustle";
				// . . .
				who.currentLocation.playSound(sfx);

				// Warp the player inside the secret base.
				var dest = ((string)Game1.getLocationFromName(whichBase).Map.Properties["Warp"]).Split(' ');
				who.warpFarmer(new Warp(0, 0, whichBase, 
					int.Parse(dest[0]), int.Parse(dest[1]) - 1, false));
			}
			else if (DoesAnyoneOwnSecretBase(whichBase))
			{
				// todo: if this base already owned, offer entry confirm/deny to the owner
				//		(just once/always/not today/never

				// todo: if this base not owned, prompt for packup or cancel

				// todo: block guests from interacting with furniture in other bases
			}
			else
			{
				// Popup an inspection dialogue for unowned bases before the question dialogue

				// todo: queue up the inspection dialogue before the question dialogue

				var inspectDialogue = i18n.Get("entry.treeprompt");
				if (theme == Const.Theme.Rock || theme == Const.Theme.Desert || theme == Const.Theme.Cave)
					inspectDialogue = i18n.Get("entry.caveprompt");
				else if (theme == Const.Theme.Bush)
					inspectDialogue = i18n.Get("entry.bushprompt");
				Game1.drawDialogueNoTyping(inspectDialogue);

				if (GetSecretBaseForFarmer(who) == null)
				{
					// Prompt to take control of this base
					if (CanFarmerHaveSecretBase(who))
					{
						question = i18n.Get("entry.activateprompt");
						options.Add(new Response("activate", i18n.Get("dialogue.y")));
						options.Add(new Response("cancel", i18n.Get("dialogue.n")));
					}
				}
				else
				{
					// Prompt to abandon current base and take control
					if (CanFarmerHaveSecretBase(who))
					{
						question = i18n.Get("entry.relocateprompt");
						options.Add(new Response("packprompt", i18n.Get("dialogue.y")));
						options.Add(new Response("cancel", i18n.Get("dialogue.n")));
					}
				}
			}

			if (options.Count > 0)
				who.currentLocation.createQuestionDialogue(
					question, options.ToArray(), DialogueAnswers);
		}

		/// <summary>
		/// Marks the base closest to the player as active,
		/// and associates it with this player in mod data.
		/// </summary>
		private void SecretBaseActivate(Farmer who)
		{
			var nearestBase = GetNearestSecretBase(who.getTileLocation());

			ModData.SecretBaseOwnership[nearestBase] = who.UniqueMultiplayerID;
			SecretBaseActivationFX(who, nearestBase);

			Log.D($@"Invalidating cache for Maps/{Game1.player.currentLocation.Name}");
			Helper.Content.InvalidateCache($@"Maps/{Game1.player.currentLocation.Name}");
		}

		/// <summary>
		/// Adds temporary fx in the overworld when a secret base is activated.
		/// </summary>
		private void SecretBaseActivationFX(Farmer who, string whichBase)
		{
			var coords = GetSecretBaseCoordinates(whichBase);
			var theme = GetSecretBaseTheme(whichBase);

			var sfx = "";
			var vfx = 0;
			var tool = (Item)null;

			switch (theme)
			{
				case Const.Theme.Tree:
				{
					sfx = "cut";
					vfx = 1088;
					tool = Game1.player.Items.FirstOrDefault(item =>
						item is MeleeWeapon weapon && weapon.InitialParentTileIndex == 47);
					break;
				}
				case Const.Theme.Rock:
				case Const.Theme.Desert:
				case Const.Theme.Cave:
				{
					sfx = "boulderBreak";
					// vfx = . . .
					// tool = . . .
					break;
				}
				case Const.Theme.Bush:
				{
					sfx = "leafrustle";
					vfx = 1088;
					tool = Game1.player.Items.FirstOrDefault(item =>
						item is MeleeWeapon weapon && weapon.InitialParentTileIndex == 47);
					break;
				}
			}

			// Sound effects
			if (!sfx.Equals(""))
				who.currentLocation.playSound(sfx);

			// Visual effects
			if (vfx != 0)
				Game1.player.currentLocation.TemporarySprites.Add(
					new TemporaryAnimatedSprite(
						@"TileSheets/animations",
						new Microsoft.Xna.Framework.Rectangle(0, vfx, 64, 64),
						100f,
						8,
						0,
						new Vector2(coords.X * 64, coords.Y * 64),
						false,
						false));

			// Player animation
			if (tool != null)
			{
				var lastTool = Game1.player.CurrentTool;
				Game1.player.CurrentTool = (Tool)tool;
				Game1.player.FireTool();
				Game1.player.CurrentTool = lastTool;
			}
		}

		/* Chains question dialogue prompts by delivering them on the next tick. */
		private void OpenPackDialogueOnNextTick(object sender, UpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.UpdateTicked -= OpenPackDialogueOnNextTick;
			LaptopPackUpDialogue();
		}

		// todo: add laptop menu for allowed/blocked players if this farm has farmhands
		// their name, their portrait, their base location if visited, allow/block menu

		/// <summary>
		/// Root dialogue menu for player interactions with the laptop in the secret base.
		/// Offers base management options and item management from storage.
		/// </summary>
		private void LaptopRootDialogue()
		{
			var location = Game1.player.currentLocation;
			var options = new List<Response>();
			if (ModData.GlobalStorage[Game1.player.UniqueMultiplayerID].items.Count > 0)
				options.Add(new Response("storage", i18n.Get("laptop.storage")));
			options.Add(new Response("packprompt", i18n.Get("laptop.packup")));
			options.Add(new Response("cancel", i18n.Get("dialogue.cancel")));

			location.createQuestionDialogue(
				i18n.Get("laptop.menuprompt"), options.ToArray(), DialogueAnswers);
		}

		/// <summary>
		/// Additional question dialogue for confirming secret base deactivation.
		/// </summary>
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
			var location = (DecoratableLocation) Game1.getLocationFromName(GetSecretBaseForFarmer(who));
			var coords = GetSecretBaseCoordinates(location.Name);

			Log.D($"Packing up {location.Name}");

			// Invalidate the base entry map cache to remove the activated sprite
			Log.D($@"Invalidating cache for Maps/{location.Name}");
			Helper.Content.InvalidateCache($@"Maps/{location.Name}");

			// Evict all players from the secret base
			foreach (var player in Game1.getAllFarmers().Where(_ => _.currentLocation.Equals(location)))
				player.warpFarmer(new Warp(0, 0,
					Const.BaseEntryLocations[location.Name],
					(int)coords.X,
					(int)coords.Y + 2,
					false));

			// Remove all placed objects from the map and save them to the global storage data model
			foreach (var o in location.objects.Values)
				ModData.GlobalStorage[who.UniqueMultiplayerID].addItem(o);
			location.objects.Clear();

			foreach (var f in location.furniture)
				ModData.GlobalStorage[who.UniqueMultiplayerID].addItem(f);
			location.furniture.Clear();

			// Mark the secret base as inactive, allowing it to be used by other players
			ModData.SecretBaseOwnership.Remove(GetSecretBaseForFarmer(who));
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

		/// <summary>
		/// Clears items from global storage when picked from the laptop storage prompt.
		/// </summary>
		public void OnFurnitureGrabbed(Item item, Farmer who)
		{
			if (item != null)
				ModData.GlobalStorage[Game1.player.UniqueMultiplayerID].items.Remove(item);
		}

		/// <summary>
		/// Warps the player in front of a random secret base.
		/// </summary>
		private void DebugWarpBase()
		{
			var who = Game1.player;
			var whichBase = Path.GetFileNameWithoutExtension(_maps[new Random().Next(_maps.Count - 1)]);
			var where = Const.BaseEntryLocations[whichBase];
			var coords = Const.BaseEntryCoordinates[whichBase];

			whichBase = GetSecretBaseForFarmer(who);
			if (whichBase != null)
			{
				where = Const.BaseEntryLocations[whichBase];
				coords = Const.BaseEntryCoordinates[whichBase];
			}

			who.warpFarmer(new Warp(0, 0, where, (int)coords.X, (int)coords.Y + 2, false));
			Log.D($"Warped {who} to {where} at {coords.X}, {coords.Y}");
		}

		private void DebugWarpHome()
		{
			Game1.player.warpFarmer(new Warp(0, 0, "Farmhouse", 20, 5, false));
		}
	}
}
