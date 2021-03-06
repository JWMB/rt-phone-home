using rt_call_home;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

using IUrlProvider urlProvider = new FileUrlProvider("urls.txt");

var client = new HttpClient();

var ipGuard = new IPGuard(client);
ipGuard.Allowed.Add(await ipGuard.Fetch());
Console.WriteLine($"Current IP:{ipGuard.CurrentIP}");

var results = new ConcurrentDictionary<Uri, RequestStats>();
var probabilities = new Dictionary<Uri, decimal>();
var totalCnt = 0;
object lockResults = new object();

await Parallel.ForEachAsync(UrlGenerator(), new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (url, cancellation) =>
{
	if (url == null)
		return;

	// Every now and then, let request run a bit longer to see if there's a response (so we can decrease intensity of unreachable URLs)
	var totalCallsToUri = results!.GetValueOrDefault(url, null)?.Total ?? 0;
	var waitForResponse = totalCallsToUri % 25 == 0;
    var timeout = waitForResponse ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(0.1);

	if (totalCnt % 2000 == 1)
    {
		if (await ipGuard.Check() == false)
        {
			Console.WriteLine($"IP guard triggered {ipGuard.CurrentIP}");
        }
	}
	if (!ipGuard.CurrentIPAllowed)
		return;

	var result = await MakeRequest(url, timeout, waitForResponse);

	UpdateStats(url, result);
});

void UpdateStats(Uri url, IRequestResult requestResult)
{
	lock (lockResults)
    {
		results.AddOrUpdate(url,
			key => Add(new RequestStats()),
			(key, stats) => Add(stats));
	}

	RequestStats Add(RequestStats stats)
    {
		stats.Add(requestResult);
		return stats;
	}

	if (!(requestResult is RequestResultCanceled))
    {
		lock (lockResults)
        {
			probabilities = results.ToDictionary(kv => kv.Key, kv => kv.Value.GetLatestFractionSuccess() + 0.1M);
		}
	}

	Interlocked.Increment(ref totalCnt);

	if (totalCnt % 50 == 0)
	{
		lock (lockResults)
		{
			PrintStats();
			SaveResults();
		}
	}
}

void PrintStats()
{
	var stats = string.Join("\n",
		results.Select(o => new { Uri = o.Key, SuccessRate = o.Value.GetFractionSuccess(), Stats = o.Value })
			.OrderBy(o => o.SuccessRate)
			.Select(o => $"{(int)(o.SuccessRate * 100)}% ({o.Stats.Total}) - {o.Uri.Host}"));
	Console.Clear();
	Console.WriteLine($"---{totalCnt}---\n{stats}");
}

void SaveResults()
{
	var forSerialization = results.ToDictionary(o => o.Key.ToString(), o => o.Value);
	var options = new JsonSerializerOptions { WriteIndented = true, };
	options.Converters.Add(new RequestStats.CustomJsonConverter());
	try
	{
		File.WriteAllText("result.json", JsonSerializer.Serialize(forSerialization, options));
	}
	catch (IOException ex) when (ex.HResult == -2147024864)
	{
		Console.WriteLine(ex.Message);
	}
}

async Task<IRequestResult> MakeRequest(Uri url, TimeSpan? timeout = null, bool longTimeout = true)
{
	try
	{
		var cancelSource = new CancellationTokenSource();
		if (timeout.HasValue)
			cancelSource.CancelAfter((int)timeout.Value.TotalMilliseconds);

		HttpResponseMessage response;
		if (true) // for testing
        {
			var delay = new Random((int)DateTime.Now.Ticks).NextDouble() * 500;
			try
			{ // see https://github.com/dotnet/runtime/issues/45650
				await Task.Delay((int)delay, cancelSource.Token);
			}
			catch { }
			response = new HttpResponseMessage(cancelSource.IsCancellationRequested ? HttpStatusCode.RequestTimeout : (DateTime.Now.Ticks % 5 == 0 ? HttpStatusCode.OK : HttpStatusCode.InternalServerError));
        }
		else
			response = await client.GetAsync(url, cancelSource.Token);

		return new RequestResultResponse(response.StatusCode); // Message = $"server={response.Headers.GetValuesOrNull("server")?.FirstOrDefault() ?? "N/A"}" };
	}
	catch (TaskCanceledException tcEx)
	{
		return longTimeout ? new RequestResultException(tcEx) : new RequestResultCanceled();
	}
	catch (OperationCanceledException ocEx)
    {
		return new RequestResultException(ocEx);
	}
	catch (HttpRequestException hEx)
    {
		if (hEx.HResult != -2147467259 // A connection attempt failed because the connected party did not properly respond after a period of time, or...
			&& hEx.HResult == -2146232800) // SSL fail or An error occurred while sending the request
			Console.WriteLine($"{url} {hEx.StatusCode} / {hEx.HResult} {hEx.Message}");
		return new RequestResultException(hEx);
	}
	catch (Exception ex)
	{
		Console.WriteLine($"{url} {ex.GetType().Name}: {ex.Message}");
		return new RequestResultException(ex);
	}
}

IEnumerable<Uri?> UrlGenerator()
{
	var rnd = new Random();
	//var cnt = 0;
	while (true)
	{
		var urls = urlProvider.Urls;
		if (urls.Count == 0)
			yield return null;
		else
		{
			var withProbabilities = urls.Select(url => new { Probablility = probabilities.GetValueOrDefault(url, 1M), Url = url });
			var distributed = withProbabilities.Fold(new { PIndex = 0M, Url = new Uri("http://a") }, (p, c) => new { PIndex = p.PIndex + c.Probablility, Url = c.Url }).ToList();
			var whereInDistribution = (decimal)rnd.NextDouble() * distributed.Last().PIndex;
			var index = distributed.FindIndex(o => o.PIndex >= whereInDistribution);
			var url = distributed[index].Url;

			//var url = urls[cnt++ % urls.Count];
			yield return url;
		}
	}
}
