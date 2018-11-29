using System;
using System.Collections.Generic;
using System.Text;

namespace NetDotEnv
{
    public class DotEnvOptions
    {
        public bool IsOverLoad { get; set; }
        public bool IgnoringNonexistentFile { get; set; }
        public bool IgnoringInvalidLine { get; set; }
    }
}
