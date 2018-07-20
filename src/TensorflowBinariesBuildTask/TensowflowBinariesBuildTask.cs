using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using TensorflowBinariesBuildTask.Core;

namespace TensorflowBinariesBuildTask
{
    public class TensowflowBinariesBuildTask : Microsoft.Build.Utilities.Task
    {
        public string PypiPackageName { get; set; }

        public string PypiPackageVersion { get; set; }

        public string PythonVersion { get; set; } = "cp36";

        public string Runtime { get; set; }

        public string OutputDir { get; set; }

        public ITaskItem[] FilesToExtract { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private Task<bool> ExecuteAsync()
        {
            if (!(FilesToExtract?.Length > 0))
            {
                throw new ArgumentException($"{nameof(FilesToExtract)} property cannot be empty");
            }

            var filesToExtract = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in FilesToExtract)
            {
                filesToExtract[item.GetMetadata("OriginalFileName")] = item.GetMetadata("TargetFileName");
            }

            return TensowflowBinariesBuildTaskUtils.ExecuteAsync(
                runtime: Runtime,
                pythonVersion: PythonVersion,
                pypiPackageName: PypiPackageName,
                pypiPackageVersion: PypiPackageVersion,
                outputDir: OutputDir,
                filesToExtract: filesToExtract);
        }
    }
}
