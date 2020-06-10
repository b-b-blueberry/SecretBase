using System.Collections.Generic;
using SecretBase.ModMessages;

namespace SecretBase
{
	public class ModState
	{
		public Dictionary<string, long> SecretBaseOwnership { get; set; }
		public Dictionary<long, bool> FarmersWhoCanClaimSecretBases { get; set; }
		public Dictionary<long, bool> FarmersWhoCanFixHoles { get; set; }
		public Dictionary<long, bool> FarmersWithFixedHoles { get; set; }
		public Dictionary<long, Notification> GuestListForLocalSecretBase { get; set; }
		public Dictionary<long, Notification> GuestListForPeerSecretBases { get; set; }
		public bool HasUnreadSecretMail { get; set; }

		public ModState()
		{
			SecretBaseOwnership = new Dictionary<string, long>();
			FarmersWhoCanClaimSecretBases = new Dictionary<long, bool>();
			FarmersWhoCanFixHoles = new Dictionary<long, bool>();
			FarmersWithFixedHoles = new Dictionary<long, bool>();
			GuestListForLocalSecretBase = new Dictionary<long, Notification>();
			GuestListForPeerSecretBases = new Dictionary<long, Notification>();
			HasUnreadSecretMail = false;
		}
	}
}
