using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace TensorflowBinariesBuildTask
{
    public class TensowflowBinariesBuildTask : Microsoft.Build.Utilities.Task
    {
        private const string PypiUrlTemplate = @"https://pypi.org/project/{0}/{1}/#files";

        public string PypiPackageName { get; set; }

        public string PypiPackageVersion { get; set; }

        public string Runtime { get; set; }

        public string OutputPath { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            var url = string.Format(PypiUrlTemplate, PypiPackageName, PypiPackageVersion);
            using (var httpClient = new HttpClient())
            {
                var stream = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
                var htmlDoc = new HtmlDocument();
                htmlDoc.Load(stream);
                var anchors = htmlDoc.DocumentNode.SelectNodes(@".//div[@id='files']//td//a");
                var selection = anchors.FirstOrDefault(_ => _.InnerText.ToLowerInvariant().Contains(Runtime.ToLowerInvariant()));
                var wheelLink = selection.GetAttributeValue("href", null);

                using (var ms = new MemoryStream(await httpClient.GetByteArrayAsync(wheelLink)))
                using (var zip = new ZipArchive(ms))
                {
                    foreach (var entry in zip.Entries.Where(_ => _.FullName.EndsWith(".pyd")))
                    {
                        var dir = Path.GetDirectoryName(OutputPath);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        entry.ExtractToFile(OutputPath, true);
                        break;
                    }
                }
            }

            return true;
        }
    }
}
