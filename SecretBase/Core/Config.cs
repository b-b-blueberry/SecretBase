using StardewModdingAPI;

namespace SecretBase
{
	public class Config
	{
		public bool DebugCanClaimSecretBases { get; set; } = true;
		public bool DebugCanFixHoles { get; set; } = true;
		public bool DebugMode { get; set; } = true;
		public string MapType { get; set; } = "Default";
	}
}
