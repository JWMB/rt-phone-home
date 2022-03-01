namespace rt_call_home
{
	static class IEnumerableExtensions
	{
		public static IEnumerable<TSeed> Fold<TInput, TSeed>(this IEnumerable<TInput> input, TSeed seed, Func<TSeed, TInput, TSeed> folder)
		{
			foreach (var item in input)
			{
				seed = folder(seed, item);
				yield return seed;
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
}
