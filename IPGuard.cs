using System.Net;

namespace rt_call_home
{
    public class IPGuard
    {
        private readonly HttpClient client;
        public List<IPAddress> Allowed = new List<IPAddress>();
        private IPAddress Undetermined = IPAddress.Parse("0.0.0.0");
        private IPAddress current;

        public IPGuard(HttpClient client, IEnumerable<IPAddress>? allowed = null)
        {
            current = Undetermined;
            this.client = client;
            if (allowed != null)
                Allowed = allowed.ToList();
        }

        public IPAddress CurrentIP => current;
        public bool CurrentIPAllowed => Allowed.Contains(current);

        public async Task<bool> Check()
        {
            current = await Fetch();
            return CurrentIPAllowed;
        }

        public async Task<IPAddress> Fetch()
        {
            try
            {
                var externalIpString = (await client.GetStringAsync("https://icanhazip.com")).Replace("\\r", "").Replace("\\n", "").Trim();
                current = IPAddress.Parse(externalIpString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(IPGuard)} fetch: {ex.Message}");
                if (current == Undetermined)
                    throw;
            }
            return current;
        }
    }
}
