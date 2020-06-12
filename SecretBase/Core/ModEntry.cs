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

using SecretBase.ModMessages;

namespace SecretBase
{
	/* TODO: ASSETS: Event scripts:
		Linus event with introduction to opening secret bases
		Vincent dialogue with your secret base location
		Vincent event with secret base hole fixing
	*/

	// TODO: SYSTEM: Synchronise ModState changes between all players aaaaaa it's useless

	public class ModEntry : Mod
	{
		internal ITranslationHelper i18n => Helper.Translation;

		internal static ModEntry Instance { get; private set; }
		internal static ModState ModState { get; set; }
		internal Config Config { get; private set; }
		internal static Dictionary<long, Chest> GlobalStorage = new Dictionary<long, Chest>();
		
		internal static NotificationButton NotificationButton = new NotificationButton();
		internal List<Notification> PendingNotifications
			= new List<Notification>();

		public enum BaseStatus
		{
			Available,
			AvailableButNoTool,
			AvailableButNotToFarmer,
			AvailableButAlreadyOwnAnother,
			OwnedBySelf,
			OwnedByAnother,
			EntryDenied
		}

		public override void Entry(IModHelper helper)
		{
			Instance = this;

			Config = helper.ReadConfig<Config>();
			
			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			helper.Events.GameLoop.Saving += OnSaving;
			helper.Events.GameLoop.Saved += OnSaved;
			helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
			helper.Events.Input.ButtonReleased += OnButtonReleased;
			helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
			helper.Events.Player.Warped += OnWarped;

			if (Config.DebugMode)
			{
				AddConsoleCommands();
			}

			AddNotificationButton();
		}

		internal void AddNotificationButton()
		{
			if (Game1.onScreenMenus.Contains(NotificationButton))
				return;
			
			Log.W("Adding NotificationButton to HUD");
			Game1.onScreenMenus.Add(NotificationButton);
		}

		internal void RemoveNotificationButton()
		{
			if (!Game1.onScreenMenus.Contains(NotificationButton))
				return;

			Log.W("Removing NotificationButton from HUD");
			Game1.onScreenMenus.Remove(NotificationButton);
		}

		private void AddConsoleCommands() {
			Helper.ConsoleCommands.Add("sbsave", "Save the mod state, including all Secret Base info.",
				(s, p) =>
				{
					DebugSaveState();
				});
			Helper.ConsoleCommands.Add("sbname", "Print the name of the map for your Secret Base.",
				(s, p) =>
				{
					var who = Game1.player;
					var str = GetSecretBaseForFarmer(who);
					str = !string.IsNullOrEmpty(str)
						? $"{str} ({GetSecretBaseLocationForFarmer(who)} - {GetSecretBaseCoordinatesForFarmer(who)})"
						: "null";
					Log.D($"Currently own Secret Base: {str}");
				});
			Helper.ConsoleCommands.Add("sbpack", "Pack up and clear out your Secret Base.",
				(s, p) =>
				{
					var whichBase = GetSecretBaseForFarmer(Game1.player);
					Log.D($"Packing up Secret Base: {whichBase ?? "null"}");
					if (Game1.getLocationFromName(whichBase) is SecretBaseLocation secretBase)
						secretBase.PackUpAndShipOut(Game1.player);
				});
			Helper.ConsoleCommands.Add("sbfix", "Fix any holes in your Secret Base.",
				(s, p) =>
				{
					var whichBase = GetSecretBaseForFarmer(Game1.player);
					Log.D($"Fixing holes in Secret Base: {whichBase ?? "null"}");
					if (whichBase != null && Game1.getLocationFromName(whichBase) is SecretBaseLocation secretBase)
						secretBase.FixHoles(false);
				});
			Helper.ConsoleCommands.Add("sbreset", "Reset the map data for your Secret Base.",
				(s, p) =>
				{
					var whichBase = GetSecretBaseForFarmer(Game1.player);
					Log.D($"Resetting Secret Base: {whichBase ?? "null"}");
					if (Game1.getLocationFromName(whichBase) is SecretBaseLocation secretBase)
						secretBase.Reset();
				});
			Helper.ConsoleCommands.Add("sbwarp", "Warp your Secret Base or a random entry.",
				(s, p) =>
				{
					if (Game1.currentLocation.Name != GetSecretBaseLocationForFarmer(Game1.player))
						DebugWarpBase();
					else
						DebugWarpHome();
				});
			Helper.ConsoleCommands.Add("sbfill", "Fill your Global Storage with random items.",
				(s, p) =>
				{
					DebugFillStorage();
				});
			Helper.ConsoleCommands.Add("sbempty", "Empty out your Global Storage.",
				(s, p) =>
				{
					DebugClearStorage();
				});
			Helper.ConsoleCommands.Add("sbnotify", "Add 3 notifications to your inbox.",
				(s, p) =>
				{
					DebugAddRandomNotification();
				});
			Helper.ConsoleCommands.Add("sbclear", "Remove all notifications from your inbox.",
				(s, p) =>
				{
					PendingNotifications.Clear();
					Log.D($"Cleared notifications: {(PendingNotifications.Count <= 0 ? "true" : "false")}");
				});
			Helper.ConsoleCommands.Add("sbinbox", "Bring up the Notification Menu.",
				(s, p) =>
				{
					Game1.activeClickableMenu = new NotificationMenu();
				});
			Helper.ConsoleCommands.Add("sboutbox", "Close active menu.",
				(s, p) =>
				{
					Game1.activeClickableMenu = null;
				});
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
		
		private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			UnloadModState();
		}

