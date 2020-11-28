using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.InteropServices;

namespace NetDotEnv
{
    /// <summary>
    /// 
    /// </summary>
    public static class DotEnv
    {
        private const string DoubleQuoteSpecialChars = "\\\n\r\"!$`";
        private const string DefaultFileName = ".env";
        private static DotEnvOptions Options { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNames"></param>
        /// <param name="options"></param>
        public static void Load(string[] fileNames =null, DotEnvOptions options = null)
        {
            if (options == null)
            {
                options = new DotEnvOptions
                {
                    IgnoringInvalidLine = true,
                    IgnoringNonexistentFile = true,
                    IsOverLoad = false
                };
            }
            Options = options;
            LoadFile(fileNames);
        }
        public static Dictionary<string, string> UnMarshal(string str) 
        {
            return Parse(new StringReader(str));
        }
        public static string Marshal(Dictionary<string, string> envMap) 
         {
            var lines = envMap.Select(kv => $"{kv.Key}={DoubleQuoteEscape(kv.Value)}").ToList();
            lines.Sort();
            return string.Join(Environment.NewLine, lines);
        }

        private static void LoadFile(string[] fileNames)
        {
            var arrFileNames = fileNames == null || fileNames.Length == 0 ? new []{DefaultFileName} : fileNames;

            foreach (var fileName in arrFileNames)
            {
                SetEnv(fileName);
            }
        }
        private static void SetEnv(string fileName)
        {
            if (!File.Exists(fileName))
            {
                if (!Options.IgnoringNonexistentFile)
                {
                    throw new FileNotFoundException($"An environment file with path \"{fileName}\" does not exist.");
                }
                return;
            }
            var envMap = ReadDotEnvFile(fileName);
            var rawEnv = Environment.GetEnvironmentVariables();
            var currentEnv = rawEnv.Keys.Cast<object>().ToDictionary(rawEnvKey => rawEnvKey.ToString(), rawEnvKey => true);

            foreach (var kv in envMap.Where(kv => !currentEnv.ContainsKey(kv.Key) || Options.IsOverLoad))
            {
                Environment.SetEnvironmentVariable(kv.Key,kv.Value);
            }
        }

        private static Dictionary<string, string> ReadDotEnvFile(string fileName)
        {
            //var fileContent = File.ReadAllText(fileName, Encoding.Default);
            using (var sr = new StreamReader(fileName))
            {
                return Parse(sr);
            }
        }

        private static Dictionary<string, string> Parse(TextReader sr)
        {
            var dicEnv = new Dictionary<string,string>();
            var lines = new List<string>();
            using(sr)
            {
                while (sr.Peek() >= 0) 
                {
                    lines.Add(sr.ReadLine());
                }
            }
            foreach (var (key, value) in from line in lines where !IsIgnoredLine(line) select ParseLine(line, dicEnv))
            {
                dicEnv.Add(key, value);
            }
            return dicEnv;
        }
        private static (string key, string value) ParseLine(string line, IReadOnlyDictionary<string, string> envMap)
        {
            if (line.Length == 0 && !Options.IgnoringInvalidLine)
            {
                throw new IndexOutOfRangeException("zero length string");
            }
            // ditch the comments (but keep quoted hashes)
            if (line.Contains("#"))
            {
                var segmentsBetweenHashes = line.Split('#');
                var quotesAreOpen = false;
                var segmentsToKeep = new List<string>();
                foreach (var segment in segmentsBetweenHashes) {
                    if (segment.Count(x=>x=='"')  == 1 || segment.Count(x=>x=='\'') == 1) {
                        if (quotesAreOpen)
                        {
                            quotesAreOpen = false;
                            segmentsToKeep.Add(segment);
                        } else
                        {
                            quotesAreOpen = true;
                        }
                    }
                    if (segmentsToKeep.Count == 0 || quotesAreOpen) {
                        segmentsToKeep.Add(segment);
                    }
                }
                line = string.Join("#", segmentsToKeep);
            }
            var firstEquals = line.IndexOf("=", StringComparison.Ordinal);
            var firstColon = line.IndexOf(":", StringComparison.Ordinal);
            var splitString = line.Split(new []{'='}, 2);
            if (firstColon != -1 && (firstColon < firstEquals || firstEquals == -1)) {
                //this is a yaml-style line
                splitString = line.Split(new[] {':'}, 2);
            }
            if(splitString.Length != 2 && !Options.IgnoringInvalidLine)
            {
                throw new Exception("Can't separate key from value");
            }
            // Parse the key
            var key = splitString[0];
            if (key.StartsWith("export"))
            {
                key = key.TrimStart("export".ToCharArray());
            }
            key = key.Trim();
            // Parse the value
            var value = ParseValue(splitString[1], envMap);
            return (key,value);

        }
        private static string ParseValue(string val, IReadOnlyDictionary<string, string> envMap)
        {
            val = val.Trim();
            if (val.Length <= 1) return val;
            var rs = new Regex(@"\A'(.*)'\z", RegexOptions.Compiled);
            var singleQuotes = rs.Matches(val);
            var rd = new Regex(@"\A""(.*)""\z", RegexOptions.Compiled);
            var doubleQuotes = rd.Matches(val);
            if (singleQuotes.Count > 0 || doubleQuotes.Count > 0) {
                // pull the quotes off the edges
                val = val.Substring(1, val.Length - 1 - 1);
            }
            if (doubleQuotes.Count > 0) {
                // expand newlines
                var escapeRegex = new Regex(@"\\.", RegexOptions.Compiled);
                val = escapeRegex.Replace(val, match =>
                {
                    var c = match.Value.TrimStart(@"\".ToCharArray());
                    switch (c)
                    {
                        case "n":
                            return "\n";
                        case "r":
                            return "\r";
                        default:
                            return match.Value;
                    }
                });
                var e = new Regex(@"\\([^$])", RegexOptions.Compiled);
                val = e.Replace(val, "$1");
            }
            if (singleQuotes.Count == 0)
            {
                val = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ExpandVariablesWin(val, envMap): ExpandVariablesUnix(val, envMap);
            }
            return val;
        }
        private static string ExpandVariablesWin(string val, IReadOnlyDictionary<string, string> m)
        {
            var result = new StringBuilder();
            int lastPos = 0, pos;
            while (lastPos < val.Length && (pos = val.IndexOf('%', lastPos + 1)) >= 0)
            {
                if (val[lastPos] == '%')
                {
                    var key = val.Substring(lastPos + 1, pos - lastPos - 1);
                    var value = GetExpandEnv(key, m);
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Append(value);
                        lastPos = pos + 1;
                        continue;
                    }
                }
                result.Append(val.Substring(lastPos, pos - lastPos));
                lastPos = pos;
            }
            result.Append(val.Substring(lastPos));
            return result.ToString();
        }
        private static string ExpandVariablesUnix(string val, IReadOnlyDictionary<string, string> m)
        {
            var rx = new Regex(@"(\\)?(\$)(\()?\{?([A-Z0-9_]+)?\}?", RegexOptions.IgnoreCase);
            return rx.Replace(val, match =>
            {
                var subMatch = match.Groups;
                if (subMatch.Count == 0)
                {
                    return match.Value;
                }
                if (subMatch[1].Value == "\\" || subMatch[2].Value == "(")
                {
                    return subMatch[0].Value.Substring(1);
                }
                return subMatch[4].Value == "" ? match.Value : GetExpandEnv(subMatch[4].Value, m);
            });
        }

        private static string GetExpandEnv(string name, IReadOnlyDictionary<string, string> m)
        {
            var variable = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(variable))
            {
                return m.ContainsKey(name) ? m[name] : string.Empty;
            }
            if (!Options.IsOverLoad)
            {
                return variable;
            }
            return m.ContainsKey(name) ? m[name] : variable;
        }
        private static bool IsIgnoredLine(string line)
        {
            var trimmedLine = line.Trim(" \n\t".ToCharArray());
	        return trimmedLine.Length == 0 || trimmedLine.StartsWith("#");
        }

        private static string DoubleQuoteEscape(string line)
        {
            foreach (var c in DoubleQuoteSpecialChars)
            {
                var toReplace = "\\" + c;
                switch (c)
                {
                    case '\n':
                        toReplace = "\n";
                        break;
                    case '\r':
                        toReplace = "\r";
                        break;
                }
                line = line.Replace(c.ToString(), toReplace);
            }
            return line;
        }
    }
}