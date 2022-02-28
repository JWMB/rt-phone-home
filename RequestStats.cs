using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rt_call_home
{
	public record RequestStats
	{
		private Dictionary<HttpStatusCode, int> counts = new Dictionary<HttpStatusCode, int>();
		public void Add(HttpStatusCode code, string message)
		{
			counts[code] = counts.TryGetValue(code, out int count) ? count + 1 : 1;
		}

		public int Total => counts.Sum(o => o.Value);

		public decimal GetFractionSuccess() => (decimal)counts.Where(kv => IsSuccessStatusCode(kv.Key)).Sum(o => o.Value) / Total;

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
