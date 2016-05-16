using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using static Roslyn.Test.Performance.Utilities.TestUtilities;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Roslyn.Test.Performance.Utilities
{
    public static class RuntimeSettings
    {

        public static ILogger logger = new ConsoleAndFileLogger();
        public static bool isVerbose = true;
        public static bool isRunnerAttached = false;
    }

    public class Tools
    {

        void CopyDirectory(string source, string destination, string argument = @"/mir")
        {
            var result = ShellOut("Robocopy", $"{argument} {source} {destination}", "");

            // Robocopy has a success exit code from 0 - 7
            if (result.Code > 7)
            {
                throw new IOException($"Failed to copy \"{source}\" to \"{destination}\".");
            }
        }

        /// Logs a message.
        ///
        /// The actual implementation of this method may change depending on
        /// if the script is being run standalone or through the test runner.
        static void Log(string info)
        {
            Console.WriteLine(info);
        }

        /// Logs the result of a finished process
        static void LogProcessResult(ProcessResult result)
        {
            Log(String.Format("The process \"{0}\" {1} with code {2}",
                $"{result.ExecutablePath} {result.Args}",
                result.Failed ? "failed" : "succeeded",
                result.Code));
            Log($"Standard Out:\n{result.StdOut}");
            Log($"\nStandard Error:\n{result.StdErr}");
        }
        /// Takes a consumptionTempResults file and converts to csv file
        /// Each info contains the <ScenarioName, Metric Key, Metric value>
        public static bool ConvertConsumptionToCsv(string source, string destination, string requiredMetricKey, ILogger logger)
        {
            logger.Log("Entering ConvertConsumptionToCsv");
            if (!File.Exists(source))
            {
                logger.Log($"File {source} does not exist");
                return false;
            }

            try
            {
                var result = new List<string>();
                string currentScenarioName = null;

                using (XmlReader xmlReader = XmlReader.Create(source))
                {
                    while (xmlReader.Read())
                    {
                        if ((xmlReader.NodeType == XmlNodeType.Element))
                        {
                            if (xmlReader.Name.Equals("ScenarioResult"))
                            {
                                currentScenarioName = xmlReader.GetAttribute("Name");

                                // These are not test results
                                if (string.Equals(currentScenarioName, "..TestDiagnostics.."))
                                {
                                    currentScenarioName = null;
                                }
                            }
                            else if (currentScenarioName != null && xmlReader.Name.Equals("CounterResult"))
                            {
                                var metricKey = xmlReader.GetAttribute("Name");

                                if (string.Equals(metricKey, requiredMetricKey))
                                {
                                    var metricScale = xmlReader.GetAttribute("Units");
                                    xmlReader.Read();
                                    var metricvalue = xmlReader.Value;
                                    result.Add($"{currentScenarioName}, {metricKey} ({metricScale}), {metricvalue}");
                                }
                            }
                        }
                    }
                }

                File.WriteAllLines(destination, result);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                return false;
            }

            return true;
        }

        /// Gets a csv file with metrics and converts them to ViBench supported JSON file
        public static string GetViBenchJsonFromCsv(string compilerTimeCsvFilePath, string execTimeCsvFilePath, string fileSizeCsvFilePath)
        {
            RuntimeSettings.logger.Log("Convert the csv to JSON using ViBench tool");
            string branch = StdoutFrom("git", "rev-parse --abbrev-ref HEAD");
            string date = FirstLine(StdoutFrom("git", $"show --format=\"%aI\" {branch} --"));
            string hash = FirstLine(StdoutFrom("git", $"show --format=\"%h\" {branch} --"));
            string longHash = FirstLine(StdoutFrom("git", $"show --format=\"%H\" {branch} --"));
            string username = StdoutFrom("whoami");
            string machineName = StdoutFrom("hostname");
            string architecture = System.Environment.Is64BitOperatingSystem ? "x86-64" : "x86";

            // File locations
            string outJson = Path.Combine(GetCPCDirectoryPath(), $"Roslyn-{longHash}.json");

            // ViBenchToJson does not like empty csv files.
            string files = "";
            if (compilerTimeCsvFilePath != null && new FileInfo(compilerTimeCsvFilePath).Length != 0)
            {
                files += $@"compilertime:""{compilerTimeCsvFilePath}""";
            }
            if (execTimeCsvFilePath != null && new FileInfo(execTimeCsvFilePath).Length != 0)
            {
                files += $@"exectime:""{execTimeCsvFilePath}""";
            }
            if (fileSizeCsvFilePath != null && new FileInfo(fileSizeCsvFilePath).Length != 0)
            {
                files += $@"filesize:""{fileSizeCsvFilePath}""";
            }
            string arguments = $@"
    {files}
    jobName:""RoslynPerf-{hash}-{date}""
    jobGroupName:""Roslyn-{branch}""
    jobTypeName:""official""
    buildInfoName:""{date}-{branch}-{hash}""
    configName:""Default Configuration""
    machinePoolName:""4-core-windows""
    architectureName:""{architecture}""
    manufacturerName:""unknown-manufacturer""
    microarchName:""unknown-microarch""
    userName:""{username}""
    userAlias:""{username}""
    osInfoName:""Windows""
    machineName:""{machineName}""
    buildNumber:""{date}-{hash}""
    /json:""{outJson}""
    ";

            arguments = arguments.Replace("\r\n", " ").Replace("\n", "");

            ShellOutVital(Path.Combine(GetCPCDirectoryPath(), "ViBenchToJson.exe"), arguments, workingDirectory: "");

            return outJson;
        }

        public static string FirstLine(string input)
        {
            return input.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None)[0];
        }

        public static void UploadTraces(string sourceFolderPath, string destinationFolderPath, ILogger logger)
        {
            logger.Log("Uploading traces");
            if (Directory.Exists(sourceFolderPath))
            {
                var directoriesToUpload = new DirectoryInfo(sourceFolderPath).GetDirectories("DataBackup*");
                if (directoriesToUpload.Count() == 0)
                {
                    logger.Log($"There are no trace directory starting with DataBackup in {sourceFolderPath}");
                    return;
                }

                var perfResultDestinationFolderName = string.Format("PerfResults-{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now);

                var destination = Path.Combine(destinationFolderPath, perfResultDestinationFolderName);
                foreach (var directoryToUpload in directoriesToUpload)
                {
                    var destinationDataBackupDirectory = Path.Combine(destination, directoryToUpload.Name);
                    if (Directory.Exists(destinationDataBackupDirectory))
                    {
                        Directory.CreateDirectory(destinationDataBackupDirectory);
                    }

                    CopyDirectory(directoryToUpload.FullName, logger, destinationDataBackupDirectory);
                }

                foreach (var file in new DirectoryInfo(sourceFolderPath).GetFiles().Where(f => f.Name.StartsWith("ConsumptionTemp", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("Roslyn-", StringComparison.OrdinalIgnoreCase)))
                {
                    File.Copy(file.FullName, Path.Combine(destination, file.Name));
                }
            }
            else
            {
                logger.Log($"sourceFolderPath: {sourceFolderPath} does not exist");
            }
        }

        public static void CopyDirectory(string source, ILogger logger, string destination, string argument = @"/mir")
        {
            var result = ShellOut("Robocopy", $"{argument} {source} {destination}", workingDirectory: "");

            // Robocopy has a success exit code from 0 - 7
            if (result.Code > 7)
            {
                throw new IOException($"Failed to copy \"{source}\" to \"{destination}\".");
            }
        }

        class ProcessResult
        {
            public string ExecutablePath { get; set; }
            public string Args { get; set; }
            public int Code { get; set; }
            public string StdOut { get; set; }
            public string StdErr { get; set; }

            public bool Failed => Code != 0;
            public bool Succeeded => !Failed;
        }

        static ProcessResult ShellOut(
                string file,
                string args,
                string workingDirectory,
                CancellationToken? cancelationToken = null)
        {
            var tcs = new TaskCompletionSource<ProcessResult>();
            var startInfo = new ProcessStartInfo(file, args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.WorkingDirectory = workingDirectory;


            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            if (cancelationToken != null)
            {
                cancelationToken.Value.Register(() => process.Kill());
            }

            Log($"running \"{file}\" with arguments \"{args}\" from directory {workingDirectory}");

            process.Start();

            var output = new StringWriter();
            var error = new StringWriter();

            process.OutputDataReceived += (s, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    output.WriteLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    error.WriteLine(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new ProcessResult
            {
                ExecutablePath = file,
                Args = args,
                Code = process.ExitCode,
                StdOut = output.ToString(),
                StdErr = error.ToString(),
            };
        }

        public static string StdoutFrom(string program, string args = "", string workingDirectory = "")
        {
            var result = ShellOut(program, args, workingDirectory);
            if (result.Failed)
            {
                LogProcessResult(result);
                throw new Exception("Shelling out failed");
            }
            return result.StdOut.Trim();
        }
    }
}
