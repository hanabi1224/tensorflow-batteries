using System.Threading.Tasks;
using TensorflowBinariesBuildTask.Core;

namespace TensorflowBinariesBuildTask
{
    public class TensowflowBinariesBuildFromStorageTask : Microsoft.Build.Utilities.Task
    {
        public string OS { get; set; }

        public string Device { get; set; }

        public string Version { get; set; }

        public string OutputDir { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private Task<bool> ExecuteAsync()
        {
            return TensowflowBinariesBuildTaskUtils.BuildFromStorageAsync(
                os: OS,
                device: Device,
                version: Version,
                outputDir: OutputDir);
        }
    }
}