		private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
		{
			if (Game1.activeClickableMenu != null || Game1.player.UsingTool || Game1.pickingTool || Game1.menuUp
			    || (Game1.eventUp && !Game1.currentLocation.currentEvent.playerControlSequence)
			    || Game1.nameSelectUp || Game1.numberOfSelectedItems != -1)
				return;

			if (e.Button.IsActionButton())
				CheckForAction();
		}

		private void SuppressInteractionButtons(object sender, ButtonPressedEventArgs e)
		{
			if (!e.Button.IsActionButton() && !e.Button.IsUseToolButton())
				return;

			Helper.Input.Suppress(e.Button);
		}

		private void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
		{
			if (e.FromModID != ModManifest.UniqueID || e.Type != Notification.MessageType)
				return;

			var notification = e.ReadAs<Notification>();
			switch (notification.Request)
			{
				case Notification.RequestCode.Requested:
					// TODO: METHOD: Entry requested behaviour
					break;

				case Notification.RequestCode.Allowed:
					// TODO: METHOD: Entry accepted behaviour:
					ModState.GuestListForPeerSecretBases[e.FromPlayerID] = notification;
					break;

				case Notification.RequestCode.Denied:
					// TODO: METHOD: Entry denial behaviour:
					ModState.GuestListForPeerSecretBases[e.FromPlayerID] = notification;
					break;
			}

			Log.D($"Received mail from {Game1.getFarmer(e.FromPlayerID)} ({e.FromPlayerID}):\n{notification.Summary}");

			AddNewPendingNotification(notification);
		}

		private void AddNewPendingNotification(Notification notification)
		{
			// Ignore duplicate notifications to prevent spam
			if (PendingNotifications.Exists(n
				=> n.Guest == notification.Guest
				   && n.Request == notification.Request))
				return;

			ModState.HasUnreadSecretMail = true;
			Game1.playSound("give_gift");
			Game1.showGlobalMessage(notification.Summary + ".");
			PendingNotifications.Add(notification);
			AddNotificationButton();
		}

		private void OnWarped(object sender, WarpedEventArgs e)
		{
			// Reenable player actions when leaving a peer's secret base
			if (ModConsts.BaseEntryCoordinates.ContainsKey(e.OldLocation.Name)
			    && DoesAnyoneOwnSecretBase(e.OldLocation.Name) != null
			    && DoesAnyoneOwnSecretBase(e.OldLocation.Name) != e.Player.UniqueMultiplayerID)
			{
				Helper.Events.Input.ButtonPressed -= SuppressInteractionButtons;
			}

			// Block players from actions when entering someone's secret base
			if (ModConsts.BaseEntryCoordinates.ContainsKey(e.NewLocation.Name)
				&& DoesAnyoneOwnSecretBase(e.NewLocation.Name) != null
				&& DoesAnyoneOwnSecretBase(e.NewLocation.Name) != e.Player.UniqueMultiplayerID)
			{
				Helper.Events.Input.ButtonPressed += SuppressInteractionButtons;
			}
		}
		
