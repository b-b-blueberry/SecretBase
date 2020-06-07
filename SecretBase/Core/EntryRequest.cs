namespace SecretBase
{
	public class EntryRequest
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

		public RequestCode Request { get; set; }
		public DurationCode Duration { get; set; }
		public const string MessageType = "EntryRequest";

		public EntryRequest(RequestCode request, DurationCode duration)
		{
			Request = request;
			Duration = duration;
		}
	}
}
