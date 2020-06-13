using System.Collections.Generic;
using SecretBase.ModMessages;

namespace SecretBase
{
	public class ModState
	{
		public bool CanPlayerClaimSecretBases { get; set; }
		public bool CanPlayerFixHoles { get; set; }
		public bool HasPlayerFixedHolesForever { get; set; }
		public bool PlayerHasUnreadMail { get; set; }
		public Dictionary<long, Notification> SecretBaseGuestList { get; set; }

		public ModState()
		{
			SecretBaseGuestList = new Dictionary<long, Notification>();
		}
	}
}
