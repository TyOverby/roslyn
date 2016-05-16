using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Roslyn.Test.Performance.Utilities.TestUtilities;

namespace Roslyn.Test.Performance.Utilities
{
    public class TraceManager : ITraceManager
    {
        private readonly ScenarioGenerator _scenarioGenerator;
        private readonly string _cpcPath;

        private int _startEventAbsoluteInstance = 1;
        private int _stopEventAbsoluteInstance = 1;

        public TraceManager(
            string cpcPath,
            string scenarioPath) : base()
        {
            _cpcPath = cpcPath;
            _scenarioGenerator = new ScenarioGenerator(scenarioPath);
        }

        public bool HasWarmUpIteration
        {
            get
            {
                return true;
            }
        }

        // Cleanup the results directory and files before every run
        public void Initialize()
        {
            var consumptionTempResultsPath = Path.Combine(GetCPCDirectoryPath(), "ConsumptionTempResults.xml");
            if (File.Exists(consumptionTempResultsPath))
            {
                File.Delete(consumptionTempResultsPath);
            }

            if (Directory.Exists(GetCPCDirectoryPath()))
            {
                var databackDirectories = Directory.GetDirectories(GetCPCDirectoryPath(), "DataBackup*", SearchOption.AllDirectories);
                foreach (var databackDirectory in databackDirectories)
                {
                    Directory.Delete(databackDirectory, true);
                }
            }
        }

        public void Setup()
        {
            ShellOutVital(_cpcPath, "/Setup /DisableArchive", workingDirectory: "");
        }

        public void Start()
        {
            ShellOutVital(_cpcPath, "/Start /DisableArchive", workingDirectory: "");
        }

        public void Stop()
        {
            var scenariosXmlPath = Path.Combine(GetCPCDirectoryPath(), "scenarios.xml");
            var consumptionTempResultsPath = Path.Combine(GetCPCDirectoryPath(), "ConsumptionTempResults.xml");
            ShellOutVital(_cpcPath, $"/Stop /DisableArchive /ScenarioPath=\"{scenariosXmlPath}\" /ConsumptionTempResultsPath=\"{consumptionTempResultsPath}\"", workingDirectory: "");
        }

        public void Cleanup()
        {
            ShellOutVital(_cpcPath, "/Cleanup /DisableArchive", workingDirectory: "");
        }

        public void StartScenarios()
        {
            _scenarioGenerator.AddScenariosFileStart();
        }

        public void StartScenario(string scenarioName, string processName)
        {
            _scenarioGenerator.AddStartScenario(scenarioName, processName);
        }

        public void StartEvent()
        {
            _scenarioGenerator.AddStartEvent(_startEventAbsoluteInstance);
            _startEventAbsoluteInstance++;
        }

        public void EndEvent()
        {
            _scenarioGenerator.AddEndEvent();
            _stopEventAbsoluteInstance++;
        }

        public void EndScenario()
        {
            _scenarioGenerator.AddEndScenario();
        }

        public void EndScenarios()
        {
            _scenarioGenerator.AddScenariosFileEnd();
        }

        public void WriteScenarios(string[] scenarios)
        {
            foreach (var line in scenarios)
            {
                _scenarioGenerator.AddLine(line);
            }
        }

        public void WriteScenariosFileToDisk()
        {
            _scenarioGenerator.WriteToDisk();
        }

        public void ResetScenarioGenerator()
        {
            _scenarioGenerator.Initialize();
            _startEventAbsoluteInstance = 1;
            _stopEventAbsoluteInstance = 1;
        }
    }
}
