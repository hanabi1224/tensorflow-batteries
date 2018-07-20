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
        [TestCaseSource(nameof(CreateTestCases))]
        public async Task E2EAsync(
            string runtime,
            string packageName,
            string packageVersion,
            bool shouldSkipTesting)
        {
            Environment.SetEnvironmentVariable("LD_DEBUG", "unused");

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
                        outputFrameworkFileName = filesToExtract["libtensorflow_framework.so"] = null;
                        runtime = "linux";
                    }
                    break;
                default:
                    outputFileName = filesToExtract["_pywrap_tensorflow_internal.pyd"] = "libtensorflow.dll";
                    outputFrameworkFileName = null;
                    break;
            }

            var libFullPath = Path.Combine(outputDir, outputFileName);

            await TensowflowBinariesBuildTaskUtils.ExecuteAsync(
                runtime: runtime,
                pythonVersion: pythonVersion,
                pypiPackageName: packageName,
                pypiPackageVersion: packageVersion,
                outputDir: outputDir,
                filesToExtract: filesToExtract);

            FileAssert.Exists(libFullPath);

            if (!shouldSkipTesting)
            {
                IntPtr pFrameworkLib = IntPtr.Zero;
                if (!string.IsNullOrEmpty(outputFrameworkFileName))
                {
                    pFrameworkLib = NativeMethods.LoadLibrary(Path.Combine(outputDir, outputFrameworkFileName));
                    Console.WriteLine($"{nameof(pFrameworkLib)}: {pFrameworkLib}");
                }

                var pLib = NativeMethods.LoadLibrary(libFullPath);
                Console.WriteLine($"{nameof(pLib)}: {pLib}");

                var pFunc = Marshal.GetDelegateForFunctionPointer<TF_Version>(NativeMethods.GetProcAddress(pLib, nameof(TF_Version)));
                Console.WriteLine($"{nameof(pFunc)}: {pFunc}");

                var versionPtr = pFunc();
                var version = Marshal.PtrToStringAnsi(versionPtr);
                version.Should().Contain(packageVersion);

                NativeMethods.FreeLibrary(pLib);

                if (pFrameworkLib != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(pFrameworkLib);
                }
            }
        }

        public static IEnumerable<TestCaseData> CreateTestCases()
        {
            string runtime;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        runtime = "osx";
                    }
                    else
                    {
                        runtime = "linux";
                    }
                    break;
                default:
                    runtime = "win";
                    break;
            }

            foreach (var package in new[] { "tensorflow", "tensorflow-gpu" })
            {
                var shouldSkipTesting = package.Contains("gpu");
                foreach (var version in new[] { "1.2", "1.2.1", "1.3", "1.4", "1.5", "1.5.1", "1.6", "1.7", "1.7.1", "1.8", "1.9" })
                {
                    yield return new TestCaseData(runtime, package, version, shouldSkipTesting).SetName($"[{runtime}][{package}][{version}]");
                }
            }
        }
    }

    public static class NativeMethods
    {
        private const int RTLD_NOW = 2;

        public static IntPtr LoadLibrary(string lpFileName)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    return LoadLibraryOSX(lpFileName, RTLD_NOW);
                case PlatformID.Unix:
                    return LoadLibraryLinux(lpFileName, RTLD_NOW);
                default:
                    return LoadLibraryWindows(lpFileName);
            }
        }

        public static bool FreeLibrary(IntPtr hModule)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    return FreeLibraryOSX(hModule) == 0;
                case PlatformID.Unix:
                    return FreeLibraryLinux(hModule) == 0;
                default:
                    return FreeLibraryWindows(hModule);
            }
        }

        public static IntPtr GetProcAddress(IntPtr hModule, string procedureName)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    return GetProcAddressOSX(hModule, procedureName);
                case PlatformID.Unix:
                    return GetProcAddressLinux(hModule, procedureName);
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

        [DllImport("libc", EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryOSX(String fileName, int flags);

        [DllImport("libc", EntryPoint = "dlsym")]
        private static extern IntPtr GetProcAddressOSX(IntPtr handle, String symbol);

        [DllImport("libc", EntryPoint = "dlclose")]
        private static extern int FreeLibraryOSX(IntPtr handle);

        //[DllImport("libc", EntryPoint = "dlerror")]
        //private static extern IntPtr dlerror();
    }
}
