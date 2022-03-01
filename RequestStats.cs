using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rt_call_home
{
	public record RequestStats
	{
		private Dictionary<HttpStatusCode, int> counts = new Dictionary<HttpStatusCode, int>();
		private Queue<HttpStatusCode> latestAwaitedResponse = new Queue<HttpStatusCode>(Enumerable.Repeat(HttpStatusCode.OK, 4));

		public void Add(HttpStatusCode code, string message, bool awaitedResponse)
		{
			counts[code] = counts.TryGetValue(code, out int count) ? count + 1 : 1;
			if (awaitedResponse)
            {
				latestAwaitedResponse.Enqueue(code);
				latestAwaitedResponse.Dequeue();
			}
		}

		public int Total => counts.Sum(o => o.Value);

		public decimal GetFractionSuccess()
        {
			var notInterrupted = counts.Where(o => o.Key != HttpStatusCode.GatewayTimeout).ToList();
			return notInterrupted.Any()
				? (decimal)notInterrupted.Where(kv => IsSuccessStatusCode(kv.Key)).Sum(o => o.Value) / notInterrupted.Sum(o => o.Value)
				: 1;
		}

		public decimal GetLatestFractionSuccess() => 
			latestAwaitedResponse.Any()
			? (decimal)latestAwaitedResponse.Count(code => IsSuccessStatusCode(code)) / latestAwaitedResponse.Count()
			: 1;

		private static bool IsSuccessStatusCode(HttpStatusCode statusCode) =>
			((int)statusCode >= 200) && ((int)statusCode <= 299);

		public class CustomJsonConverter : JsonConverter<RequestStats>
		{
			public override RequestStats Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				// TODO
				return new RequestStats();
			}

			public override void Write(Utf8JsonWriter writer, RequestStats value, JsonSerializerOptions options)
			{
				writer.WriteRawValue(JsonSerializer.Serialize(value.counts));
			}
		}
	}
}