		#endregion
		
		#region Getter Methods

		// TODO: METHOD: Implement this method: GetConfigMapType()
		public static string GetConfigMapType()
		{
			if (!ModConsts.AcceptableMapTypes.Contains(Instance.Config.MapType))
				Instance.Config.MapType = ModConsts.AcceptableMapTypes[0];
			return Instance.Config.MapType;
		}

		/// <summary>
		/// Visual changes in the world are tied to the theming of each base.
		/// A rock base appears on certain layers with certain tiles, and creates
		/// specific visual and sound effects on use, for example.
		/// </summary>
		/// <returns>Returns the visual theming of a secret base.</returns>
		public static ModConsts.Theme GetSecretBaseTheme(string whichBase)
		{
			var theme = ModConsts.Theme.Tree;
			
			if (whichBase.Contains("R"))
				theme = ModConsts.Theme.Rock;
			else if (whichBase.Contains("Bu"))
				theme = ModConsts.Theme.Bush;
			else if (whichBase.Contains("D"))
				theme = ModConsts.Theme.Desert;
			else if (whichBase.Contains("C"))
				theme = ModConsts.Theme.Cave;

			return theme;
		}
		
		/// <returns>Returns the secret base owned by a player.</returns>
		public static string GetSecretBaseForFarmer(Farmer who)
		{
			return ModState.SecretBaseOwnership.FirstOrDefault(
				b => b.Value.Equals(who.UniqueMultiplayerID)).Key;
		}

		/// <returns>Returns the location name for some secret base.</returns>
		public static string GetSecretBaseLocationForFarmer(Farmer who)
		{
			return ModConsts.BaseEntryLocations.FirstOrDefault(
				a => a.Key.Equals(GetSecretBaseForFarmer(who))).Value;
		}

		/// <returns>Returns the map coordinates for some secret base in a nonspecific location.</returns>
		public static Vector2 GetSecretBaseCoordinatesForFarmer(Farmer who)
		{
			return ModConsts.BaseEntryCoordinates.FirstOrDefault(
				a => a.Key.Equals(GetSecretBaseForFarmer(who))).Value;
		}

		/// <returns>Returns overworld entry coordinates for a secret base.
		/// Coordinates provided are for the tile above the interactable tile.</returns>
		public static Vector2 GetSecretBaseCoordinates(string whichBase)
		{
			return ModConsts.BaseEntryCoordinates[whichBase];
		}
		
		/// <returns>Returns the name of the secret base nearest to the given coordinates.</returns>
		public static string GetNearestSecretBase(Vector2 coords)
		{
			var nearbyBases = ModConsts.BaseEntryCoordinates.Where(
				_ => ModConsts.BaseEntryLocations.ContainsKey(_.Key));
			return nearbyBases.OrderBy(
				_ => Math.Abs(_.Value.X - coords.X) + Math.Abs(_.Value.Y - coords.Y)).First().Key;
		}

		public static long? DoesAnyoneOwnSecretBase(string whichBase)
		{
			if (ModState != null && ModState.SecretBaseOwnership.ContainsKey(whichBase))
				return ModState.SecretBaseOwnership[whichBase];
			return null;
		}

