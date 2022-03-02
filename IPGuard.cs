using System.Net;

namespace rt_call_home
{
    public class IPGuard
    {
        private readonly HttpClient client;
        private IPAddress current;
        public List<IPAddress> Allowed = new List<IPAddress>();

        public IPGuard(HttpClient client, IEnumerable<IPAddress>? allowed = null)
        {
            current = IPAddress.Parse("0.0.0.0");
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
            var externalIpString = (await client.GetStringAsync("https://icanhazip.com")).Replace("\\r", "").Replace("\\n", "").Trim();
            current = IPAddress.Parse(externalIpString);
            return current;
        }
    }
}
