using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using StardewModdingAPI;
using StardewModdingAPI.Events;

using StardewValley;
using StardewValley.Objects;
using StardewValley.Menus;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using xTile.Dimensions;
using xTile.ObjectModel;
using xTile;
using xTile.Layers;
using xTile.Tiles;

using PyTK.Extensions;
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
			NullOrInvalid,
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
			helper.Events.Player.Warped += OnWarped;
			
			helper.Events.Multiplayer.PeerContextReceived += OnPeerContextReceived;
			helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

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
					Log.D("Manually saving ModState.");
					DebugSaveState();
				});
			Helper.ConsoleCommands.Add("sbname", "Print the name of the map for your Secret Base.",
				(s, p) =>
				{
					var message = "";
					var secretBase = GetSecretBaseForFarmer(Game1.player);
					if (secretBase != null)
					{
						message = $"You own a Secret Base: {secretBase.Name}"
						          + $", at {ModConsts.BaseEntryLocations[secretBase.Name]}"
						          + $" {ModConsts.BaseEntryCoordinates[secretBase.Name]}";
					}
					else
					{
						message = "No Secret Base was found owned by this player.";
					}
					Log.D(message);
				});
			Helper.ConsoleCommands.Add("sbowners", "Print the owners for all Secret Bases in the world.",
				(s, p) =>
				{
					var message = "Current Secret Bases in the world:\n";
					var secretBases = GetAllSecretBases();
					if (secretBases != null)
					{
						foreach (var secretBase in secretBases)
							message += $"{secretBase.Name}"
							           + $" - {(secretBase.Owner.Value > 0 ? Game1.getFarmer(secretBase.Owner.Value).Name : "null")}"
							           + $" ({secretBase.Owner.Value})\n";
					}
					else
					{
						message = "No Secret Bases were found.";
					}
					Log.D(message);
				});
			Helper.ConsoleCommands.Add("sbpack", "Pack up and clear out your Secret Base.",
				(s, p) =>
				{
					var message = "";
					var secretBase = GetSecretBaseForFarmer(Game1.player);
					if (secretBase != null)
					{
						secretBase.PackUpAndShipOut(Game1.player);
						message = $"Packed up Secret Base: {secretBase.Name}";
					}
					else
					{
						message = "No Secret Base was found under this player.";
					}
					Log.D(message);
				});
			Helper.ConsoleCommands.Add("sbfix", "Fix any holes in your Secret Base.",
				(s, p) =>
				{
					var message = "";
					var secretBase = GetSecretBaseForFarmer(Game1.player);
					if (secretBase != null)
					{
						secretBase.FixHoles(false);
						message = $"Fixing holes in Secret Base: {secretBase.Name}";
					}
					else
					{
						message = "No Secret Base was found under this player.";
					}
					Log.D(message);
				});
			Helper.ConsoleCommands.Add("sbreset", "Reset the map data for your Secret Base.",
				(s, p) =>
				{
					var message = "";
					var secretBase = GetSecretBaseForFarmer(Game1.player);
					if (secretBase != null)
					{
						secretBase.Unassign();
						message = $"Reset Secret Base: {secretBase.Name}";
					}
					else
					{
						message = "No Secret Base was found under this player.";
					}
					Log.D(message);
				});
			Helper.ConsoleCommands.Add("sbwarp", "Warp your Secret Base or a random entry.",
				(s, p) =>
				{
					if (Game1.currentLocation.Name != GetSecretBaseEntryLocationForFarmer(Game1.player))
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
			Helper.ConsoleCommands.Add("sbnotify", "Add notifications to your inbox.",
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
		
		private void OnPeerContextReceived(object sender, PeerContextReceivedEventArgs e)
		{
			if (!Context.IsMainPlayer)
				return;

			Log.W($"Broadcasting from master player: {Game1.player.Name} ({Game1.player.UniqueMultiplayerID}"
			      + $" / {Game1.MasterPlayer.Name} ({Game1.MasterPlayer.UniqueMultiplayerID}))");

			// Send out broadcasts from all Secret Base locations to set the world state for new players
			var secretBases = GetAllSecretBases();
			if (secretBases == null)
				return;
			foreach (var secretBase in secretBases)
				secretBase.BroadcastUpdate(new []{ e.Peer.PlayerID });
		}

		private void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
		{
			if (e.FromModID != ModManifest.UniqueID)
				return;

			switch (e.Type)
			{
				case Notification.MessageType:
				{
					var message = e.ReadAs<Notification>();

					Log.D($"Received mail from {Game1.getFarmer(e.FromPlayerID)} ({e.FromPlayerID}):"
					      + $"\n{message.Summary}");
			
					ModState.SecretBaseGuestList[e.FromPlayerID] = message;
					AddNewPendingNotification(message);
					break;
				}
				case UpdateMessage.MessageType:
				{
					var message = e.ReadAs<UpdateMessage>();
				
					Log.D($"Received update from {Game1.getFarmer(e.FromPlayerID).Name} ({e.FromPlayerID}):"
					      + $"Location: {message.Location}"
					      + $"\nOwner: {(message.Owner <= 0 ? "null" : Game1.getFarmer(message.Owner).Name)} ({message.Owner})"
					      + $"\nHoles fixed: {message.IsHoleFixed}");

					if (string.IsNullOrEmpty(message.Location) || Game1.getLocationFromName(message.Location) == null)
					{
						Log.E($"No location found for value in multiplayer location update message: {message.Location ?? "null"}");
						return;
					}

					(Game1.getLocationFromName(message.Location) as SecretBaseLocation).UpdateFromMessage(message);
					break;
				}
			}
		}

		private void AddNewPendingNotification(Notification notification)
		{
			// Ignore duplicate notifications to prevent spam
			if (PendingNotifications.Exists(n
				=> n.Guest == notification.Guest
				   && n.Request == notification.Request))
				return;

			ModState.PlayerHasUnreadMail = true;
			Game1.playSound("give_gift");
			Game1.showGlobalMessage(notification.Summary + ".");
			PendingNotifications.Add(notification);
			AddNotificationButton();
		}

		private void OnWarped(object sender, WarpedEventArgs e)
		{
			// Reenable player actions when leaving a peer's secret base
			if (GetSecretBaseCoordinates(e.OldLocation.Name) != Vector2.Zero
			    && GetSecretBaseOwner(e.OldLocation.Name) != null
			    && GetSecretBaseOwner(e.OldLocation.Name) != e.Player.UniqueMultiplayerID)
			{
				Helper.Events.Input.ButtonPressed -= SuppressInteractionButtons;
			}

			// Block players from actions when entering someone's secret base
			if (GetSecretBaseCoordinates(e.NewLocation.Name) != Vector2.Zero
			    && GetSecretBaseOwner(e.NewLocation.Name) != null
			    && GetSecretBaseOwner(e.NewLocation.Name) != e.Player.UniqueMultiplayerID)
			{
				Helper.Events.Input.ButtonPressed += SuppressInteractionButtons;
			}
		}
		
		private void SuppressInteractionButtons(object sender, ButtonPressedEventArgs e)
		{
			if (!e.Button.IsActionButton() && !e.Button.IsUseToolButton())
				return;

			Helper.Input.Suppress(e.Button);
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

		public static IEnumerable<SecretBaseLocation> GetAllSecretBases()
		{
			return Game1.locations.Where(_ => _.Name.StartsWith(ModConsts.ModId)).Cast<SecretBaseLocation>();
		}

		/// <returns>Returns the secret base owned by a player.</returns>
		public static SecretBaseLocation GetSecretBaseForFarmer(Farmer who)
		{
			var secretBases = GetAllSecretBases()?.ToList();
			Log.D($"GetSecretBaseForFarmer: Who? {who != null} && Bases? {secretBases != null}");
			if (who == null || secretBases == null)
				return null;
			return secretBases.FirstOrDefault(_ => _.Owner.Value == who.UniqueMultiplayerID);
		}
		
		public static Vector2 GetSecretBaseCoordinates(string whichBase)
		{
			if (whichBase == null)
				return Vector2.Zero;

			var name = whichBase.Split('.');
			return ModConsts.BaseEntryCoordinates.ContainsKey(name[name.Length - 1])
				? ModConsts.BaseEntryCoordinates[name[name.Length - 1]]
				: Vector2.Zero;
		}

		public static string GetSecretBaseLocation(string whichBase)
		{
			if (whichBase == null)
				return null;

			var name = whichBase.Split('.');
			return ModConsts.BaseEntryLocations.ContainsKey(name[name.Length - 1])
				? ModConsts.BaseEntryLocations[name[name.Length - 1]]
				: null;
		}

		/// <returns>Returns the location name for some secret base.</returns>
		public static string GetSecretBaseEntryLocationForFarmer(Farmer who)
		{
			var whichBase = GetSecretBaseForFarmer(who).Name;
			return ModConsts.BaseEntryLocations.FirstOrDefault(_ => _.Key.EndsWith(whichBase)).Value;
		}

		/// <returns>Returns the Secret Base nearest to the given coordinates.</returns>
		public static SecretBaseLocation GetNearestSecretBase(Vector2 coords)
		{
			// Fetch Secret Base coordinates in game locations matching the current location
			var nearbyBases = ModConsts.BaseEntryCoordinates.Where(
				_ => ModConsts.BaseEntryLocations.ContainsKey(_.Key));

			// Fetch the name of the Secret Base at the top of that list, ordered by proximity
			var whichBase = nearbyBases.OrderBy(
				_ => Math.Abs(_.Value.X - coords.X) + Math.Abs(_.Value.Y - coords.Y)).First().Key;

			// Fetch the SecretBaseLocation by name
			return whichBase != null
				? Game1.getLocationFromName(ModConsts.AssetPrefix + whichBase) as SecretBaseLocation
				: null;
		}

		public static long? GetSecretBaseOwner(string whichBase)
		{
			if (whichBase != null)
			{
				var secretBase = Game1.getLocationFromName(whichBase) as SecretBaseLocation;
				Log.D($"Getting owner for Secret Base at {whichBase} = {secretBase?.Name}");
				return secretBase != null && secretBase.Owner.Value > 0
					? secretBase.Owner
					: null;
			}
			return null;
		}

		public static BaseStatus GetStatusOfSecretBase(Farmer who, SecretBaseLocation secretBase)
		{
			BaseStatus status;
			if (who == null || secretBase == null)
				return BaseStatus.NullOrInvalid;

			// For currently-owned bases:
			if (secretBase.Owner.Value > 0)
			{
				// Player is on the owner's guest list as a not-allowed guest
				if (secretBase.Owner.Value != who.UniqueMultiplayerID
					&& secretBase.Owner.Value > 0
				    && ModState != null
				    && ModState.SecretBaseGuestList.ContainsKey(secretBase.Owner.Value)
				    && ModState.SecretBaseGuestList[secretBase.Owner.Value].Request != NotifCodes.RequestCode.Allowed)
					status = BaseStatus.EntryDenied;
				// Player isn't yet on the owner's guest list
				else
					status = secretBase.Owner.Value == who.UniqueMultiplayerID
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
					if (Instance.Config.DebugCanClaimSecretBases
					    || ModState != null
					    && ModState.CanPlayerClaimSecretBases)
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
				else if (secretBase.GetToolForTheme(who) == null)
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
			return who != null && (Instance.Config.DebugCanFixHoles || ModState != null && ModState.CanPlayerFixHoles);
		}

		#endregion

		#region Management Methods

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
				CreateGlobalStorageForFarmer(farmer);
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

			var farm = Game1.getLocationFromName("Farm"); // hehehehe
			Vector2 coords;

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
			var secretBase = GetSecretBaseForFarmer(who);
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
			var secretBase = GetNearestSecretBase(actionCoords);
			var theme = secretBase.GetTheme();
			
			// Popup an inspection dialogue for unowned bases deciding on a question dialogue
			var dialogue = new List<string>{ i18n.Get("entry.tree_inspect") };
			var options = new List<Response>();

			switch (theme)
			{
				case SecretBaseLocation.Theme.Rock:
				case SecretBaseLocation.Theme.Desert:
				case SecretBaseLocation.Theme.Cave:
					dialogue[0] = i18n.Get("entry.cave_inspect");
					break;
				case SecretBaseLocation.Theme.Bush:
					dialogue[0] = i18n.Get("entry.bush_inspect");
					break;
			}

			var availability = GetStatusOfSecretBase(who, secretBase);
			Log.D($"Secret base at {secretBase.Name} is: {availability}",
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
					var response = $"request {secretBase.Owner}";
					dialogue[0] = i18n.Get("entry.guest_request_inspect");
					dialogue.Add(i18n.Get("entry.guest_request_prompt"));
					options.Add(new Response(response, i18n.Get("dialogue.yes_option")));
					options.Add(new Response("cancel", i18n.Get("dialogue.no_option")));
					CreateInspectThenQuestionDialogue(location, dialogue, options);
					break;

				case BaseStatus.OwnedBySelf:
					// Warp the player inside the secret base
					location.playSound("stairsdown");
					secretBase.Warp(who);
					break;

				case BaseStatus.NullOrInvalid:
				default:
					Log.E("Attempting to use invalid Secret Base or availability.");
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
			var secretBase = GetNearestSecretBase(who.getTileLocation());
			secretBase.Assign(who);
			SecretBaseActivationFx(who, secretBase);
		}

		/// <summary>
		/// Adds temporary special effects in the overworld when a secret base is activated.
		/// Also removes the inactive secret base entry tiles and invalidates the cache,
		/// updating the location to reflect the new entry appearance and functionality.
		/// </summary>
		private void SecretBaseActivationFx(Farmer who, SecretBaseLocation secretBase)
		{
			var where = who.currentLocation;
			var coords = GetSecretBaseCoordinates(secretBase.Name);

			var sfx = "";
			var vfx = 0;
			var yOffset = 0;
			var tool = secretBase.GetToolForTheme(who);

			if (tool == null)
				return;

			// Player animation
			var lastTool = Game1.player.CurrentTool;
			Game1.player.CurrentTool = (Tool)tool;
			Game1.player.FireTool();
			Game1.player.CurrentTool = lastTool;

			// Choose effects for theme
			switch (secretBase.GetTheme())
			{
				case SecretBaseLocation.Theme.Tree:
				{
					sfx = "cut";
					vfx = 17 * 64;
					yOffset = 1;

					where.Map.GetLayer("Front").Tiles[(int)coords.X, (int)coords.Y] = null;
					where.Map.GetLayer("Buildings").Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
				case SecretBaseLocation.Theme.Rock:
				case SecretBaseLocation.Theme.Desert:
				case SecretBaseLocation.Theme.Cave:
				{
					sfx = "boulderBreak";
					vfx = 5 * 64;

					where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y] = null;
					where.Map.GetLayer(ModConsts.ExtraLayerId).Tiles[(int)coords.X, (int)coords.Y + 1] = null;

					break;
				}
				case SecretBaseLocation.Theme.Bush:
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
						new Vector2(coords.X * 64, (coords.Y - yOffset) * 64),
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

		#region World Map Modifier Methods
		
		internal static void EditVanillaMap(Map map, string name)
		{
			// TODO: METHOD: Add seasonal loading for all entry themes once assets are ready
			// TODO: BUGS: Resolve beach/beach-nightmarket entry patching inconsistency when Night Markets are active

			Log.D($"Editing vanilla map: {name}");

			var path = Instance.Helper.Content.GetActualAssetKey(
				Path.Combine(ModConsts.AssetsPath, $"{ModConsts.OutdoorsStuffTilesheetId}.png"));
			var texture = Instance.Helper.Content.Load<Texture2D>(path);

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
				var coords = GetSecretBaseCoordinates(baseLocation.Key);
				var row = tilesheet.SheetWidth;

				// TODO: ASSETS: Patch in inactive assets once they've been made

				var index = 0;
				switch (SecretBaseLocation.GetTheme(baseLocation.Key))
				{
					case SecretBaseLocation.Theme.Tree:
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

					case SecretBaseLocation.Theme.Bush:
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
						
					case SecretBaseLocation.Theme.Cave:
					case SecretBaseLocation.Theme.Desert:
					case SecretBaseLocation.Theme.Rock:
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
			var secretBase = GetSecretBaseForFarmer(who);

			var whichBase = secretBase?.Name ?? ModConsts.BaseEntryCoordinates.Keys.ToArray()
				[Game1.random.Next(ModConsts.BaseEntryCoordinates.Keys.Count - 1)];
			
			var where = GetSecretBaseLocation(whichBase);
			var coords = GetSecretBaseCoordinates(whichBase);
			
			Log.D($"Warping {who.Name} to {where} at {coords.X}, {coords.Y}");

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
				NotifCodes.RequestCode.Allowed, NotifCodes.DurationCode.Once, farmer, farmer, null, null));
			AddNewPendingNotification(new Notification(
				NotifCodes.RequestCode.Denied, NotifCodes.DurationCode.Today, farmer, farmer, null, null));
			AddNewPendingNotification(new Notification(
				NotifCodes.RequestCode.Allowed, NotifCodes.DurationCode.Always, farmer, farmer, null, null));
			AddNewPendingNotification(new Notification(
				NotifCodes.RequestCode.Requested, NotifCodes.DurationCode.None, farmer, farmer, null, null));

			Log.D($"Added notifications: {count} => {PendingNotifications.Count}");
		}

		#endregion
	}
}
