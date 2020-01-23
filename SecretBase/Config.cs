using StardewModdingAPI;

namespace SecretBase
{
	class Config
	{
		public SButton debugKey { get; set; }

		public Config()
		{
			debugKey = SButton.J;
		}
	}
}