		public static BaseStatus CanFarmerHaveSecretBase(Farmer who, string whichBase)
		{
			var owner = DoesAnyoneOwnSecretBase(whichBase);
			BaseStatus status;

			// For currently-owned bases:
			if (owner != null)
			{
				// Player is on the owner's guest list as a not-allowed guest
				if (owner.Value != who.UniqueMultiplayerID
					&& ModState != null
					&& ModState.GuestListForPeerSecretBases.ContainsKey(owner.Value)
					&& ModState.GuestListForPeerSecretBases[owner.Value].Request != Notification.RequestCode.Allowed)
					status = BaseStatus.EntryDenied;
				// Player isn't yet on the owner's guest list
				else
					status = owner.Value == who.UniqueMultiplayerID
						? BaseStatus.OwnedBySelf
						: BaseStatus.OwnedByAnother;
			}

			// For available bases:
			else
			{
				// Player without a base
				if (GetSecretBaseForFarmer(who) == null)
				{
					// Player can claim bases
					if (Instance.Config.DebugCanClaimSecretBases ||
					    ModState != null
					    && ModState.FarmersWhoCanClaimSecretBases.ContainsKey(who.UniqueMultiplayerID)
					    && ModState.FarmersWhoCanClaimSecretBases[who.UniqueMultiplayerID])
					{
						status = BaseStatus.Available;
					}
					// Player can't yet claim bases
					else
					{
						status = BaseStatus.AvailableButNotToFarmer;
					}
				}
				// Player doesn't have the required tool in their inventory
				else if (GetAppropriateToolForTheme(who, GetSecretBaseTheme(whichBase)) == null)
				{
					status = BaseStatus.AvailableButNoTool;
				}
				// Player could activate this base, but they need to deactivate another first
				else
				{
					status = BaseStatus.AvailableButAlreadyOwnAnother;
				}
			}

			return status;
		}

		public static bool CanFarmerFixHoles(Farmer who)
		{
			return Instance.Config.DebugCanFixHoles
			       || ModState != null
			       && who != null
			       && ModState.FarmersWhoCanFixHoles.ContainsKey(who.UniqueMultiplayerID)
			       && ModState.FarmersWhoCanFixHoles[who.UniqueMultiplayerID];
		}

		public static bool ShouldFarmersSecretBaseHolesBeFixed(Farmer who)
		{
			return ModState != null
			       && who != null
			       && ModState.FarmersWithFixedHoles.ContainsKey(who.UniqueMultiplayerID)
			       && ModState.FarmersWithFixedHoles[who.UniqueMultiplayerID];
		}

		public static void ClearSecretBaseFromFarmer(Farmer who)
		{
			if (who == null)
				return;

			// Mark the secret base as inactive, allowing it to be used by other players
			ModState.SecretBaseOwnership.Remove(GetSecretBaseForFarmer(who));

			// Mark base holes as unfixed
			if (ShouldFarmersSecretBaseHolesBeFixed(who))
				ModState.FarmersWithFixedHoles[who.UniqueMultiplayerID] = false;
		}

		/// <summary>
		/// Different tools are used to interact with the entries of secret bases
		/// from different visual themes.
		/// </summary>
		/// <returns>Returns the tool suited to this theme.</returns>
		public static Item GetAppropriateToolForTheme(Farmer who, ModConsts.Theme whichTheme)
		{
			if (who == null)
				return null;

			// Scythe: Tree, Bush
			if (whichTheme == ModConsts.Theme.Tree
			    || whichTheme == ModConsts.Theme.Bush)
				return who.Items.FirstOrDefault(item =>
					item is MeleeWeapon weapon && weapon.InitialParentTileIndex == 47);

			// Pick: Rock, Cave, Desert
			if (whichTheme == ModConsts.Theme.Rock
			    || whichTheme == ModConsts.Theme.Cave
			    || whichTheme == ModConsts.Theme.Desert)
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
			foreach (var location in Game1.locations.Where(_ => _.Name.StartsWith(ModConsts.ModId)))
			{
				// Update return warps
				var name = location.Name;
				var warp = ((string)location.Map.Properties["Warp"]).Split(' ');
				warp[2] = ModConsts.BaseEntryLocations[name];
				warp[3] = GetSecretBaseCoordinates(name).X.ToString();
				warp[4] = (GetSecretBaseCoordinates(name).Y + 2f).ToString();
				location.Map.Properties["Warp"] = string.Join(" ", warp);
				location.updateWarps();
			}
		}
		
		private static void WarpIntoBase(Farmer who, string whichBase)
		{
			if (who == null)
				return;

			var dest = ((string)Game1.getLocationFromName(whichBase)
					.Map.Properties["Warp"]).Split(' ');
			who.warpFarmer(new Warp(0, 0, whichBase, 
				int.Parse(dest[0]), int.Parse(dest[1]) - 1, false));
		}

		/// <returns>Returns the filename of the mod data JSON for this savegame.</returns>
		private static string GetDataFile()
		{
			return Path.Combine("data", string.Format(ModConsts.DataFile, Constants.SaveFolderName));
		}

