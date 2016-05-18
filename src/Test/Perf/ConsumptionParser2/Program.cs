using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roslyn.Test.Performance.Utilities.ConsumptionParser
{
    class Program
    {
        public static void Main()
        {
            const string prefix = "PerfResults-";
            const string share = @"\\mlangfs1\public\basoundr\PerfTraces\";

            var runs = new List<Tuple<string, ConsumptionParse, RunInfo>>();

            // Collect run information from the share
            foreach (var directory in Directory.GetDirectories(share))
            {
                var lastPath = directory.Substring(share.Length);
                if (!lastPath.StartsWith(prefix))
                {
                    continue;
                }

                var date = lastPath.Substring(prefix.Length);
                var consumptionXml = Path.Combine(directory, "ConsumptionTempResults.xml");
                var resultJson = Directory.EnumerateFiles(directory, "Roslyn*.json").First();

                var parse = ConsumptionParse.Parse(File.ReadAllText(consumptionXml));
                var runInfo = RunInfo.Parse(File.ReadAllText(resultJson));
                runs.Add(Tuple.Create(date, parse, runInfo));

            }

            // Collect all of the metric names first 
            var metrics = new HashSet<string>();
            metrics.UnionWith(
                from run in runs
                from scenario in run.Item2.Scenarios
                from metric in scenario.Counters
                select metric.Name
            );

            // Write the column titles
            Console.Write("build, username, branch, scenario");
            foreach (var metric in metrics)
            {
                Console.Write($", z_{metric}");
            }
            Console.WriteLine();

            foreach (var run in runs)
            {
                var date = run.Item1;
                var consumption = run.Item2;
                var runInfo = run.Item3;

                foreach (var scenario in consumption.Scenarios)
                {
                    if (!scenario.Name.StartsWith("hello world"))
                    {
                        //continue;
                    }

                    Console.Write($"{NormalizeDate(date)}, {NormalizeUsername(runInfo.Username)}, {NormalizeBranch(runInfo.Branch)}, {NormalizeScenario(scenario.Name)}");
                    foreach (var metric in metrics)
                    {
                        var m = scenario[metric];
                        if (m != null) {
                            var mm = m.Value;
                            Console.Write($", {mm.Value} {mm.Units}");
                        } else
                        {
                            Console.Write(",");
                        }
                    }
                    Console.WriteLine();
                }
            }
        }

        static string RemovePrefix(string target, string prefix)
        {
            if (target.StartsWith(prefix))
            {
                target = target.Substring(prefix.Length);
            }
            return target;
        }

        static string NormalizeBranch(string s)
        {
            s = RemovePrefix(s, "Roslyn-");
            if (s == "HEAD")
            {
                s = "master";
            }
            return s;
        }

        static string NormalizeUsername(string s)
        {
            return RemovePrefix(s, @"redmond\");
        }

        static string NormalizeScenario(string s)
        {
            while (s.Length != 0 && char.IsNumber(s.Last()))
            {
                s = s.Substring(0, s.Length - 1);
            }
            return s;
        }

        static string NormalizeDate(string s)
        {
            var dateTimeSplit = s.Split('_');
            var date = dateTimeSplit[0];
            var time = dateTimeSplit[1];

            var hourMinuteSecondSplit = time.Split('-');
            var hour = hourMinuteSecondSplit[0];
            var min = hourMinuteSecondSplit[1];
            var sec = hourMinuteSecondSplit[2];

            return $"{date} {hour}:{min}:{sec}";
        }
    }
}
