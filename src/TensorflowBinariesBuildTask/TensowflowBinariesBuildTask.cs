using System.Threading.Tasks;
using TensorflowBinariesBuildTask.Core;

namespace TensorflowBinariesBuildTask
{
    public class TensowflowBinariesBuildTask : Microsoft.Build.Utilities.Task
    {
        public string PypiPackageName { get; set; }

        public string PypiPackageVersion { get; set; }

        public string PythonVersion { get; set; } = "cp36";

        public string Runtime { get; set; }

        public string OutputPath { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private Task<bool> ExecuteAsync()
        {
            return TensowflowBinariesBuildTaskUtils.ExecuteAsync(
                runtime: Runtime,
                pythonVersion: PythonVersion,
                pypiPackageName: PypiPackageName,
                pypiPackageVersion: PypiPackageVersion,
                outputPath: OutputPath);
        }
    }
}
