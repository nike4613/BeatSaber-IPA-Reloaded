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
            string depsfile = File.ReadAllText(args[0]);
            string fdir = Path.GetDirectoryName(args[0]);
            
            List<string> files = new List<string>();
            { // Create files from stuff in depsfile
                Stack<string> fstack = new Stack<string>();

                void Push(string val)
                {
                    string pre = "";
                    if (fstack.Count > 0)
                        pre = fstack.First();
                    fstack.Push(pre + val);
                }
                string Pop() => fstack.Pop();
                string Replace(string val)
                {
                    var v2 = Pop();
                    Push(val);
                    return v2;
                }

                foreach (var line in depsfile.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
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
                            path = File.ReadAllText(Path.Combine(fdir ?? throw new InvalidOperationException(), arglist));
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

                    if (level > fstack.Count - 1)
                        Push(path);
                    else if (level == fstack.Count - 1)
                        files.Add(Replace(path));
                    else if (level < fstack.Count - 1)
                    {
                        files.Add(Pop());
                        while (level < fstack.Count)
                            Pop();
                        Push(path);
                    }
                }

                files.Add(Pop());
            }

            foreach (var file in files)
            {
                var fparts = file.Split('?');
                var fname = fparts[0];

                if (fname == "") continue;

                var outp = Path.Combine(fdir ?? throw new InvalidOperationException(), Path.GetFileName(fname) ?? throw new InvalidOperationException());
                Console.WriteLine($"Copying \"{fname}\" to \"{outp}\"");
                if (File.Exists(outp)) File.Delete(outp);

                if (Path.GetExtension(fname)?.ToLower() == ".dll")
                {
                    // ReSharper disable once StringLiteralTypo
                    if (fparts.Length > 1 && fparts[1] == "virt")
                    {
                        var module = VirtualizedModule.Load(fname);
                        module.Virtualize(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Path.GetFileName(fname) ?? throw new InvalidOperationException()));
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

        }
    }
}
