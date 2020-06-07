using StardewValley.Locations;

namespace SecretBase.Core
{
	public class SecretBaseLocation : DecoratableLocation
	{
		public SecretBaseLocation() : base() {
			Log.W($"TYPEOF LOCATION: {GetType().AssemblyQualifiedName}");
		}
		public SecretBaseLocation(string mapPath, string name) : base(mapPath, name) {}
	}
}
