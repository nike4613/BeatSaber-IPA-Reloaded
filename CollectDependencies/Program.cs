using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollectDependencies
{
    static class Program
    {
        static void Main(string[] args)
        {
            var depsFile = File.ReadAllText(args[0]);
            var directoryName = Path.GetDirectoryName(args[0]);

            var files = new List<Tuple<string, int>>();
            { // Create files from stuff in depsfile
                var stack = new Stack<string>();

                void Push(string val)
                {
                    string pre = "";
                    if (stack.Count > 0)
                        pre = stack.First();
                    stack.Push(pre + val);
                }
                string Pop() => stack.Pop();
                string Replace(string val)
                {
                    var v2 = Pop();
                    Push(val);
                    return v2;
                }

                var lineNo = 0;
                foreach (var line in depsFile.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                {
                    var parts = line.Split('"');
                    var path = parts.Last();
                    var level = parts.Length - 1;

                    if (path.StartsWith("::"))
                    { // pseudo-command
                        parts = path.Split(' ');
                        var command = parts[0].Substring(2);
                        parts = parts.Skip(1).ToArray();
                        var arglist = string.Join(" ", parts);
                        if (command == "from")
                        { // an "import" type command
                            path = File.ReadAllText(Path.Combine(directoryName ?? throw new InvalidOperationException(), arglist));
                        }
                        else if (command == "prompt")
                        {
                            Console.Write(arglist);
                            path = Console.ReadLine();
                        }
                        else
                        {
                            path = "";
                            Console.Error.WriteLine($"Invalid command {command}");
                        }
                    }

                    if (level > stack.Count - 1)
                        Push(path);
                    else if (level == stack.Count - 1)
                        files.Add(new Tuple<string, int>(Replace(path), lineNo));
                    else if (level < stack.Count - 1)
                    {
                        files.Add(new Tuple<string, int>(Pop(), lineNo));
                        while (level < stack.Count)
                            Pop();
                        Push(path);
                    }

                    lineNo++;
                }

                files.Add(new Tuple<string, int>(Pop(), lineNo));
            }

            foreach (var file in files)
            {
                try
                {
                    var fparts = file.Item1.Split('?');
                    var fname = fparts[0];

                    if (fname == "") continue;

                    var outp = Path.Combine(directoryName ?? throw new InvalidOperationException(),
                        Path.GetFileName(fname) ?? throw new InvalidOperationException());
                    Console.WriteLine($"Copying \"{fname}\" to \"{outp}\"");
                    if (File.Exists(outp)) File.Delete(outp);

                    if (Path.GetExtension(fname)?.ToLower() == ".dll")
                    {
                        // ReSharper disable once StringLiteralTypo
                        if (fparts.Length > 1 && fparts[1] == "virt")
                        {
                            var module = VirtualizedModule.Load(fname);
                            module.Virtualize(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(),
                                Path.GetFileName(fname) ?? throw new InvalidOperationException()));
                        }

                        var modl = ModuleDefinition.ReadModule(fparts[0]);
                        foreach (var t in modl.Types)
                        {
                            foreach (var m in t.Methods)
                            {
                                if (m.Body != null)
                                {
                                    m.Body.Instructions.Clear();
                                    m.Body.InitLocals = false;
                                    m.Body.Variables.Clear();
                                }
                            }
                        }

                        modl.Write(outp);
                    }
                    else
                    {
                        File.Copy(fname, outp);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{args[0]}({file.Item2}): error: {e}");
                }
            }

        }
    }
}
