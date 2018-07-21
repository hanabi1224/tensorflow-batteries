using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace TensorflowBinariesBuildTask.Core
{
    public static class TensowflowBinariesBuildTaskUtils
    {
        private const string PypiUrlTemplate = @"https://pypi.org/project/{0}/{1}/#files";

        public static async Task<bool> ExecuteAsync(
            string runtime,
            string pythonVersion,
            string pypiPackageName,
            string pypiPackageVersion,
            string outputDir,
            IDictionary<string, string> filesToExtract)
        {
            var url = string.Format(PypiUrlTemplate, pypiPackageName, pypiPackageVersion);
            using (var httpClient = new HttpClient())
            {
                var stream = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
                var htmlDoc = new HtmlDocument();
                htmlDoc.Load(stream);
                var anchors = htmlDoc.DocumentNode.SelectNodes(@".//div[@id='files']//td//a");
                var selection = anchors
                    .Where(_ => _.InnerText.ToLowerInvariant().Contains(pythonVersion))
                    .First(_ => _.InnerText.ToLowerInvariant().Contains(runtime.ToLowerInvariant()));

                var wheelLink = selection.GetAttributeValue("href", null);

                using (var ms = new MemoryStream(await httpClient.GetByteArrayAsync(wheelLink)))
                using (var zip = new ZipArchive(ms))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (filesToExtract.TryGetValue(entry.Name, out var targetFileName))
                        {
                            if (!Directory.Exists(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }

                            entry.ExtractToFile(Path.Combine(outputDir, string.IsNullOrWhiteSpace(targetFileName) ? entry.Name : targetFileName), true);
                        }
                    }
                }
            }

            return true;
        }
    }
}
