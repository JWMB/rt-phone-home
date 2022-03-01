namespace rt_call_home
{
	interface IUrlProvider : IDisposable
	{
		List<Uri> Urls { get; }
	}

	class FileUrlProvider : IUrlProvider
	{
		private bool reloadUrls;
		private FileInfo fileInfo;
		private FileSystemWatcher fileWatcher;
		public FileUrlProvider(string path)
		{
			fileInfo = new FileInfo(path);
			fileWatcher = new FileSystemWatcher(fileInfo.Directory.FullName, fileInfo.Name);
			fileWatcher.EnableRaisingEvents = true;
			fileWatcher.Changed += (object sender, FileSystemEventArgs e) => reloadUrls = true;

			reloadUrls = true;
		}

		public void Dispose()
		{
			fileWatcher.Dispose();
		}

		private object lockObject = new object();
		private List<Uri> urls = new List<Uri>();
		public List<Uri> Urls
		{
			get
			{
				if (reloadUrls)
				{
					lock (lockObject)
					{
						reloadUrls = false;
						urls = fileInfo.Exists
							? Parse(File.ReadAllText(fileInfo.FullName))
							: new List<Uri>();
						Console.WriteLine($"Changes detected in {fileInfo.Name} - {urls.Count} urls");
					}
				}
				return urls;
			}
		}

		public static List<Uri> Parse(string input)
		{
			return input.Trim().Split('\n')
				.Where(o => !o.StartsWith("//"))
				.Select(o => o.Trim())
				.Where(o => o.Length > 0)
				.Select(o => Uri.TryCreate(o, UriKind.Absolute, out var uri) ? uri : null)
				.OfType<Uri>()
				.ToList();
		}
	}

	class FixedUrlProvider : IUrlProvider, IDisposable
	{
		public List<Uri> Urls => FileUrlProvider.Parse(Content);
		private string Content =
	@"
// https://rt.com
https://sweden.mid.ru/documents/19711091/0/fon.jpg
// https://kremlin.ru
";
		public void Dispose() { }
	}
}
