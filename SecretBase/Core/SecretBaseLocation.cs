using System.Linq;

using Microsoft.Xna.Framework;
using Netcode;

using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Tools;

using SecretBase.ModMessages;

namespace SecretBase
{
	public class SecretBaseLocation : DecoratableLocation
	{
		public enum Theme
		{
			Tree,
			Rock,
			Bush,
			Desert,
			Cave
		}

		// Net fields
		internal readonly NetLong Owner = new NetLong(-1);
		internal readonly NetBool IsHoleFixed = new NetBool(false);

		// Local field
		private readonly Theme _theme;

		public SecretBaseLocation()
			: base()
		{
			_theme = GetTheme(Name);
			FirstTimeSetup();
		}

		public SecretBaseLocation(string mapPath, string name)
			: base(mapPath, name)
		{
			_theme = GetTheme(Name);
			FirstTimeSetup();
		}

		protected override void initNetFields()
		{
			base.initNetFields();
			NetFields.AddFields(Owner, IsHoleFixed);
		}

		private void FirstTimeSetup()
		{
			Log.D($"Setting up {Name}",
				ModEntry.Instance.Config.DebugMode);
			
			// Patch functional return warp coords into the dummy values for the Warp property
			var warp = ((string)Map.Properties["Warp"]).Split(' ');
			var coords = ModEntry.GetSecretBaseCoordinates(Name);
			var dest = ModEntry.GetSecretBaseLocation(Name);
			if (warp.Length == 0 || coords == Vector2.Zero || dest == null)
			{
				Log.E($"Failed to patch in warp properties for {Name} in setup:"
				      + $"\nWarp: {(warp.Length <= 0 ? "null" : warp.Aggregate((a,b) => $"{a} / {b}"))}"
				      + $"\nCoords: {(coords == Vector2.Zero ? "null" : coords.ToString())}"
				      + $"\nLocation: {dest ?? "null"}");
			}
			else
			{
				warp[2] = dest;
				warp[3] = (coords.X).ToString();
				warp[4] = (coords.Y + 2f).ToString();
				Map.Properties["Warp"] = string.Join(" ", warp);
				updateWarps();
			}

			// Fix holes for bases owned by farmers who've fixed the holes in their current base previously
			if (ModEntry.ModState != null
			    && ModEntry.ModState.HasPlayerFixedHolesForever)
			{
				Log.D($"{(TryFixHoles() ? "Did" : "Didn't")} fix holes on startup for {Name}.");
			}
		}
		
		internal void BroadcastUpdate()
		{
			new UpdateMessage(Name, Owner, IsHoleFixed).Send();
		}

		internal void UpdateFromMessage(UpdateMessage message)
		{
			Owner.Value = message.Owner;
			IsHoleFixed.Value = message.IsHoleFixed;
		}

		internal void Assign(Farmer who)
		{
			Log.D($"Assigning {who.Name} ({who.UniqueMultiplayerID}) as Owner of {Name}.");

			Owner.Value = who.UniqueMultiplayerID;
			BroadcastUpdate();
		}

		internal void Unassign()
		{
			Log.D($"Unassigning {(Owner > 0 ? Game1.getFarmer(Owner).Name : "null")} ({Owner}) from {Name}.");

			Reset();
		}
		
		private void Reset()
		{
			Log.D($"Resetting Secret Base at {Name}...",
				ModEntry.Instance.Config.DebugMode);
			
			// Remove ownership
			Owner.Value = -1;

			// Mark floor holes as not fixed
			IsHoleFixed.Value = false;

			// Reset map tiles
			ModEntry.EditVanillaMap(Map, Name);
			
			// Why did I call this again? Probably important
			updateMap();

			BroadcastUpdate();
		}

		/// <summary>
		/// Visual changes in the world are tied to the theming of each Secret Base.
		/// A rock Secret Base appears on certain layers with certain tiles, and creates
		/// different visual and sound effects on use compared to a tree Secret Base, for example.
		/// </summary>
		/// <param name="name">Map name for the Secret Base.</param>
		/// <returns>Theme value appropriate to the name of the Secret Base.</returns>
		internal static Theme GetTheme(string name) {
			var theme = Theme.Tree;
			if (name.Contains("R"))
				theme = Theme.Rock;
			else if (name.Contains("Bu"))
				theme = Theme.Bush;
			else if (name.Contains("D"))
				theme = Theme.Desert;
			else if (name.Contains("C"))
				theme = Theme.Cave;
			return theme;
		}
		
		internal Theme GetTheme()
		{
			return _theme;
		}

		/// <summary>
		/// Different tools are used to interact with the entries of secret bases
		/// from different visual themes.
		/// </summary>
		/// <returns>Returns the tool suited to this theme.</returns>
		public Item GetToolForTheme(Farmer who)
		{
			if (who == null)
				return null;

			switch (_theme)
			{
				// Scythe:
				case Theme.Tree:
				case Theme.Bush:
					return who.Items.FirstOrDefault(item =>
						item is MeleeWeapon weapon && weapon.InitialParentTileIndex == 47);

				// Pickaxe:
				case Theme.Rock:
				case Theme.Cave:
				case Theme.Desert:
					return who.getToolFromName("Pickaxe");

				default:
					return null;
			}
		}

		internal void Warp(Farmer who)
		{
			if (who == null)
				return;

			var dest = ((string)Map.Properties["Warp"]).Split(' ');
			who.warpFarmer(new Warp(0, 0, Name, 
				int.Parse(dest[0]), int.Parse(dest[1]) - 1, false));
		}

		internal void EvictFarmers(bool evictOwner)
		{
			Log.D($"Evicting farmers from {Name} (evict owner: {evictOwner})",
				ModEntry.Instance.Config.DebugMode);

			// Evict all players from this Secret Base
			var coords = ModConsts.BaseEntryCoordinates[Name];
			foreach (var farmer in farmers)
			{
				if (!evictOwner && Owner > 0 && farmer.UniqueMultiplayerID == Owner)
					continue;
				farmer.warpFarmer(new Warp(0, 0,
					ModConsts.BaseEntryLocations[Name],
					(int)coords.X,
					(int)coords.Y + 2,
					false));
			}
		}
		
		internal void MoveObjectsToStorage(Farmer who)
		{
			Log.D($"Moving objects to Storage from {Name}",
				ModEntry.Instance.Config.DebugMode);

			foreach (var obj in Objects.Values)
			{
				if (obj.GetType() != typeof(Chest))
					ModEntry.AddToStorage(who, obj);
				else
				{
					// Deposit chest contents into storage
					foreach (var chest in ((Chest) obj).items)
						ModEntry.AddToStorage(who, chest);
					ModEntry.AddToStorage(who, (Chest)obj);
				}
			}
			Objects.Clear();

			// TODO: BUGS: Ensure decoratable location issues with locations added by TMXL are resolved
			
			foreach (var f in furniture)
				ModEntry.AddToStorage(who, f);
			furniture.Clear();
			
		}
		
		/// <summary>
		/// Returns the interior of the secret base to its default inactive state
		/// and clears out all placed objects, saving them to global storage data.
		/// Will also warp out any players still inside.
		/// </summary>
		internal void PackUpAndShipOut(Farmer who)
		{
			Log.D($"Packing up {Name} and shipping out",
				ModEntry.Instance.Config.DebugMode);

			// Remove all players from the Secret Base
			EvictFarmers(true);

			// Remove all placed objects from the map and save them to the global storage data model
			if (who != null)
				MoveObjectsToStorage(who);
			
			// Reset map state
			Reset();
		}

		/// <summary>
		/// Copy of the StardewValley.Locations.Beach fadedForBridgeFix method.
		/// After fading to black, play some construction sounds, patch the holes, and fade in again.
		/// </summary>
		public void FadedForHoleFix()
		{
			DelayedAction.playSoundAfterDelay("crafting", 1000);
			DelayedAction.playSoundAfterDelay("crafting", 1500);
			DelayedAction.playSoundAfterDelay("crafting", 2000);
			DelayedAction.playSoundAfterDelay("crafting", 2500);
			DelayedAction.playSoundAfterDelay("axchop", 3000);
			DelayedAction.playSoundAfterDelay("Ship", 3200);
			Game1.viewportFreeze = true;
			Game1.viewport.X = -10000;
			Game1.pauseThenDoFunction(4000, DoneWithHoleFix);
			FixHoles(true);
		}

		private void DoneWithHoleFix()
		{
			Game1.globalFadeToClear();
			Game1.viewportFreeze = false;
		}

		/// <summary>
		/// Fix holes if the player has been marked as being able to.
		/// </summary>
		/// <returns>Whether or not the player was able to fix holes.</returns>
		internal bool TryFixHoles() {
			if (Owner.Value > 0
			    && ModEntry.ModState != null
			    && ModEntry.ModState.CanPlayerFixHoles)
			{
				FixHoles(true);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Replaces obstructions in a secret base with non-obstructive pathable tiles.
		/// </summary>
		internal void FixHoles(bool forceAbleToFixHoles)
		{
			if (IsHoleFixed)
			{
				Log.D($"Holes already fixed in {Name}.",
					ModEntry.Instance.Config.DebugMode);
				return;
			}

			Log.D($"Fixing holes at {Name}",
				ModEntry.Instance.Config.DebugMode);

			if (forceAbleToFixHoles)
			{
				// Ensure farmer is marked as able to fix holes
				ModEntry.ModState.CanPlayerFixHoles = true;
				ModEntry.ModState.HasPlayerFixedHolesForever = true;
			}
			
			// Why did I call this again? Probably important
			updateMap();

			// Fetch the holes that need patching over
			var holeList = ModConsts.BaseHoleCoordinates[Name];

			var str = holeList.Aggregate("", (current, hole) => current + $"{hole.Location.ToString()}\n");
			Log.D($"Fixing {holeList.Count} holes: {str}");
			
			// Grab the Secret Base interior tilesheet index, we'll need it for the patch
			const string tilesheetName = ModConsts.IndoorsStuffTilesheetId;
			var tilesheet = Map.GetTileSheet(tilesheetName);
			var whichTileSheet = -1;
			if (tilesheet != null)
			{
				for (whichTileSheet = Map.TileSheets.Count - 1; whichTileSheet > 0; --whichTileSheet)
					if (Map.TileSheets[whichTileSheet].Id == tilesheet.Id)
						break;
			}
			if (tilesheet == null || whichTileSheet < 0 || Map.TileSheets[whichTileSheet].Id != tilesheet.Id)
			{
				var message = $"Failed to fetch the interiors tilesheet '{tilesheetName}' for {Name}.";
				Log.E(message);
				Game1.showGlobalMessage(message);
				return;
			}

			// Patch over the holes in the map, replacing the Buildings hole tiles with pathable tiles for planks
			foreach (var hole in holeList)
			{
				// Use vertical or horizontal plank sprites depending on the hole position in the map
				var plank = new Rectangle(
					hole.Width > hole.Height ? 0 : 3, 4, hole.Width, hole.Height);
				var plankIndex = tilesheet.SheetWidth * plank.Y + plank.X;
				const string layer = "Buildings";

				for (var y = 0; y < hole.Height; ++y)
				{
					for (var x = 0; x < hole.Width; ++x)
					{
						var tileX = hole.X + x;
						var tileY = hole.Y + y;
						var tileIndex = plankIndex + tilesheet.SheetWidth * y + x;

						Log.D($"location.Name={Name} layer={layer} plank={plank} whichTileSheet={whichTileSheet} "
						      + $"tileIndex={tileIndex} tileX={tileX} tileY={tileY}");

						setMapTile(tileX, tileY, tileIndex, layer, null, whichTileSheet);
					}
				}
			}

			IsHoleFixed.Value = true;
		}
	}
}
