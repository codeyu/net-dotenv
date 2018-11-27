using System;
using System.Collections.Generic;
using System.IO;
namespace NetDotEnv
{
    public class DotEnv
    {
        const string doubleQuoteSpecialChars = "\\\n\r\"!$`";
        public void Load(params string[] fileNames = ".env")
        {

        }

        private bool SetEnv(string fileName, bool isOverLoad = false)
        {

        }

        private Dictionary<string, string> ReadDotEnvFile(string fileName)
        {
            var fileContent = File.ReadAllText(fileName, Encoding.Default);
        }
        public Dictionary<string, string> Parse(StreamReader sr)
        {
            List<string> lines = new List<string>();
            using(sr)
            {
                while (sr.Peek() >= 0) 
                {
                    lines.Add(sr.ReadLine());
                }
            }
        }
        private (string key, string value) ParseLine(string line, Dictionary<string, string> dic)
        {

        }
        private string ParseValue(string value, Dictionary<string, string> dic)
        {

        }
        private bool IsIgnoredLine(string line)
        {
            var trimmedLine = line.Trim(" \n\t");
	        return trimmedLine.Length == 0 || trimmedLine.HasPrefix("#");
        }
    }
}