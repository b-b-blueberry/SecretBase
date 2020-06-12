using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace SecretBase
{
	public class SecretBaseLocation : DecoratableLocation
	{
		private bool _areHolesFixed;

		public SecretBaseLocation()
			: base()
		{
			Setup();
		}

		public SecretBaseLocation(string mapPath, string name)
			: base(mapPath, name)
		{
			Setup();
		}

		private void Setup()
		{
			Log.D($"Setting up {Name}",
				ModEntry.Instance.Config.DebugMode);

			// Fix holes for bases owned by farmers who've fixed the holes in their current base previously
			if (ModEntry.ShouldFarmersSecretBaseHolesBeFixed(Game1.player))
				FixHoles(true);
		}
		
		internal void Reset()
		{
			Log.D($"Resetting Secret Base at {Name}...",
				ModEntry.Instance.Config.DebugMode);

			ModEntry.Instance.Helper.Content.InvalidateCache($@"Maps/{Name}");
			
			var whomst = ModEntry.DoesAnyoneOwnSecretBase(Name);
			var owner = whomst == null ? null : Game1.getFarmer(whomst.Value);

			// Mark floor holes as not fixed
			_areHolesFixed = false;
			if (owner != null && ModEntry.ShouldFarmersSecretBaseHolesBeFixed(owner))
				ModEntry.ModState.FarmersWithFixedHoles[owner.UniqueMultiplayerID] = false;

			Setup();

			// Why did I call this again? Probably important
			updateMap();
		}

		private bool CheckForValidOwner(Farmer who)
		{
			return CheckForValidOwner(who.UniqueMultiplayerID);
		}

		private bool CheckForValidOwner(long? whomst)
		{
			var owner = ModEntry.DoesAnyoneOwnSecretBase(Name);
			if (whomst == null || owner == null || owner != whomst)
			{
				Log.E("Farmer acting in call"
				      + $" ({(whomst == null ? "null" : Game1.getFarmer(whomst.Value)?.Name)}) doesn't own this Secret Base"
				      + $" ({Name}, owned by {(owner != null ? Game1.getFarmer(owner.Value).Name : "null")})");
				return false;
			}
			return true;
		}
		
		internal void EvictFarmers(bool evictOwner)
		{
			Log.D($"Evicting farmers from {Name} (evict owner: {evictOwner})",
				ModEntry.Instance.Config.DebugMode);

			// Evict all players from this Secret Base
			var coords = ModEntry.GetSecretBaseCoordinates(Name);
			var owner = ModEntry.DoesAnyoneOwnSecretBase(Name);
			foreach (var farmer in farmers)
			{
				if (!evictOwner && farmer.UniqueMultiplayerID == owner)
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
			if (!CheckForValidOwner(who))
				return;
			
			var isDebug = ModEntry.Instance.Config.DebugMode;
			Log.D($"Packing up {Name} and shipping out",
				isDebug);

			// Remove all players from the Secret Base
			EvictFarmers(true);

			// Remove all placed objects from the map and save them to the global storage data model
			MoveObjectsToStorage(who);
			
			// Remove ownership
			ModEntry.ClearSecretBaseFromFarmer(who);

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
		/// Replaces obstructions in a secret base with non-obstructive pathable tiles.
		/// </summary>
		internal void FixHoles(bool forceAbleToFixHoles)
		{
			if (_areHolesFixed)
			{
				Log.D($"Holes already fixed in {Name}.",
					ModEntry.Instance.Config.DebugMode);
				return;
			}

			Log.D($"Fixing holes at {Name}",
				ModEntry.Instance.Config.DebugMode);

			var whomst = ModEntry.DoesAnyoneOwnSecretBase(Name);
			if (!CheckForValidOwner(whomst))
				return;
			var owner = Game1.getFarmer(whomst.Value);

			if (forceAbleToFixHoles)
			{
				// Ensure farmer is marked as able to fix holes
				if (!ModEntry.CanFarmerFixHoles(Game1.getFarmer(owner.UniqueMultiplayerID)))
					ModEntry.ModState.FarmersWhoCanFixHoles[owner.UniqueMultiplayerID] = true;

				// Mark the holes in the farmer's base as fixed
				if (!ModEntry.ShouldFarmersSecretBaseHolesBeFixed(owner))
					ModEntry.ModState.FarmersWithFixedHoles[owner.UniqueMultiplayerID] = true;
			}
			
			// Why did I call this again? Probably important
			updateMap();

			// Fetch the holes that need patching over
			var holeList = ModConsts.BaseHoleCoordinates[Name];

			var str = "";
			foreach (var hole in holeList)
				str += $"{hole.Location.ToString()} ";
			Log.D($"Fixing {holeList.Count} holes: {str}");
			
			// Grab the Secret Base interior tilesheet index, we'll need it for the patch
			const string tilesheetName = ModConsts.IndoorsStuffTilesheetId;
			var tilesheet = Map.GetTileSheet(tilesheetName);
			var whichTileSheet = -1;
			if (tilesheet != null)
			{
				for (whichTileSheet = Map.TileSheets.Count - 1; whichTileSheet > 0; --whichTileSheet)
				{
					if (Map.TileSheets[whichTileSheet].Id == tilesheet.Id)
					{
						break;
					}
				}
			}
			if (tilesheet == null || whichTileSheet < 0 || Map.TileSheets[whichTileSheet].Id != tilesheet.Id)
			{
				var message = $"Failed to fetch the interiors tilesheet '{tilesheetName}'.";
				Log.E(message);
				Game1.showGlobalMessage(message);
				return;
			}

			// Patch over the holes in the map, replacing the Buildings hole tiles with pathable tiles for planks
			foreach (var hole in holeList)
			{
				// Use vertical or horizontal plank sprites depending on the hole position in the map
				var plank = new Microsoft.Xna.Framework.Rectangle(
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

			_areHolesFixed = true;
		}
	}
}
