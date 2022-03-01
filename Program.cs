using rt_call_home;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

using IUrlProvider urlProvider = new FileUrlProvider("urls.txt");
var client = new HttpClient();
client.Timeout = TimeSpan.FromSeconds(2);

var results = new ConcurrentDictionary<Uri, RequestStats>();
var totalCnt = 0;

await Parallel.ForEachAsync(UrlGenerator(), new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (url, cancellation) =>
{
	if (url == null)
		return;

	var info = await MakeRequest(url);
	UpdateStats(url, info.Item1, info.Item2);
});

void UpdateStats(Uri url, HttpStatusCode statusCode, string info)
{
	results.AddOrUpdate(url,
	key =>
	{
		var stats = new RequestStats();
		stats.Add(statusCode, info);
		return stats;
	},
	(key, stats) =>
	{
		stats.Add(statusCode, info);
		return stats;
	});

	Interlocked.Increment(ref totalCnt);
	if (totalCnt % 50 == 0)
	{
		var stats = string.Join("\n",
			results.Select(o => new { Uri = o.Key, SuccessRate = o.Value.GetFractionSuccess(), Stats = o.Value })
			.OrderByDescending(o => o.SuccessRate)
			.Select(o => $"{(int)(o.SuccessRate * 100)}% ({o.Stats.Total}) - {o.Uri.Host}"));
		Console.WriteLine($"---{totalCnt}---\n{stats}");

		var forSerialization = results.ToDictionary(o => o.Key.ToString(), o => o.Value);
		var options = new JsonSerializerOptions { WriteIndented = true, };
		options.Converters.Add(new RequestStats.CustomJsonConverter());
		File.WriteAllText("result.json", JsonSerializer.Serialize(forSerialization, options));
	}
}

async Task<(HttpStatusCode, string)> MakeRequest(Uri url)
{
	try
	{
		//await Task.Delay(1000);
		//return (HttpStatusCode.OK, "");
		var r = await client.GetAsync(url);
		return (r.StatusCode, $"server={r.Headers.GetValuesOrNull("server")?.FirstOrDefault() ?? "N/A"}");
	}
	catch (TaskCanceledException)
	{
		return (HttpStatusCode.RequestTimeout, $"Timeout");
	}
	catch (OperationCanceledException ocEx)
    {
		return (HttpStatusCode.InsufficientStorage, $"{ocEx.GetType().Name}: {ocEx.Message}");
	}
	catch (HttpRequestException hEx)
    {
		if (hEx.HResult == -2147467259) // A connection attempt failed because the connected party did not properly respond after a period of time, or...
			return (HttpStatusCode.RequestTimeout, $"{hEx.GetType().Name}: {hEx.Message}");
		if (hEx.HResult == -2146232800) // SSL fail or An error occurred while sending the request
			return (HttpStatusCode.BadGateway, $"{hEx.GetType().Name}: {hEx.Message}");
		Console.WriteLine($"{url} {hEx.StatusCode} / {hEx.HResult} {hEx.Message}");
		return (HttpStatusCode.InsufficientStorage, $"{hEx.GetType().Name}: {hEx.Message}");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"{url} {ex.GetType().Name}: {ex.Message}");
		return (HttpStatusCode.InsufficientStorage, $"{ex.GetType().Name}: {ex.Message}");
	}
}

IEnumerable<Uri?> UrlGenerator()
{
	var cnt = 0;
	while (true)
	{
		var urls = urlProvider.Urls;
		if (urls.Count == 0)
			yield return null;
		else
		{
			var url = urls[cnt++ % urls.Count];
			yield return url;
		}
	}
}

static class HeadersExtensions
{
	public static IEnumerable<string>? GetValuesOrNull(this System.Net.Http.Headers.HttpResponseHeaders headers, string name)
	{
		return headers.Contains(name) ? headers.GetValues(name) : null;
	}
}
