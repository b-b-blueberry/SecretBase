using System.Linq;
using StardewValley;

namespace SecretBase.ModMessages
{
	public class UpdateMessage
	{
		public const string MessageType = "UpdateMessage";

		public readonly string Location;
		public readonly long Owner;
		public readonly bool IsHoleFixed;

		public UpdateMessage(string location, long owner, bool isHoleFixed)
		{
			Location = location;
			Owner = owner;
			IsHoleFixed = isHoleFixed;
		}

		public void Send()
		{
			ModEntry.Instance.Helper.Multiplayer.SendMessage(
				this, MessageType,
				new [] { ModEntry.Instance.ModManifest.UniqueID },
				Game1.otherFarmers.Keys.ToArray());
		}
	}
}