		/// <summary>
		/// Loads in the mod state from the data file for this savegame.
		/// </summary>
		private void LoadModState()
		{
			var datafile = GetDataFile();
			ModState = Helper.Data.ReadJsonFile<ModState>(datafile) ?? new ModState();
			
			// Repopulate object storage
			foreach (var farmer in Game1.getAllFarmers())
			{
				CreateGlobalStorageForFarmer(farmer);
			}
		}

		private void UnloadModState()
		{
			ModState = null;

			GlobalStorage.Clear();
			PendingNotifications.Clear();
		}

		private Chest GetGlobalStorageForFarmer(Farmer who)
		{
			if (who == null)
				return null;

			Log.D($"GetGlobalStorage for {who.Name}: ");
			
			Chest chest = null;
			if (GlobalStorage.ContainsKey(who.UniqueMultiplayerID))
			{
				chest = GlobalStorage[who.UniqueMultiplayerID];
			} else {
				CreateGlobalStorageForFarmer(who);
			}

			Log.D($"{chest.items.Count} items, at {chest.TileLocation.ToString()}");
			
			return chest;
		}

		private Chest CreateGlobalStorageForFarmer(Farmer who)
		{
			if (who == null)
				return null;

			Log.D($"CreateGlobalStorage for {who.Name}");

			var coords = Vector2.Zero;
			var farm = Game1.getLocationFromName("Farm"); // hehehehe

			if (GlobalStorage.ContainsKey(who.UniqueMultiplayerID))
			{
				// Use existing coordinates for chests already tied to the player
				coords = GlobalStorage[who.UniqueMultiplayerID].TileLocation;

				Log.D($"{who.Name} exists in GlobalStorage, using TileLocation for chest at {coords.ToString()}");
			} else {
				// Use next coordinates across for chests being tied to new players
				coords = new Vector2(
					ModConsts.DummyChestCoords + GlobalStorage.Count, 
					ModConsts.DummyChestCoords);

				Log.D($"{who.Name} doesn't have a chest in GlobalStorage, using new TileLocation for {coords.ToString()}");
			}

			if (farm.Objects.ContainsKey(coords))
			{
				Log.D($"Chest at {coords.ToString()} exists in world, reading to GlobalStorage");

				// Populate with existing chests
				var dummyChest = (Chest)farm.getObjectAtTile((int)coords.X, (int)coords.Y);
				GlobalStorage.Add(who.UniqueMultiplayerID, dummyChest);
			}
			else
			{
				Log.D($"Adding chest to {coords.ToString()} for GlobalStorage");

				// Populate with new chests
				var dummyChest = new Chest(
					playerChest: true, tileLocation: coords);
				farm.Objects.Add(coords, dummyChest);
				GlobalStorage.Add(who.UniqueMultiplayerID, dummyChest);
			}

			return GlobalStorage[who.UniqueMultiplayerID];
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

			var actionProperty = (PropertyValue)null;
			tile?.Properties.TryGetValue("Action", out actionProperty);
			if (actionProperty == null)
				return;

			var strArray = ((string)actionProperty).Split(' ');
			var action = strArray[0];
			var args = new string[strArray.Length - 1];
			Array.Copy(strArray, 1, args, 0, args.Length);

			switch (action)
			{
				// Actions on each Base Entry building
				case ModConsts.BaseEntryAction:
					SecretBaseEntryDialogue(Game1.player, grabTile);
					break;
				// Actions on the Laptop building
				case ModConsts.LaptopAction:
					LaptopRootDialogue();
					break;
				// Actions on the Hole buildings
				case ModConsts.HoleAction:
					SecretBaseHoleFixDialogue(Game1.player);
					break;
			}
		}

		#endregion

		#region Dialogue Methods
		
		private void CreateInspectDialogue(string dialogue)
		{
			Game1.drawDialogueNoTyping(dialogue);
		}

		private void CreateQuestionDialogue(GameLocation location, string question, List<Response> answers)
		{
			location.createQuestionDialogue(question, answers.ToArray(), DialogueAnswers);
		}

