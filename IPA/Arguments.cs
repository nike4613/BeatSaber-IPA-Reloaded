using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IPA.ArgParsing
{
    public class Arguments
    {
        public static Arguments CmdLine = new Arguments(Environment.GetCommandLineArgs());

        private List<string> positional = new List<string>();
        private Dictionary<string, string> longFlags = new Dictionary<string, string>();
        private Dictionary<char, string> flags = new Dictionary<char, string>();
        private List<ArgumentFlag> flagObjects = new List<ArgumentFlag>();

        private string[] toParse = null;

        private Arguments(string[] args)
        {
            toParse = args.Skip(1).ToArray();
        }

        public Arguments Flags(params ArgumentFlag[] toAdd)
        {
            foreach (var f in toAdd) AddFlag(f);
            return this;
        }

        public void AddFlag(ArgumentFlag toAdd)
        {
            if (toParse == null) throw new InvalidOperationException();
            flagObjects.Add(toAdd);
        }

        public void Process()
        {
            foreach (var arg in toParse)
            {
                if (arg.StartsWith("--"))
                { // parse as a long flag
                    var name = arg.Substring(2); // cut off first two chars
                    string value = null;

                    if (name.Contains('='))
                    {
                        var spl = name.Split('=');
                        name = spl[0];
                        value = string.Join("=", spl, 1, spl.Length - 1);
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

            toParse = null;

            foreach (var flag in flagObjects)
            {
                foreach (var chflag in flag.shortFlags)
                {
                    if (flag.exists = HasFlag(chflag))
                    {
                        flag.value = GetFlagValue(chflag);
                        goto FoundValue; // continue to next flagObjects item
                    }
                }

                foreach (var lflag in flag.longFlags)
                {
                    if (flag.exists = HasLongFlag(lflag))
                    {
                        flag.value = GetLongFlagValue(lflag);
                        goto FoundValue; // continue to next flagObjects item
                    }
                }

                FoundValue:;
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

        public void PrintHelp()
        {
            const string indent = "    ";
            string filename = Environment.GetCommandLineArgs()[0];
            string format = @"usage:
{2}{0} [FLAGS] [ARGUMENTS]

flags:
{1}";
            var flagsBuilder = new StringBuilder();
            foreach (var flag in flagObjects)
            {
                flagsBuilder.AppendFormat("{2}{0}{3}{1}", 
                    string.Join(", ", flag.shortFlags.Select(s => $"-{s}").Concat( flag.longFlags.Select(s => $"--{s}")) ), 
                    Environment.NewLine, indent, flag.ValueString != null ? "=" + flag.ValueString : "");
                flagsBuilder.AppendFormat("{2}{2}{0}{1}", flag.DocString, Environment.NewLine, indent);
            }

            Console.Write(string.Format(format, filename, flagsBuilder.ToString(), indent));
        }

        public IReadOnlyList<string> PositionalArgs => positional;
    }

    public class ArgumentFlag
    {
        internal List<char> shortFlags = new List<char>();
        internal List<string> longFlags = new List<string>();

        internal string value = null;
        internal bool exists = false;

        public ArgumentFlag(params string[] flags)
        {
            foreach (var part in flags)
                AddPart(part);
        }

        private void AddPart(string flagPart)
        {
            if (flagPart.StartsWith("--"))
                longFlags.Add(flagPart.Substring(2));
            else if (flagPart.StartsWith("-"))
                shortFlags.Add(flagPart[1]);
        }

        public bool Exists => exists;
        public string Value => value;

        public bool HasValue => Exists && Value != null;

        public string DocString { get; set; } = "";
        public string ValueString { get; set; } = null;

        public static implicit operator bool(ArgumentFlag f)
        {
            return f.Exists;
        }
    }
}
