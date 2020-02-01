using System.Collections.Generic;

namespace SecretBase
{
	public class ModState
	{
		public Dictionary<string, long> SecretBaseOwnership { get; set; }

		public ModState()
		{
			SecretBaseOwnership = new Dictionary<string, long>();
		}
	}
}
