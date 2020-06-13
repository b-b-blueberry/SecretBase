using StardewValley;

namespace SecretBase.ModMessages
{
	public class Notification
	{
		public enum RequestCode
		{
			Requested,
			Allowed,
			Denied
		}

		public enum DurationCode
		{
			None,
			Once,
			Today,
			Always
		}

		public const string MessageType = "Notification";

		public RequestCode Request;
		public DurationCode Duration;
		public readonly long Owner;
		public readonly long Guest;
		public string Summary;
		public int[] MessageTokens;
		public string[] SomeoneTokens;

		public Notification(RequestCode request, DurationCode duration, long owner, long guest,
			int[] messageTokens, string[] someoneTokens)
		{
			Request = request;
			Duration = duration;
			Owner = owner;
			Guest = guest;

			SetTokens(messageTokens, someoneTokens);
			Rebuild();
		}

		public Notification(Notification n)
		{
			Request = n.Request;
			Duration = n.Duration;
			Owner = n.Owner;
			Guest = n.Guest;

			SetTokens();
			n.MessageTokens.CopyTo(MessageTokens, 0);
			n.SomeoneTokens.CopyTo(SomeoneTokens, 0);

			Rebuild();
		}

		public void SetTokens()
		{
			SetTokens(MessageTokens, SomeoneTokens);
		}

		public void SetTokens(int[] messageTokens, string[] someoneTokens)
		{
			MessageTokens = messageTokens ?? new [] { 1, 1, 1, 1, 1 };
			SomeoneTokens = someoneTokens ?? new [] { "", "", "", "", "" };
		}

		public void Rebuild()
		{
			var messageRequest = Request switch
			{
				RequestCode.Allowed => "allowed",
				RequestCode.Denied => "denied",
				_ => "requested"
			};
			var messageDuration = Duration switch
			{
				DurationCode.Once => "once",
				DurationCode.Today => "today",
				DurationCode.Always => "always",
				_ => "none"
			};
			var composeToWho = Request == RequestCode.Requested
				? Guest
				: Owner;

			Summary = ModEntry.Instance.Helper.Translation.Get("notification.message.format",
				new {
					sender = Game1.getFarmer(composeToWho).Name, 
					request = ModEntry.Instance.i18n.Get("notification.request." + messageRequest), 
					duration = ModEntry.Instance.i18n.Get("notification.duration." + messageRequest + "." + messageDuration)
				});
		}

		public void Send()
		{
			var recipient = Request == RequestCode.Requested ? Owner : Guest;
			Log.D($"Sending mail to {Game1.getFarmer(recipient)} ({recipient}):\n{Summary}");
			ModEntry.Instance.Helper.Multiplayer.SendMessage(
				this, MessageType,
				new [] { ModEntry.Instance.ModManifest.UniqueID },
				new [] { recipient });
		}
	}
}