		/// <summary>
		/// Creates a hybrid dialogue box using features of inspectDialogue and questionDialogue.
		/// A series of dialogues is presented, with the final dialogue having assigned responses.
		/// </summary>
		private void CreateInspectThenQuestionDialogue(GameLocation location, List<string> dialogues, List<Response> answerChoices)
		{
			location.afterQuestion = DialogueAnswers;
			Game1.activeClickableMenu = new MultipleDialogueQuestion(Helper, dialogues, answerChoices);
			Game1.dialogueUp = true;
			Game1.player.canMove = false;
		}

		/// <summary>
		/// Control flow for all question dialogues in the mod.
		/// </summary>
		/// <param name="who">Player.</param>
		/// <param name="answer">Player's answer to the last question dialogue.</param>
		private void DialogueAnswers(Farmer who, string answer)
		{
			var ans = answer.Split(' ');
			var farmersBaseName = GetSecretBaseForFarmer(who);
			var secretBase = Game1.getLocationFromName(farmersBaseName) as SecretBaseLocation;
			if (secretBase == null)
			{
				Log.D($"No secret base found for {who.Name}!"
				      + $"{(farmersBaseName != null ? $"(Saved as {farmersBaseName})" : "")}", Config.DebugMode);
			}
			switch (ans[0])
			{
				// Secret base available entry dialogue
				case "activate":
					SecretBaseActivate(who);
					break;

				// Secret base guest entry dialogue
				case "request":
					Game1.activeClickableMenu = new NotificationMenu(long.Parse(ans[1]));
					break;

				// Laptop root dialogue
				case "storage":
					LaptopStorageDialogue();
					break;
					
				// Laptop and base entry packup dialogue
				case "packprompt":
					Helper.Events.GameLoop.UpdateTicked += OpenPackDialogueOnNextTick;
					break;

				// Laptop and base entry packup confirmed
				case "packup":
					secretBase?.PackUpAndShipOut(who);
					break;
					
				// Secret base hole fix dialogue
				case "fixhole":
					if (secretBase != null)
						Game1.globalFadeToBlack(secretBase.FadedForHoleFix);
					else
						Log.E("Attempting to fix holes in null Secret Base.");
					break;
			}
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
			
			// Popup an inspection dialogue for unowned bases deciding on a question dialogue
			var dialogue = new List<string>{ i18n.Get("entry.tree_inspect") };
			var options = new List<Response>();

			if (theme == ModConsts.Theme.Rock || theme == ModConsts.Theme.Desert || theme == ModConsts.Theme.Cave)
				dialogue[0] = i18n.Get("entry.cave_inspect");
			else if (theme == ModConsts.Theme.Bush)
				dialogue[0] = i18n.Get("entry.bush_inspect");

			var availability = CanFarmerHaveSecretBase(who, whichBase);
			Log.D($"Secret base at {whichBase} is: {availability}",
				Config.DebugMode);
			switch (availability)
			{
				case BaseStatus.Available:
					// Prompt to take control of this base
					dialogue.Add(i18n.Get("entry.activate_prompt"));
					options.Add(new Response("activate", i18n.Get("dialogue.yes_option")));
					options.Add(new Response("cancel", i18n.Get("dialogue.no_option")));
					CreateInspectThenQuestionDialogue(location, dialogue, options);
					break;

				case BaseStatus.AvailableButNoTool:
				case BaseStatus.AvailableButNotToFarmer:
					// Offer no prompt, only the basic inspect dialogue
					CreateInspectDialogue(dialogue[0]);
					break;

				case BaseStatus.AvailableButAlreadyOwnAnother:
					// Prompt to abandon current base and take control
					dialogue.Add(i18n.Get("entry.deactivate_prompt"));
					options.Add(new Response("packprompt", i18n.Get("dialogue.yes_option")));
					options.Add(new Response("cancel", i18n.Get("dialogue.no_option")));
					CreateInspectThenQuestionDialogue(location, dialogue, options);
					break;

				case BaseStatus.EntryDenied:
					// Show a unique inspection dialogue
					dialogue[0] = i18n.Get("entry.denied_inspect");
					CreateInspectDialogue(dialogue[0]);
					break;

				case BaseStatus.OwnedByAnother:
					var response = $"request {DoesAnyoneOwnSecretBase(whichBase)}";
					dialogue[0] = i18n.Get("entry.guest_request_inspect");
					dialogue.Add(i18n.Get("entry.guest_request_prompt"));
					options.Add(new Response(response, i18n.Get("dialogue.yes_option")));
					options.Add(new Response("cancel", i18n.Get("dialogue.no_option")));
					CreateInspectThenQuestionDialogue(location, dialogue, options);
					break;

				case BaseStatus.OwnedBySelf:
					// Warp the player inside the secret base
					location.playSound("stairsdown");
					WarpIntoBase(who, whichBase);
					break;
			}
		}

