using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Roslyn.Test.Performance.Utilities.ConsumptionParser
{

    public struct ConsumptionParse
    {
        public IEnumerable<ScenarioResult> Scenarios { get; }
        public ConsumptionParse(IEnumerable<ScenarioResult> scenarios)
        {
            this.Scenarios = scenarios;
        }

        public static ConsumptionParse Parse(String s)
        {
            var document = XDocument.Parse(s);

            var scenariosOut = new List<ScenarioResult>();
            foreach (var scenario in document.Descendants("ScenarioResult"))
            {
                var countersOut = new List<CounterResult>();
                var name = scenario.Attribute("Name").Value;
                if (name == "..TestDiagnostics..")
                {
                    continue;
                }

                foreach (var counter in scenario.Descendants("CounterResult"))
                {
                    countersOut.Add(new CounterResult(
                        name: counter.Attribute("Name").Value,
                        units: counter.Attribute("Units").Value,
                        isDefault: bool.Parse(counter.Attribute("Default").Value),
                        top: bool.Parse(counter.Attribute("Top").Value),
                        iteration: int.Parse(counter.Attribute("Iteration").Value),
                        value: counter.Value
                    ));
                }
                scenariosOut.Add(new ScenarioResult(name, countersOut));
            }

            return new ConsumptionParse(scenariosOut);
        }

    }

    public struct ScenarioResult
    {
        public ScenarioResult(string name, IEnumerable<CounterResult> counters)
        {
            this.Name = name;
            this.Counters = counters;
        }

        public CounterResult? this[string name]
        {
            get
            {
                foreach (var counter in Counters)
                {
                    if (counter.Name == name)
                        return counter;
                }
                return null;
            }

        }

        public string Name { get; }
        public IEnumerable<CounterResult> Counters { get; }

    }

    public struct CounterResult
    {
        public CounterResult(string name, string units, bool isDefault, bool top, int iteration, string value)
        {
            this.Name = name;
            this.Units = units;
            this.Default = isDefault;
            this.Top = top;
            this.Iteration = iteration;
            this.Value = value;
        }

        public string Name { get; }
        public string Units { get; }
        public bool Default { get; }
        public bool Top { get; }
        public int Iteration { get; }
        public string Value { get; }
    }
}
