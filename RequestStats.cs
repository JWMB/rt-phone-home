using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rt_call_home
{
	public record RequestStats
	{
		public enum ResultCategory
        {
			Success,
			Failure,
			Canceled
        }

		//private Dictionary<string, int> counts = new Dictionary<string, int>();
		private Dictionary<ResultCategory, int> counts = new Dictionary<ResultCategory, int>();
		private Queue<IRequestResult> latestAwaitedResponse = new Queue<IRequestResult>();

		public void Add(IRequestResult requestResult)
		{
			var cat = requestResult is RequestResultCanceled ? ResultCategory.Canceled
				: (requestResult is RequestResultResponse rr && IsSuccessStatusCode(rr.StatusCode) ? ResultCategory.Success : ResultCategory.Failure);

			counts[cat] = counts.TryGetValue(cat, out int count) ? count + 1 : 1;

			if (requestResult is not RequestResultCanceled)
            {
				latestAwaitedResponse.Enqueue(requestResult);
				if (latestAwaitedResponse.Count > 4)
					latestAwaitedResponse.Dequeue();
			}
		}

		public int Total => counts.Sum(o => o.Value);

		public decimal GetFractionSuccess()
        {
			var notInterrupted = counts.Where(o => o.Key != ResultCategory.Canceled).ToList();
			return notInterrupted.Any()
				? (decimal)notInterrupted.Where(kv => kv.Key == ResultCategory.Success).Sum(o => o.Value) / notInterrupted.Sum(o => o.Value)
				: 1;
		}

		public decimal GetLatestFractionSuccess() => 
			latestAwaitedResponse.Any()
			? (decimal)latestAwaitedResponse.Count(r => r is RequestResultResponse resp && IsSuccessStatusCode(resp.StatusCode)) / latestAwaitedResponse.Count()
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


	public interface IRequestResult
	{
		string GetKey();
	}

	public record RequestResultResponse : IRequestResult
	{
		public HttpStatusCode StatusCode { get; set; }

		public RequestResultResponse(HttpStatusCode statusCode)
		{
			StatusCode = statusCode;
		}

		public string GetKey() => $"{(int)StatusCode}";
	}

	public record RequestResultCanceled : IRequestResult
	{
		public string GetKey() => "Canceled";
	}

	public record RequestResultException : IRequestResult
	{
		public Exception Exception { get; set; }

        public RequestResultException(Exception exception)
        {
			Exception = exception;
        }

		public string GetKey() => Exception.GetType().Name;
    }
}
