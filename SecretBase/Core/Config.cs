using StardewModdingAPI;

namespace SecretBase
{
	public class Config
	{
		public SButton DebugWarpKey { get; set; }
		public bool DebugMode { get; set; }

		public Config()
		{
			DebugWarpKey = SButton.O;
			DebugMode = false;
		}
	}
}
