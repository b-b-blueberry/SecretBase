using System.Linq;
using Newtonsoft.Json;
using StardewValley;

namespace SecretBase.ModMessages
{
	public class UpdateMessage
	{
		public const string MessageType = "UpdateMessage";

		public readonly string Location;
		public readonly long Owner;
		public readonly bool IsHoleFixed;
		private readonly long[] _playerIDs;

		[JsonConstructor]
		public UpdateMessage(string location, long owner, bool isHoleFixed, long[] playerIDs)
		{
			Location = location;
			Owner = owner;
			IsHoleFixed = isHoleFixed;
			_playerIDs = playerIDs ?? Game1.otherFarmers.Keys.ToArray();
		}

		public void Send()
		{
			ModEntry.Instance.Helper.Multiplayer.SendMessage(
				this, MessageType,
				new [] { ModEntry.Instance.ModManifest.UniqueID },
				_playerIDs);
		}
	}
}
