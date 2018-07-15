using System;
using System.IO;
using NUnit.Framework;

namespace TensorflowBinariesBuildTask.Tests
{
    [Parallelizable(ParallelScope.All)]
    [TestFixture]
    public class BuildTaskTests
    {
        [Test]
        [TestCase("tensorflow", "1.2.0")]
        [TestCase("tensorflow", "1.8.0")]
        [TestCase("tensorflow", "1.9.0")]
        [TestCase("tensorflow-gpu", "1.2.0")]
        [TestCase("tensorflow-gpu", "1.8.0")]
        [TestCase("tensorflow-gpu", "1.9.0")]
        public void WindowsX64(string packageName, string packageVersion)
        {
            var task = new TensowflowBinariesBuildTask
            {
                OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, packageName, packageVersion, "libtensorflow.dll"),
                PypiPackageName = packageName,
                PypiPackageVersion = packageVersion,
                Runtime = "win",
            };

            task.Execute();

            FileAssert.Exists(task.OutputPath);
        }
    }
}
