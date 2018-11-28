using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
namespace NetDotEnv
{
    public class DotEnv
    {
        private const string DoubleQuoteSpecialChars = "\\\n\r\"!$`";
        private const string DefaultFileName = ".env";

        public static void Load(string[] fileNames = null, bool isOverLoad = false, bool ignoringNonexistentFile = true)
        {
            LoadFile(fileNames, isOverLoad, ignoringNonexistentFile);
        }

        private static void LoadFile(string[] fileNames, bool isOverLoad, bool ignoringNonexistentFile)
        {
            var arrFileNames = fileNames == null || fileNames.Length == 0 ? new []{DefaultFileName} : fileNames;

            foreach (var fileName in arrFileNames)
            {
                SetEnv(fileName, isOverLoad, ignoringNonexistentFile);
            }
        }
        private static void SetEnv(string fileName, bool isOverLoad, bool ignoringNonexistentFile)
        {
            if (!File.Exists(fileName))
            {
                if (!ignoringNonexistentFile)
                {
                    throw new FileNotFoundException($"An environment file with path \"{fileName}\" does not exist.");
                }
                return;
            }
            var envMap = ReadDotEnvFile(fileName);
            var rawEnv = Environment.GetEnvironmentVariables();
            var currentEnv = rawEnv.Keys.Cast<object>().ToDictionary(rawEnvKey => rawEnvKey.ToString(), rawEnvKey => true);

            foreach (var kv in envMap)
            {
                if (!currentEnv.ContainsKey(kv.Key) || isOverLoad)
                {
                    Environment.SetEnvironmentVariable(kv.Key,kv.Value);
                }
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

            foreach (var line in lines)
            {
                if (IsIgnoredLine(line)) continue;
                var kv = ParseLine(line, dicEnv);
                dicEnv.Add(kv.key, kv.value);

            }

            return dicEnv;
        }
        private static (string key, string value) ParseLine(string line, Dictionary<string, string> envMap)
        {
            if (line.Length == 0)
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

            if(splitString.Length != 2)
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
        private static string ParseValue(string val, Dictionary<string, string> envMap)
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
                // unescape characters
                var e = new Regex(@"\\([^$])", RegexOptions.Compiled);
                val = e.Replace(val, "$1");
            }

            if (singleQuotes.Count == 0)
            {
                val = ExpandVariables(val, envMap);
            }

            return val;
        }

        private static string ExpandVariables(string val, IReadOnlyDictionary<string, string> m)
        {
            var rx = new Regex(@"(\\)?(\$)(\()?\{?([A-Z0-9_]+)?\}?", RegexOptions.Compiled);
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

                if (subMatch[4].Value != "")
                {
                    return m.ContainsKey(subMatch[4].Value) ? m[subMatch[4].Value] : string.Empty;
                }

                return match.Value;
            });

        }
        private static bool IsIgnoredLine(string line)
        {
            var trimmedLine = line.Trim(" \n\t".ToCharArray());
	        return trimmedLine.Length == 0 || trimmedLine.StartsWith("#");
        }

        private string DoubleQuoteEscape(string line)
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