		/// <summary>
		/// This method sequences question dialogue boxes by assigning them for the next tick,
		/// allowing for repeated calls to the afterDialogueBehavior field per location.
		/// </summary>
		private void OpenPackDialogueOnNextTick(object sender, UpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.UpdateTicked -= OpenPackDialogueOnNextTick;
			SecretBasePackUpDialogue();
		}

		/// <summary>
		/// Presents a confirmation dialogue box for packing up and disassociating this player's secret base.
		/// </summary>
		private void SecretBasePackUpDialogue()
		{
			var location = Game1.player.currentLocation;
			var question = i18n.Get("laptop.pack_prompt");
			var answers = new List<Response>
			{
				new Response("packup", i18n.Get("dialogue.yes_option")),
				new Response("cancel", i18n.Get("dialogue.no_option")),
			};
			CreateQuestionDialogue(location, question, answers);
		}

		// TODO: SYSTEM: Add laptop menu for allowed/blocked players if this farm has farmhands
		// their name, their portrait, their base location if visited, allow/block menu

		/// <summary>
		/// Root dialogue menu for player interactions with the laptop in the secret base.
		/// Offers base management options and item management from storage.
		/// </summary>
		private void LaptopRootDialogue()
		{
			var location = Game1.player.currentLocation;
			var question = i18n.Get("laptop.menu_prompt");
			var answers = new List<Response>();

			if (GetGlobalStorageForFarmer(Game1.player).items.Count > 0)
				answers.Add(new Response("storage", i18n.Get("laptop.storage_option")));
			answers.Add(new Response("packprompt", i18n.Get("laptop.pack_option")));
			answers.Add(new Response("cancel", i18n.Get("dialogue.cancel_option")));
			
			CreateQuestionDialogue(location, question, answers);
		}

		/// <summary>
		/// Opens a fishing chest-style grab dialogue taking from
		/// personal global storage data into the player's inventory.
		/// </summary>
		private void LaptopStorageDialogue()
		{
			var chest = GlobalStorage[Game1.player.UniqueMultiplayerID];
			chest.clearNulls();
			Game1.activeClickableMenu = new ItemGrabMenu(chest.items)
			{
				behaviorOnItemGrab = delegate (Item item, Farmer who)
				{
					if (item != null)
						chest.items.Remove(item);
				}
			};
		}

		/// <summary>
		/// Inspects a hole in the floor of some secret bases.
		/// May offer the player a prompt to fix it, removing the obstruction.
		/// </summary>
		private void SecretBaseHoleFixDialogue(Farmer who)
		{
			var location = Game1.player.currentLocation;
			
			var dialogue = new List<string>{ i18n.Get("hole.fix_inspect") };
			var options = new List<Response>();

			if (CanFarmerFixHoles(who))
			{
				// Offer a prompt to fix the obstructive holes in some secret bases
				dialogue.Add(i18n.Get("hole.fix_prompt"));
				options.Add(new Response("fixhole", i18n.Get("dialogue.yes_option")));
				options.Add(new Response("cancel", i18n.Get("dialogue.no_option")));
				CreateInspectThenQuestionDialogue(location, dialogue, options);
			}
			else
			{
				// Offer no prompt, only an inspection dialogue
				CreateInspectDialogue(dialogue[0]);
			}
		}

		#endregion
		
		#region Secret Base Modifier Methods

