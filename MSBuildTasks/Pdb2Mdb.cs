using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuildTasks
{
    public class PdbToMdb : Task
    {

        [Required]
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public ITaskItem[] Binaries { get; set; }

        public override bool Execute()
        {
            //var readerProvider = new PdbReaderProvider();
            //var writerProvider = new MdbWriterProvider();

            foreach (ITaskItem dll in Binaries)
            {
                // ItemSpec holds the filename or path of an Item
                if (dll.ItemSpec.Length > 0)
                {
                    if (!File.Exists(dll.ItemSpec))
                    {
                        Log.LogMessage(MessageImportance.Normal, "No file at " + dll.ItemSpec);
                        continue;
                    }

                    if (Path.GetExtension(dll.ItemSpec) != ".dll" && Path.GetExtension(dll.ItemSpec) != ".pdb")
                    {
                        Log.LogMessage(MessageImportance.Normal, dll.ItemSpec + " not a DLL or PDB");
                        continue;
                    }

                    try
                    {
                        /*Log.LogMessage(MessageImportance.Normal, "Processing PDB for " + dll.ItemSpec);
                        var path = Path.ChangeExtension(dll.ItemSpec, ".dll");
                        var module = ModuleDefinition.ReadModule(path);
                        var reader = readerProvider.GetSymbolReader(module, path);
                        var writer = writerProvider.GetSymbolWriter(module, path);

                        foreach (var type in module.Types)
                        foreach (var method in type.Methods)
                        {
                            var read = reader.Read(method);
                            if (read == null) Log.LogWarning($"Method {module.FileName} -> {method.FullName} read from PDB as null");
                            else writer.Write(read);
                        }

                        writer.Dispose();
                        reader.Dispose();
                        module.Dispose();*/
                        var path = Path.ChangeExtension(dll.ItemSpec, ".dll");
                        Log.LogMessage(MessageImportance.Normal, "Processing PDB for " + path);

                        /*Process.Start(new ProcessStartInfo
                        {
                            WorkingDirectory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException(),
                            FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) ?? throw new InvalidOperationException(), "pdb2mdb.exe"),
                            Arguments = Path.GetFileName(path)
                        });*/
                        
                        //Pdb2Mdb.Converter.Convert(path);
                    }
                    catch (Exception e)
                    {
                        Log.LogErrorFromException(e);
                        Log.LogError(e.ToString());
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}