using System.Collections.Generic;

namespace SecretBase
{
	public class ModData
	{
		public Dictionary<string, long> SecretBaseOwnership { get; set; }

		public ModData()
		{
			SecretBaseOwnership = new Dictionary<string, long>();
		}
	}
}
