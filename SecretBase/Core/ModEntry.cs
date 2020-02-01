using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Menus;
using StardewValley.Tools;

using Microsoft.Xna.Framework;
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
		internal static ModState ModState;

		internal Config Config;
		internal ITranslationHelper i18n => Helper.Translation;

		internal Dictionary<long, Chest> GlobalStorage = new Dictionary<long, Chest>();
		
		public override void Entry(IModHelper helper)
		{
			Instance = this;

			Config = helper.ReadConfig<Config>();
			
			helper.Events.Input.ButtonReleased += OnButtonReleased;
			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.GameLoop.Saved += OnSaved;
			helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			helper.Events.GameLoop.Saving += OnSaving;

			// todo: block guests from interacting with furniture in other bases
			//helper.Events.Player.Warped += OnWarped;
		}

		#region Game Events

		private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			Helper.Content.AssetEditors.Add(new Editors.WorldEditor(Helper));
		}

		private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			AddReturnWarps();
			LoadModState();
		}

		private void OnSaving(object sender, SavingEventArgs e)
		{
			SaveModState();
		}

		private void OnSaved(object s, EventArgs e)
		{
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

			/* Debug hotkeys */
			if (!Config.DebugMode)
				return;
			if (e.Button.Equals(Config.DebugWarpBaseKey))
				DebugWarpBase();
			if (e.Button.Equals(Config.DebugWarpHomeKey))
				DebugWarpHome();
			if (e.Button.Equals(Config.DebugSaveStateKey))
				DebugSaveState();
			if (e.Button.Equals(Config.DebugFillStorageKey))
				DebugFillStorage();
		}

		private void OnWarped(object sender, WarpedEventArgs e)
		{
			/*
			if (_maps.Contains(e.OldLocation.Name))
			{
			}
			if (_maps.Contains(e.NewLocation.Name))
			{

			}
			*/
		}

		#endregion
		
		#region Public Getter Methods

		/// <returns>Returns whether a player meets the criteria to view the secret base entry prompts.</returns>
		public static bool CanFarmerHaveSecretBase(Farmer who, string whichBase = null)
		{
			// Players not currently holding the correct tool won't receive the activation prompt.
			if (whichBase != null)
				if (GetAppropriateToolForTheme(who, GetSecretBaseTheme(whichBase)) == null)
					return false;

			// todo: determine conditions before farmer can have certain interactions

			return true;
		}

		/// <summary>
		/// Visual changes in the world are tied to the theming of each base.
		/// A rock base appears on certain layers with certain tiles, and creates
		/// specific visual and sound effects on use, for example.
		/// </summary>
		/// <returns>Returns the visual theming of a secret base.</returns>
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
		
		/// <returns>Returns the secret base owned by a player.</returns>
		public static string GetSecretBaseForFarmer(Farmer who)
		{
			return ModState.SecretBaseOwnership.FirstOrDefault(b => b.Value.Equals(who.UniqueMultiplayerID)).Key;
		}

		/// <returns>Returns the location name for some secret base.</returns>
		public static string GetSecretBaseLocationForFarmer(Farmer who)
		{
			return Const.BaseEntryLocations.FirstOrDefault(a => a.Key.Equals(GetSecretBaseForFarmer(who))).Value;
		}

		/// <returns>Returns the map coordinates for some secret base in a nonspecific location.</returns>
		public static Vector2 GetSecretBaseCoordinatesForFarmer(Farmer who)
		{
			return Const.BaseEntryCoordinates.FirstOrDefault(a => a.Key.Equals(GetSecretBaseForFarmer(who))).Value;
		}

		/// <returns>Returns overworld entry coordinates for a secret base.
		/// Coordinates provided are for the tile above the interactable tile.</returns>
		public static Vector2 GetSecretBaseCoordinates(string whichBase)
		{
			return Const.BaseEntryCoordinates[whichBase];
		}
		
		/// <returns>Returns the name of the secret base nearest to the given coordinates.</returns>
		public static string GetNearestSecretBase(Vector2 coords)
		{
			var nearbyBases = Const.BaseEntryCoordinates.Where(
				_ => Const.BaseEntryLocations.ContainsKey(_.Key));
			return nearbyBases.OrderBy(
				_ => Math.Abs(_.Value.X - coords.X) + Math.Abs(_.Value.Y - coords.Y)).First().Key;
		}

		public static bool DoesAnyoneOwnSecretBase(string whichBase)
		{
			return ModState.SecretBaseOwnership.ContainsKey(whichBase);
		}

		public static bool DoesFarmerOwnSecretBase(Farmer who, string whichBase)
		{
			return DoesAnyoneOwnSecretBase(whichBase)
			       && ModState.SecretBaseOwnership[whichBase] == who.UniqueMultiplayerID;
		}

		/// <summary>
		/// Different tools are used to interact with the entries of secret bases
		/// from different visual themes.
		/// </summary>
		/// <returns>Returns the tool suited to this theme, if it exists.</returns>
		public static Item GetAppropriateToolForTheme(Farmer who, Const.Theme whichTheme)
		{
			if (whichTheme == Const.Theme.Tree || whichTheme == Const.Theme.Bush)
				return who.Items.FirstOrDefault(item =>
					item is MeleeWeapon weapon && weapon.InitialParentTileIndex == 47);
			if (whichTheme == Const.Theme.Rock || whichTheme == Const.Theme.Cave || whichTheme == Const.Theme.Desert)
				return who.getToolFromName("Pickaxe");
			return null;
		}

		#endregion

		#region Management Methods

		/// <summary>
		/// Patches functional return warps into the dummy properties of each secret base map file.
		/// </summary>
		private void AddReturnWarps()
		{
			foreach (var location in Game1.locations.Where(_ => _.Name.StartsWith(Const.ModId)))
			{
				// Update return warps
				var name = location.Name;
				var warp = ((string)location.Map.Properties["Warp"]).Split(' ');
				warp[2] = Const.BaseEntryLocations[name];
				warp[3] = GetSecretBaseCoordinates(name).X.ToString();
				warp[4] = (GetSecretBaseCoordinates(name).Y + 2f).ToString();
				location.Map.Properties["Warp"] = string.Join(" ", warp);
				location.updateWarps();
			}
		}
		
		/// <returns>Returns the filename of the mod data JSON for this savegame.</returns>
		private string GetDataFile()
		{
			return Path.Combine("data", string.Format(Const.DataFile, Constants.SaveFolderName));
		}

		/// <summary>
		/// Loads in the mod state from the data file for this savegame.
		/// </summary>
		private void LoadModState()
		{
			var farm = Game1.getLocationFromName("Farm"); // hehehehe
			var datafile = GetDataFile();
			ModState = Helper.Data.ReadJsonFile<ModState>(datafile) ?? new ModState();
			
			// Repopulate object storage
			if (GlobalStorage?.Count == 0)
			{
				foreach (var player in Game1.getAllFarmers())
				{
					var coords = Vector2.Zero;

					if (GlobalStorage.ContainsKey(player.UniqueMultiplayerID))
						// Use existing coordinates for chests already tied to the player
						coords = GlobalStorage[player.UniqueMultiplayerID].TileLocation;
					else
						// Use next coordinates across for chests being tied to new players
						coords = new Vector2(
							Const.DummyChestCoords + GlobalStorage.Count, 
							Const.DummyChestCoords);

					if (farm.Objects.ContainsKey(coords))
					{
						// Populate with existing chests
						var dummyChest = (Chest)farm.getObjectAtTile((int)coords.X, (int)coords.Y);
						GlobalStorage.Add(player.UniqueMultiplayerID, dummyChest);
					}
					else
					{
						// Populate with new chests
						var dummyChest = new Chest(
							playerChest: true, tileLocation: coords);
						farm.Objects.Add(coords, dummyChest);
						GlobalStorage.Add(player.UniqueMultiplayerID, dummyChest);
					}
				}
			}
		}

		/// <summary>
		/// Saves the current mod state for the next session.
		/// </summary>
		private void SaveModState()
		{
			var datafile = GetDataFile();
			Helper.Data.WriteJsonFile(datafile, ModState);
		}

		/// <summary>
		/// Player world interaction checks for tiles with custom routines.
		/// </summary>
		private void CheckForAction()
		{
			var grabTile = new Vector2(
				               Game1.getOldMouseX() + Game1.viewport.X, 
				               Game1.getOldMouseY() + Game1.viewport.Y)
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
				// Actions on each Base Entry building will open a contextual dialogue tree
				SecretBaseEntryDialogue(Game1.player, grabTile);

			else if (strArray[0].Equals(Const.LaptopAction))
				// Actions on the Laptop building will open a dialogue tree
				LaptopRootDialogue();
		}

		#endregion

		#region Dialogue Methods

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
			var location = who.currentLocation;
			var whichBase = GetNearestSecretBase(actionCoords);
			var theme = GetSecretBaseTheme(whichBase);
			
			if (DoesFarmerOwnSecretBase(who, whichBase))
			{
				// todo: play entry sound effect for each secret base theme
				var sfx = "stairsdown";
				//if (theme == Const.Theme.Tree)
				//	sfx = "leafrustle";
				// . . .
				location.playSound(sfx);

				// Warp the player inside the secret base.
				var dest = ((string)Game1.getLocationFromName(whichBase).Map.Properties["Warp"])
					.Split(' ');
				who.warpFarmer(new Warp(0, 0, whichBase, 
					int.Parse(dest[0]), int.Parse(dest[1]) - 1, false));
			}
			else if (DoesAnyoneOwnSecretBase(whichBase))
			{
				// todo: if this base already owned, offer entry confirm/deny to the owner
				//		(just once/always/not today/never
				
			}
			else
			{
				// Popup an inspection dialogue for unowned bases deciding on a question dialogue
				var dialogue = new List<string>{ i18n.Get("entry.treeprompt") };
				var options = new List<Response>();

				if (theme == Const.Theme.Rock || theme == Const.Theme.Desert || theme == Const.Theme.Cave)
					dialogue[0] = i18n.Get("entry.caveprompt");
				else if (theme == Const.Theme.Bush)
					dialogue[0] = i18n.Get("entry.bushprompt");

				if (GetSecretBaseForFarmer(who) == null)
				{
					// Prompt to take control of this base
					if (CanFarmerHaveSecretBase(who, whichBase))
					{
						dialogue.Add(i18n.Get("entry.activateprompt"));
						options.Add(new Response("activate", i18n.Get("dialogue.y")));
						options.Add(new Response("cancel", i18n.Get("dialogue.n")));
						CreateMultipleDialogueQuestion(dialogue, options, location, DialogueAnswers);
					}
				}
				else
				{
					// Prompt to abandon current base and take control
					if (CanFarmerHaveSecretBase(who, whichBase))
					{
						dialogue.Add(i18n.Get("entry.relocateprompt"));
						options.Add(new Response("packprompt", i18n.Get("dialogue.y")));
						options.Add(new Response("cancel", i18n.Get("dialogue.n")));
						CreateMultipleDialogueQuestion(dialogue, options, location, DialogueAnswers);
					}
					else
					{
						// Offer no prompt, only an inspection dialogue
						Game1.drawDialogueNoTyping(dialogue[0]);
					}
				}
			}
		}

		/// <summary>
		/// Creates a hybrid dialogue box using features of multipleDialogue and questionDialogue.
		/// A series of dialogues is presented, with the final dialogue having assigned responses.
		/// </summary>
		private void CreateMultipleDialogueQuestion(List<string> dialogues, List<Response> answerChoices,
			GameLocation where, GameLocation.afterQuestionBehavior afterDialogueBehavior)
		{
			where.afterQuestion = afterDialogueBehavior;
			Game1.activeClickableMenu = new MultipleDialogueQuestion(Helper, dialogues, answerChoices);
			Game1.dialogueUp = true;
			Game1.player.canMove = false;
		}
		
		/// <summary>
		/// This method sequences question dialogue boxes by assigning them for the next tick,
		/// allowing for repeated calls to the afterDialogueBehavior field per location.
		/// </summary>
		private void OpenPackDialogueOnNextTick(object sender, UpdateTickedEventArgs e)
		{
			Log.D("OpenPackDialogueOnNextTick");
			Helper.Events.GameLoop.UpdateTicked -= OpenPackDialogueOnNextTick;
			SecretBasePackUpDialogue();
		}

		/// <summary>
		/// Presents a confirmation dialogue box for packing up and disassociating this player's secret base.
		/// </summary>
		private void SecretBasePackUpDialogue()
		{
			var location = Game1.player.currentLocation;
			var question = i18n.Get("laptop.packprompt");
			var options = new List<Response>
			{
				new Response("packup", i18n.Get("dialogue.y")),
				new Response("cancel", i18n.Get("dialogue.n")),
			};
			location.createQuestionDialogue(question, options.ToArray(), DialogueAnswers);
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
			var question = i18n.Get("laptop.menuprompt");
			var options = new List<Response>();

			if (GlobalStorage[Game1.player.UniqueMultiplayerID].items.Count > 0)
				options.Add(new Response("storage", i18n.Get("laptop.storage")));
			options.Add(new Response("packprompt", i18n.Get("laptop.packup")));
			options.Add(new Response("cancel", i18n.Get("dialogue.cancel")));

			location.createQuestionDialogue(question, options.ToArray(), DialogueAnswers);
		}

		/// <summary>
		/// Opens a fishing chest-style grab dialogue taking from
		/// personal global storage data into the player's inventory.
		/// </summary>
		private void LaptopStorageDialogue()
		{
			var chest = GlobalStorage[Game1.player.UniqueMultiplayerID];
			chest.clearNulls();
			Game1.activeClickableMenu = new ItemGrabMenu(
				chest.items)
			{
				behaviorOnItemGrab = delegate (Item item, Farmer who)
				{
					if (item != null)
						chest.items.Remove(item);
				}
			};
		}

		#endregion
		
		#region Secret Base Modifier Methods

		/// <summary>
		/// Marks the base closest to the player as active,
		/// and associates it with this player in mod data.
		/// </summary>
		private void SecretBaseActivate(Farmer who)
		{
			var nearestBase = GetNearestSecretBase(who.getTileLocation());

			ModState.SecretBaseOwnership[nearestBase] = who.UniqueMultiplayerID;
			SecretBaseActivationFx(who, nearestBase);
		}

		/// <summary>
		/// Adds temporary special effects in the overworld when a secret base is activated.
		/// Also removes the inactive secret base entry tiles and invalidates the cache,
		/// updating the location to reflect the new entry appearance and functionality.
		/// </summary>
		private void SecretBaseActivationFx(Farmer who, string whichBase)
		{
			var where = who.currentLocation;
			var coords = GetSecretBaseCoordinates(whichBase);
			var theme = GetSecretBaseTheme(whichBase);

			var sfx = "";
			var vfx = 0;
			var tool = GetAppropriateToolForTheme(who, theme);

			if (tool == null)
				return;

			// Player animation
			var lastTool = Game1.player.CurrentTool;
			Game1.player.CurrentTool = (Tool)tool;
			Game1.player.FireTool();
			Game1.player.CurrentTool = lastTool;

			switch (theme)
			{
				case Const.Theme.Tree:
				{
					sfx = "cut";
					vfx = 17 * 64;

					where.Map.GetLayer("Front").Tiles[(int)coords.X, (int)coords.Y] = null;
					where.Map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
				case Const.Theme.Rock:
				case Const.Theme.Desert:
				case Const.Theme.Cave:
				{
					sfx = "boulderBreak";
					vfx = 5 * 64;

					where.Map.GetLayer(Const.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y] = null;
					where.Map.GetLayer(Const.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
				case Const.Theme.Bush:
				{
					sfx = "leafrustle";
					vfx = 17 * 64;

					where.Map.GetLayer(Const.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y] = null;
					if (where.Map.GetLayer(Const.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] != null)
						where.Map.GetLayer(Const.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] = null;
					else
						where.Map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
			}

			// Sound effects
			if (!sfx.Equals(""))
				where.playSound(sfx);

			// Visual effects
			if (vfx != 0)
			{
				Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue().
					broadcastSprites(where, new TemporaryAnimatedSprite(
						@"TileSheets/animations",
						new Microsoft.Xna.Framework.Rectangle(0, vfx, 64, 64),
						100f,
						8,
						0,
						new Vector2(coords.X * 64, coords.Y * 64),
						false,
						false)
					{
						endFunction = delegate
						{
							// Update the map to reflect changes to the base entry tiles under new ownership
							Log.D($@"Invalidating cache for Maps/{Game1.player.currentLocation.Name}");
							Helper.Content.InvalidateCache($@"Maps/{Game1.player.currentLocation.Name}");
						}
					});
			}
		}

		/// <summary>
		/// Returns the interior of the secret base to its default inactive state
		/// and clears out all placed objects, saving them to global storage data.
		/// Will also warp out any players still inside.
		/// </summary>
		private void SecretBasePackUp(Farmer who)
		{
			var location = Game1.getLocationFromName(GetSecretBaseForFarmer(who));
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
			{
				if (o.GetType() != typeof(Chest))
					AddToStorage(who, o);
				else
				{
					// Deposit chest contents into storage
					foreach (var c in ((Chest) o).items)
						AddToStorage(who, c);
					AddToStorage(who, (Chest)o);
				}
			}
			location.objects.Clear();

			// todo: resolve decoratable location issues with locations added by TMXL
			/*
			foreach (var f in location.furniture)
				AddToStorage(who, f);
			location.furniture.Clear();
			*/

			// Mark the secret base as inactive, allowing it to be used by other players
			ModState.SecretBaseOwnership.Remove(GetSecretBaseForFarmer(who));
		}

		#endregion

		#region Debug Methods

		/// <summary>
		/// Add items to the global storage chest, bypassing the limit check.
		/// </summary>
		/// <param name="who"></param>
		/// <param name="item"></param>
		private void AddToStorage(Farmer who, Item item)
		{
			var chest = GlobalStorage[who.UniqueMultiplayerID];
			item.resetState();
			chest.clearNulls();
			foreach (var i in chest.items)
			{
				if (i != null && i.canStackWith(item))
				{
					item.Stack = i.addToStack(item);
					if (item.Stack <= 0)
						return;
				}
			}
			chest.items.Add(item);
		}
		
		/// <summary>
		/// Warps the player in front of a random secret base if none are owned,
		/// otherwise warps the player to their own base.
		/// </summary>
		private void DebugWarpBase()
		{
			var who = Game1.player;
			var locations = Game1.locations.Where(_ => _.Name.StartsWith(Const.ModId)).ToArray();
			var names = new string[locations.Length - 1];
			for (var i = 0; i < names.Length; ++i)
				names[i] = locations[i].Name;
			var whichBase = names[new Random().Next(names.Length - 1)];
			var where = Const.BaseEntryLocations[whichBase];
			var coords = Const.BaseEntryCoordinates[whichBase];

			whichBase = GetSecretBaseForFarmer(who);
			if (whichBase != null)
			{
				where = Const.BaseEntryLocations[whichBase];
				coords = Const.BaseEntryCoordinates[whichBase];
			}

			Log.D($"Warped {who.Name} to {where} at {coords.X}, {coords.Y}");

			who.warpFarmer(new Warp(0, 0, where, (int)coords.X, (int)coords.Y + 2, false));
		}

		/// <summary>
		/// Attempts to warp the player in front of the farmhouse bed.
		/// </summary>
		private void DebugWarpHome()
		{
			var who = Game1.player;
			var where = "Farmhouse";
			var coords = new Vector2(25, 12);

			Log.D($"Warped {who.Name} to {where} at {coords.X}, {coords.Y}");

			who.warpFarmer(new Warp(0, 0, where, (int)coords.X, (int)coords.Y, false));
		}

		private void DebugSaveState()
		{
			Log.D("Forced a saved state.");

			SaveModState();
		}

		/// <summary>
		/// Populates the player's global storage with some number of random items.
		/// </summary>
		private void DebugFillStorage()
		{
			var who = Game1.player;
			var random = new Random();
			var maxIndex = Game1.objectInformation.Count - 1;
			var count = 50;

			Log.D($"Populating {who.Name}'s storage with {count} items.");

			while(GlobalStorage[who.UniqueMultiplayerID].items.Count < count)
				AddToStorage(who, new StardewValley.Object(random.Next(maxIndex), 1));
		}

		#endregion
	}
}