		/// <summary>
		/// Marks the base closest to the player as active,
		/// and associates it with this player in mod data.
		/// </summary>
		private void SecretBaseActivate(Farmer who)
		{
			var uid = who.UniqueMultiplayerID;
			var nearestBase = GetNearestSecretBase(who.getTileLocation());

			// Add any missing entries to mod state
			if (!ModState.FarmersWhoCanFixHoles.ContainsKey(uid))
				ModState.FarmersWhoCanFixHoles[uid] = false;
			if (!ModState.FarmersWithFixedHoles.ContainsKey(uid))
				ModState.FarmersWithFixedHoles[uid] = false;

			// Activate
			ModState.SecretBaseOwnership[nearestBase] = uid;
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
			var yOffset = 0;
			var tool = GetAppropriateToolForTheme(who, theme);

			if (tool == null)
				return;

			// Player animation
			var lastTool = Game1.player.CurrentTool;
			Game1.player.CurrentTool = (Tool)tool;
			Game1.player.FireTool();
			Game1.player.CurrentTool = lastTool;

			// Choose effects for theme
			switch (theme)
			{
				case ModConsts.Theme.Tree:
				{
					sfx = "cut";
					vfx = 17 * 64;
					yOffset = 1;

					where.Map.GetLayer("Front").Tiles[(int)coords.X, (int)coords.Y] = null;
					where.Map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
				case ModConsts.Theme.Rock:
				case ModConsts.Theme.Desert:
				case ModConsts.Theme.Cave:
				{
					sfx = "boulderBreak";
					vfx = 5 * 64;

					where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y] = null;
					where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
				case ModConsts.Theme.Bush:
				{
					sfx = "leafrustle";
					vfx = 17 * 64;

					where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y] = null;
					if (where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] != null)
						where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] = null;
					else
						where.Map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = null;
					break;
				}
			}

			// Sound effects
			if (!string.IsNullOrEmpty(sfx))
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
						new Vector2(coords.X * 64, (coords.Y + yOffset) * 64),
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

		#endregion

		#region Debug Methods

		/// <summary>
		/// Add items to the global storage chest, bypassing the limit check.
		/// </summary>
		/// <param name="who"></param>
		/// <param name="item"></param>
		internal static void AddToStorage(Farmer who, Item item)
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
			var locations = Game1.locations.Where(_ => _.Name.StartsWith(ModConsts.ModId)).ToArray();
			var names = new string[locations.Length - 1];
			for (var i = 0; i < names.Length; ++i)
				names[i] = locations[i].Name;
			var whichBase = names[new Random().Next(names.Length - 1)];
			var where = ModConsts.BaseEntryLocations[whichBase];
			var coords = ModConsts.BaseEntryCoordinates[whichBase];

			whichBase = GetSecretBaseForFarmer(who);
			if (whichBase != null)
			{
				where = ModConsts.BaseEntryLocations[whichBase];
				coords = ModConsts.BaseEntryCoordinates[whichBase];
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
			var coords = new Vector2(25, 12);
			const string where = "Farmhouse";

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
			var chest = GlobalStorage[who.UniqueMultiplayerID];
			var maxIndex = Game1.objectInformation.Count - 1;
			const int count = 50;

			while(chest.items.Count < count)
				AddToStorage(who, new StardewValley.Object(Game1.random.Next(maxIndex), 1));

			var msg = $"Filled {who.Name}'s storage: {chest.items.Count} items.";
			Log.D(msg);
			Game1.showGlobalMessage(msg);
		}

		private void DebugClearStorage()
		{
			var who = Game1.player;
			var chest = GlobalStorage[who.UniqueMultiplayerID];
			chest.items.Clear();
			chest.clearNulls();

			var msg = $"Cleared storage: {chest.items.Count} items.";
			Log.D(msg);
			Game1.showGlobalMessage(msg);
		}

		private void DebugAddRandomNotification()
		{
			var count = PendingNotifications.Count;

			var farmer = Game1.player.UniqueMultiplayerID;
			AddNewPendingNotification(new Notification(
				Notification.RequestCode.Allowed, Notification.DurationCode.Once, farmer, farmer, null, null));
			AddNewPendingNotification(new Notification(
				Notification.RequestCode.Denied, Notification.DurationCode.Today, farmer, farmer, null, null));
			AddNewPendingNotification(new Notification(
				Notification.RequestCode.Allowed, Notification.DurationCode.Always, farmer, farmer, null, null));
			AddNewPendingNotification(new Notification(
				Notification.RequestCode.Requested, Notification.DurationCode.None, farmer, farmer, null, null));

			Log.D($"Added notifications: {count} => {PendingNotifications.Count}");
		}

		#endregion
	}
}
