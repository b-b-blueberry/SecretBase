using System.Collections.Generic;

namespace SecretBase
{
	public class ModState
	{
		public Dictionary<string, long> SecretBaseOwnership { get; set; }
		public Dictionary<long, bool> FarmersWhoCanClaimSecretBases { get; set; }
		public Dictionary<long, bool> FarmersWhoCanFixHoles { get; set; }
		public Dictionary<long, bool> FarmersWithFixedHoles { get; set; }
		public Dictionary<long, EntryRequest> GuestListForLocalSecretBase { get; set; }
		public Dictionary<long, EntryRequest> GuestListForPeerSecretBases { get; set; }

		public ModState()
		{
			SecretBaseOwnership = new Dictionary<string, long>();
			FarmersWhoCanClaimSecretBases = new Dictionary<long, bool>();
			FarmersWhoCanFixHoles = new Dictionary<long, bool>();
			FarmersWithFixedHoles = new Dictionary<long, bool>();
			GuestListForLocalSecretBase = new Dictionary<long, EntryRequest>();
			GuestListForPeerSecretBases = new Dictionary<long, EntryRequest>();
		}
	}
}
