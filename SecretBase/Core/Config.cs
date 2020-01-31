using StardewModdingAPI;

namespace SecretBase
{
	public class Config
	{
		public SButton DebugWarpBaseKey { get; set; } = SButton.OemOpenBrackets;
		public SButton DebugWarpHomeKey { get; set; } = SButton.OemCloseBrackets;
		public SButton DebugSaveStateKey { get; set; } = SButton.L;
		public SButton DebugFillStorageKey { get; set; } = SButton.K;
		public bool DebugMode { get; set; } = true;
	}
}
