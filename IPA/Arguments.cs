using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA
{
    public class Arguments
    {
        public static Arguments Process = new Arguments(Environment.GetCommandLineArgs());

        private List<string> positional = new List<string>();
        private Dictionary<string, string> longFlags = new Dictionary<string, string>();
        private Dictionary<char, string> flags = new Dictionary<char, string>();

        private Arguments(string[] args)
        {
            foreach(var arg in args)
            {
                if (arg.StartsWith("--"))
                { // parse as a long flag
                    var name = arg.Substring(2); // cut off first two chars
                    string value = null;

                    if (name.Contains('='))
                    {
                        var spl = name.Split('=');
                        name = spl[0];
                        value = string.Join("=", spl, 1, spl.Length-1);
                    }

                    longFlags.Add(name, value);
                }
                else if (arg.StartsWith("-"))
                { // parse as flags
                    var argument = arg.Substring(1); // cut off first char

                    StringBuilder subBuildState = new StringBuilder();
                    bool parsingValue = false;
                    char mainChar = ' ';
                    foreach (char chr in argument)
                    {
                        if (!parsingValue)
                        {
                            if (chr == '=')
                            {
                                parsingValue = true;
                            }
                            else
                            {
                                mainChar = chr;
                                flags.Add(chr, null);
                            }
                        }
                        else
                        {
                            if (chr == ',')
                            {
                                parsingValue = false;
                                flags[mainChar] = subBuildState.ToString();
                                subBuildState = new StringBuilder();
                            }
                            else
                            {
                                subBuildState.Append(chr);
                            }
                        }
                    }
                }
                else
                { // parse as positional
                    positional.Add(arg);
                }
            }
        }

        public bool HasLongFlag(string flag)
        {
            return longFlags.ContainsKey(flag);
        }

        public bool HasFlag(char flag)
        {
            return flags.ContainsKey(flag);
        }

        public string GetLongFlagValue(string flag)
        {
            return longFlags[flag];
        }

        public string GetFlagValue(char flag)
        {
            return flags[flag];
        }

        public IReadOnlyList<string> PositionalArgs => positional;
    }
}
