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
            foreach (var (key, value) in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                Assert.Equal(value, envValue);
            }
            
        }
        [Fact]
        public void Test_Load_EqualsEnv()
        {
            const string envFileName = "fixtures/equals.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "postgres://localhost:5432/database?sslmode=disable"}
            };
            DotEnv.Load(new []{envFileName});
            foreach (var (key, value) in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                Assert.Equal(value, envValue);
            }
            
        }
        
        [Fact]
        public void Test_Load_PlainEnv()
        {
            const string envFileName = "fixtures/plain.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "1"},
                {"OPTION_B", "2"},
                {"OPTION_C", "3"},
                {"OPTION_D", "4"},
                {"OPTION_E", "5"},
            };
            DotEnv.Load(new []{envFileName});
            foreach (var (key, value) in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                Assert.Equal(value, envValue);
            }
            
        }
        [Fact]
        public void Test_Load_Quoted()
        {
            const string envFileName = "fixtures/quoted.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "1"},
                {"OPTION_B", "2"},
                {"OPTION_C", null},
                {"OPTION_D", "\\n"},
                {"OPTION_E", "1"},
                {"OPTION_F", "2"},
                {"OPTION_G", null},
                {"OPTION_H", "\n"},
                {"OPTION_I", "echo 'asd'"},
            };
            DotEnv.Load(new []{envFileName});
            foreach (var (key, value) in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                Assert.Equal(value, envValue);
            }
            
        }
        
        [Fact]
        public void Test_Load_SubstitutionsEnv()
        {
            const string envFileName = "fixtures/substitutions.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"OPTION_A", "1"},
                {"OPTION_B", "1"},
                {"OPTION_C", "1"},
                {"OPTION_D", "11"},
                {"OPTION_E", null},
            };
            DotEnv.Load(new []{envFileName}, new DotEnvOptions{IsOverLoad = true});
            //issue: https://github.com/dotnet/corefx/issues/28890
            var str = Environment.ExpandEnvironmentVariables("123-%OPTION_A%%OPTION_B%-222");
            Assert.Equal("123-11-222", str);
            foreach (var (key, value) in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                Assert.Equal(value, envValue);
            }
        }
        [Fact]
        public void Test_Load_Comments()
        {
            const string envFileName = "fixtures/with-comments.env";
            var expectedValues = new Dictionary<string, string>
            {
                {"SECRET_KEY", "YOURSECRETKEYGOESHERE"},
                {"SECRET_HASH", "something-with-a-#-hash"},
            };
            DotEnv.Load(new []{envFileName});
            foreach (var (key, value) in expectedValues)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                Assert.Equal(value, envValue);
            }
            
        }
        [Fact]
        public void ExpansionOfVariableSucceeds()
        {
           
            const string envVar1 = "envVar1";
            const string expectedValue = "animal";
            try
            {
                Environment.SetEnvironmentVariable(envVar1, expectedValue);
                DotEnv.Load(options:new DotEnvOptions{IsOverLoad = true});
                var envVar1Value = Environment.GetEnvironmentVariable("envVar1");
                Assert.Equal(expectedValue, envVar1Value);
                var result = Environment.GetEnvironmentVariable("envVar2");
                Assert.Equal(expectedValue, result);
                var result2 = Environment.GetEnvironmentVariable("ccc");
                Assert.Equal("12333", result2);
            }
            finally
            {
                // Clear the variables we just set
                Environment.SetEnvironmentVariable(envVar1, null);
            }
        }
    }
}
