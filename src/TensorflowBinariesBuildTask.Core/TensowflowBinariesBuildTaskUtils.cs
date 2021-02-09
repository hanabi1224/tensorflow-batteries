using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace TensorflowBinariesBuildTask.Core
{
    public static class TensowflowBinariesBuildTaskUtils
    {
        private const string PypiUrlTemplate = @"https://pypi.org/project/{0}/{1}/#files";

        private const string GoogleStorageIndexUrl = @"https://storage.googleapis.com/tensorflow/";

        public static async Task<bool> BuildFromWheelAsync(
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
                var anchors = htmlDoc.DocumentNode.SelectNodes(@".//div[@id='files']//th//a");
                var selection = anchors
                    .Where(_ => _.InnerText.ToLowerInvariant().Contains(pythonVersion))
                    .First(_ => _.InnerText.ToLowerInvariant().Contains(runtime.ToLowerInvariant()));

                var wheelLink = selection.GetAttributeValue("href", null);

                using (var ms = new MemoryStream(await httpClient.GetByteArrayAsync(wheelLink).ConfigureAwait(false)))
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

        public static async Task<bool> BuildFromStorageAsync(
            string device,
            string os,
            string version,
            string outputDir)
        {
            using (var httpClient = new HttpClient())
            {
                var prefix = $"libtensorflow-{device}-{os}-";

                var indexDoc = XDocument.Load(await httpClient.GetStreamAsync(GoogleStorageIndexUrl).ConfigureAwait(false));
                var ns = "{" + indexDoc.Root.GetDefaultNamespace().NamespaceName + "}";
                var keyNodes = indexDoc.Document.Elements().Single().Elements(ns + "Contents").Select(_ => _.Element(ns + "Key")).ToList();
                var keyNode = keyNodes
                    .Where(_ => !_.Value.Contains("rc") && _.Value.Contains(prefix) && _.Value.Contains(version + "."))
                    .Single();

                var packageUrl = $"{GoogleStorageIndexUrl}{keyNode.Value}";

                var extentions = new[] { ".dll", ".so", ".dylib" };

                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                var ms = new MemoryStream(await httpClient.GetByteArrayAsync(packageUrl).ConfigureAwait(false));
                if (packageUrl.EndsWith(".tar.gz"))
                {
                    var gzipStream = new GZipInputStream(ms);
                    using (var tarIn = new TarInputStream(gzipStream, Encoding.UTF8))
                    {
                        TarEntry tarEntry;
                        while ((tarEntry = tarIn.GetNextEntry()) != null)
                        {
                            if (tarEntry.IsDirectory)
                            {
                                continue;
                            }

                            var fileName = Path.GetFileName(tarEntry.Name);
                            if (extentions.Any(_ => fileName.EndsWith(_, StringComparison.OrdinalIgnoreCase)))
                            {
                                var targetFileName = fileName;
                                if (os.IndexOf("darwin", StringComparison.InvariantCultureIgnoreCase) >= 0)
                                {
                                    targetFileName = $"{Path.GetFileNameWithoutExtension(fileName)}.dylib";
                                }

                                using (var fs = File.OpenWrite(Path.Combine(outputDir, targetFileName)))
                                {
                                    tarIn.CopyEntryContents(fs);
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (ms)
                    using (var zip = new ZipArchive(ms))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            if (extentions.Any(_ => entry.Name.EndsWith(_, StringComparison.OrdinalIgnoreCase)))
                            {
                                entry.ExtractToFile(Path.Combine(outputDir, entry.Name), true);
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}