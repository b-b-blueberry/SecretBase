using StardewModdingAPI;

namespace SecretBase
{
	public class Config
	{
		public SButton DebugWarpBaseKey { get; set; }
		public SButton DebugWarpHomeKey { get; set; }
		public bool DebugMode { get; set; }

		public Config()
		{
			DebugWarpBaseKey = SButton.OemOpenBrackets;
			DebugWarpHomeKey = SButton.OemCloseBrackets;
			DebugMode = true;
		}
	}
}
