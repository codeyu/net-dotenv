using System;
using System.Collections.Generic;
using Xunit;

namespace NetDotEnv.Test
{
    public class DotEnvTest
    {
        [Fact]
        public void Test_Load_DefaultDotEnv()
        {
            var expectedValues = new Dictionary<string, string>
            {
                {"foo", "test"},
                {"FOO", "foo${foo}"},
                {"FOO2", "test"},
                {"BAR", "foo${FOO2} test"},
            };
            DotEnv.Load();
            foreach (var kv in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(kv.Key);
                Assert.Equal(kv.Value, envValue);
            }
            
        }
        [Fact]
        public void Test_Load_EqualsEnv()
        {
            var envFileName = "fixtures/equals.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "postgres://localhost:5432/database?sslmode=disable"}
            };
            DotEnv.Load(new []{envFileName});
            foreach (var kv in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(kv.Key);
                Assert.Equal(kv.Value, envValue);
            }
            
        }
        [Fact]
        public void Test_Load_SubstitutionsEnv()
        {
            var envFileName = "fixtures/substitutions.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "1"},
                {"OPTION_B", "1"},
                {"OPTION_C", "1"},
                {"OPTION_D", "11"},
                {"OPTION_E", null},
            };
            DotEnv.Load(new []{envFileName});
            //issue: https://github.com/dotnet/corefx/issues/28890
            var str = Environment.ExpandEnvironmentVariables("123-%OPTION_A%%OPTION_B%-222");
            Assert.Equal("123-11-222", str);
            foreach (var kv in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(kv.Key);
                Assert.Equal(kv.Value, envValue);
            }
            
        }
        [Fact]
        public void Test_Load_PlainEnv()
        {
            var envFileName = "fixtures/plain.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "1"},
                {"OPTION_B", "2"},
                {"OPTION_C", "3"},
                {"OPTION_D", "4"},
                {"OPTION_E", "5"},
            };
            DotEnv.Load(new []{envFileName});
            foreach (var kv in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(kv.Key);
                Assert.Equal(kv.Value, envValue);
            }
            
        }
        [Fact]
        public void ExpansionOfVariableSucceeds()
        {
           
            string envVar1 = "envVar1";
            string expectedValue = "animal";

            try
            {
                Environment.SetEnvironmentVariable(envVar1, expectedValue);
                DotEnv.Load();
                string result = Environment.GetEnvironmentVariable("envVar2");
                var result2 = Environment.GetEnvironmentVariable("ccc");
                Assert.Equal(expectedValue, result);
                Assert.Equal("12\\3", result2);
            }
            finally
            {
                // Clear the variables we just set
                Environment.SetEnvironmentVariable(envVar1, null);
            }
        }
    }
}
