using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using TensorflowBinariesBuildTask.Core;

namespace TensorflowBinariesBuildTask.Tests
{
    [Parallelizable(ParallelScope.All)]
    [TestFixture]
    public class BuildTaskTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr TF_Version();

        [Test]
        [TestCaseSource(nameof(CreateTestCasesUnix))]
        public async Task E2ETestUnixAsync(
            string device,
            string os,
            string version,
            bool shouldSkipTesting)
        {
            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"tensorflow-{device}", version);

            await TensowflowBinariesBuildTaskUtils.BuildFromStorageAsync(
                device: device,
                os: os,
                version: version,
                outputDir: outputDir);

            if (!shouldSkipTesting)
            {
                ValidateNativeBinaryVersion(outputDir, version);
            }
        }

        [Test]
        [TestCaseSource(nameof(CreateTestCasesWindows))]
        public async Task E2ETestWindowsAsync(
            string runtime,
            string packageName,
            string packageVersion,
            bool shouldSkipTesting)
        {
            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, packageName, packageVersion);
            string outputFileName;
            string outputFrameworkFileName;
            const string pythonVersion = "cp36";

            IDictionary<string, string> filesToExtract = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        runtime = "osx";
                        outputFileName = filesToExtract["_pywrap_tensorflow_internal.so"] = "libtensorflow.dylib";
                        outputFrameworkFileName = filesToExtract["libtensorflow_framework.so"] = "libtensorflow_framework.dylib";
                    }
                    else
                    {
                        outputFileName = filesToExtract["_pywrap_tensorflow_internal.so"] = "libtensorflow.so";
                        outputFrameworkFileName = filesToExtract["libtensorflow_framework.so"] = "libtensorflow_framework.so";
                        runtime = "linux";
                    }
                    break;
                default:
                    outputFileName = filesToExtract["_pywrap_tensorflow_internal.pyd"] = "libtensorflow.dll";
                    outputFrameworkFileName = null;
                    break;
            }

            var libFullPath = Path.Combine(outputDir, outputFileName);

            await TensowflowBinariesBuildTaskUtils.BuildFromWheelAsync(
                runtime: runtime,
                pythonVersion: pythonVersion,
                pypiPackageName: packageName,
                pypiPackageVersion: packageVersion,
                outputDir: outputDir,
                filesToExtract: filesToExtract);

            FileAssert.Exists(libFullPath);

            if (!shouldSkipTesting)
            {
                ValidateNativeBinaryVersion(outputDir, packageVersion);
            }
        }

        public static IEnumerable<TestCaseData> CreateTestCasesWindows()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    yield break;
                default:
                    break;
            }

            var runtime = "win";
            foreach (var package in new[] { "tensorflow", "tensorflow-gpu" })
            {
                var shouldSkipTesting = package.Contains("gpu");
                foreach (var version in new[]
                {
                    "1.2.0", "1.2.1", "1.3.0", "1.4.0", "1.5.0", "1.5.1", "1.6.0", "1.7.0", "1.7.1", "1.8.0", "1.9.0",
                })
                {
                    yield return new TestCaseData(runtime, package, version, shouldSkipTesting).SetName($"[{runtime}][{package}][{version}]");
                }
            }
        }

        public static IEnumerable<TestCaseData> CreateTestCasesUnix()
        {
            string os;
            var devices = new List<string> { "cpu" };
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                //case PlatformID.Win32NT:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        os = "darwin";
                    }
                    else
                    {
                        os = "linux";
                        devices.Add("gpu");
                    }
                    break;
                default:
                    yield break;
            }

            foreach (var device in devices)
            {
                var shouldSkipTesting = device.Contains("gpu");
                foreach (var version in new[]
                {
                    "1.9.0",
                })
                {
                    yield return new TestCaseData(device, os, version, shouldSkipTesting).SetName($"[{os}][{device}][{version}]");
                }
            }
        }

        private static void ValidateNativeBinaryVersion(string outputDir, string expectedVersion)
        {
            string extension;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        extension = ".dylib";
                    }
                    else
                    {
                        extension = ".so";
                    }
                    break;
                default:
                    extension = ".dll";
                    break;
            }

            var libFullPath = Path.Combine(outputDir, $"libtensorflow{extension}");

            var pLib = NativeMethods.LoadLibrary(libFullPath);
            Console.WriteLine($"{nameof(pLib)}: {pLib} ({libFullPath})");

            var pFunc = Marshal.GetDelegateForFunctionPointer<TF_Version>(NativeMethods.GetProcAddress(pLib, nameof(TF_Version)));
            Console.WriteLine($"{nameof(pFunc)}: {pFunc}");

            var versionPtr = pFunc();
            var version = Marshal.PtrToStringAnsi(versionPtr);
            version.Should().Contain(expectedVersion);

            NativeMethods.FreeLibrary(pLib);
        }
    }

    public static class NativeMethods
    {
        private const int RTLD_NOW = 2;

        public static IntPtr LoadLibrary(string lpFileName)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? LoadLibraryOSX(lpFileName, RTLD_NOW) : LoadLibraryLinux(lpFileName, RTLD_NOW);
                default:
                    return LoadLibraryWindows(lpFileName);
            }
        }

        public static bool FreeLibrary(IntPtr hModule)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FreeLibraryOSX(hModule) == 0 : FreeLibraryLinux(hModule) == 0;
                default:
                    return FreeLibraryWindows(hModule);
            }
        }

        public static IntPtr GetProcAddress(IntPtr hModule, string procedureName)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GetProcAddressOSX(hModule, procedureName) : GetProcAddressLinux(hModule, procedureName);
                default:
                    return GetProcAddressWindows(hModule, procedureName);
            }
        }

        // http://dimitry-i.blogspot.com/2013/01/mononet-how-to-dynamically-load-native.html
        [DllImport("kernel32", EntryPoint = "LoadLibrary")]
        private static extern IntPtr LoadLibraryWindows(string lpFileName);

        [DllImport("kernel32", EntryPoint = "FreeLibrary", SetLastError = true)]
        private static extern bool FreeLibraryWindows(IntPtr hModule);

        [DllImport("kernel32", EntryPoint = "GetProcAddress")]
        private static extern IntPtr GetProcAddressWindows(IntPtr hModule, string procedureName);

        [DllImport("libdl", EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryLinux(String fileName, int flags);

        [DllImport("libdl", EntryPoint = "dlsym")]
        private static extern IntPtr GetProcAddressLinux(IntPtr handle, String symbol);

        [DllImport("libdl", EntryPoint = "dlclose")]
        private static extern int FreeLibraryLinux(IntPtr handle);

        //[DllImport("libdl", EntryPoint = "dlerror")]
        //private static extern IntPtr dlerror();

        [DllImport("libdl", EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryOSX(String fileName, int flags);

        [DllImport("libdl", EntryPoint = "dlsym")]
        private static extern IntPtr GetProcAddressOSX(IntPtr handle, String symbol);

        [DllImport("libdl", EntryPoint = "dlclose")]
        private static extern int FreeLibraryOSX(IntPtr handle);

        //[DllImport("libc", EntryPoint = "dlerror")]
        //private static extern IntPtr dlerror();
    }
}
