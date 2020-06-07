using StardewModdingAPI;
using StardewValley;

namespace SecretBase
{
	public class Notification
	{
		public readonly EntryRequest.RequestCode Request;
		public readonly EntryRequest.DurationCode Duration;
		public readonly long Owner;
		public readonly long Guest;
		public readonly string Message;

		public Notification(EntryRequest.RequestCode request, EntryRequest.DurationCode duration, long owner, long guest)
		{
			Request = request;
			Duration = duration;
			Owner = owner;
			Guest = guest;

			var messageRequest = request switch
			{
				EntryRequest.RequestCode.Allowed => "allowed",
				EntryRequest.RequestCode.Denied => "denied",
				_ => "requested"
			};
			var messageDuration = duration switch
			{
				EntryRequest.DurationCode.Once => "once",
				EntryRequest.DurationCode.Today => "today",
				EntryRequest.DurationCode.Always => "always",
				_ => "none"
			};
			var whomst = request == EntryRequest.RequestCode.Requested
				? guest
				: owner;

			Message = ModEntry.Instance.Helper.Translation.Get("notification.message.format",
				new {
					sender = Game1.getFarmer(whomst).Name, 
					request = ModEntry.Instance.i18n.Get("notification.request." + messageRequest), 
					duration = ModEntry.Instance.i18n.Get("notification.duration." + messageRequest + "." + messageDuration)
			});
		}
	}
}