using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IPA
{
    public class Arguments
    {
        public static readonly Arguments CmdLine = new Arguments(Environment.GetCommandLineArgs());

        private readonly List<string> positional = new List<string>();
        private readonly Dictionary<string, string> longFlags = new Dictionary<string, string>();
        private readonly Dictionary<char, string> flags = new Dictionary<char, string>();
        private readonly List<ArgumentFlag> flagObjects = new List<ArgumentFlag>();

        private string[] toParse;

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

                    var subBuildState = new StringBuilder();
                    var parsingValue = false;
                    var escaped = false;
                    var mainChar = ' ';
                    foreach (var chr in argument)
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
                            if (!escaped)
                            {
                                if (chr == ',')
                                {
                                    parsingValue = false;
                                    flags[mainChar] = subBuildState.ToString();
                                    subBuildState = new StringBuilder();
                                    continue;
                                }
                                else if (chr == '\\')
                                {
                                    escaped = true;
                                    continue;
                                }
                            }

                            subBuildState.Append(chr);
                        }
                    }

                    if (parsingValue)
                    {
                        parsingValue = false;
                        flags[mainChar] = subBuildState.ToString();
                        subBuildState = new StringBuilder();
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
                foreach (var charFlag in flag.ShortFlags)
                {
                    if (!(flag.exists_ = HasFlag(charFlag))) continue;

                    flag.value_ = GetFlagValue(charFlag);
                    goto FoundValue; // continue to next flagObjects item
                }

                foreach (var longFlag in flag.LongFlags)
                {
                    if (!(flag.exists_ = HasLongFlag(longFlag))) continue;

                    flag.value_ = GetLongFlagValue(longFlag);
                    goto FoundValue; // continue to next flagObjects item
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
            var filename = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            const string format = @"usage:
{2}{0} [FLAGS] [ARGUMENTS]

flags:
{1}";
            var flagsBuilder = new StringBuilder();
            foreach (var flag in flagObjects)
            {
                flagsBuilder.AppendFormat("{2}{0}{3}{1}", 
                    string.Join(", ", flag.ShortFlags.Select(s => $"-{s}").Concat( flag.LongFlags.Select(s => $"--{s}")) ), 
                    Environment.NewLine, indent, flag.ValueString != null ? "=" + flag.ValueString : "");
                flagsBuilder.AppendFormat("{2}{2}{0}{1}", flag.DocString, Environment.NewLine, indent);
            }

            Console.Write(format, filename, flagsBuilder, indent);
        }

        public IReadOnlyList<string> PositionalArgs => positional;
    }

    public class ArgumentFlag
    {
        internal readonly List<char> ShortFlags = new List<char>();
        internal readonly List<string> LongFlags = new List<string>();

        internal string value_;
        internal bool exists_;

        public ArgumentFlag(params string[] flags)
        {
            foreach (var part in flags)
                AddPart(part);
        }

        private void AddPart(string flagPart)
        {
            if (flagPart.StartsWith("--"))
                LongFlags.Add(flagPart.Substring(2));
            else if (flagPart.StartsWith("-"))
                ShortFlags.Add(flagPart[1]);
        }

        public bool Exists => exists_;
        public string Value => value_;

        public bool HasValue => Exists && Value != null;

        public string DocString { get; set; } = "";
        public string ValueString { get; set; }

        public static implicit operator bool(ArgumentFlag f)
        {
            return f.Exists;
        }
    }
}
