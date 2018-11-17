using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.IO;

namespace MSBuildTasks
{
    public class AssemblyRename : Task
    {

        [Required]
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public ITaskItem[] Assemblies { get; set; }

        public override bool Execute()
        {
            foreach (ITaskItem assembly in Assemblies)
            {
                // ItemSpec holds the filename or path of an Item
                if (assembly.ItemSpec.Length > 0)
                {
                    if (!File.Exists(assembly.ItemSpec))
                    {
                        Log.LogMessage(MessageImportance.Normal, "No file at " + assembly.ItemSpec);
                        continue;
                    }

                    if (Path.GetExtension(assembly.ItemSpec) != ".dll")
                    {
                        Log.LogMessage(MessageImportance.Normal, assembly.ItemSpec + " not a DLL");
                        continue;
                    }

                    try
                    {
                        Log.LogMessage(MessageImportance.Normal, "Reading " + assembly.ItemSpec);
                        var module = ModuleDefinition.ReadModule(assembly.ItemSpec);
                        var asmName = module.Assembly.Name;
                        var name = asmName.Name;
                        var version = asmName.Version;
                        var newFilen = $"{name}.{version}.dll";
                        var newFilePath = Path.Combine(Path.GetDirectoryName(assembly.ItemSpec) ?? throw new InvalidOperationException(), newFilen);

                        module.Dispose();

                        Log.LogMessage(MessageImportance.Normal, $"Old file: {assembly.ItemSpec}, new file: {newFilePath}");

                        if (File.Exists(newFilePath))
                            File.Delete(newFilePath);

                        Log.LogMessage(MessageImportance.Normal, "Moving");
                        try
                        {
                            File.Move(assembly.ItemSpec, newFilePath);
                        }
                        catch (Exception)
                        {
                            File.Copy(assembly.ItemSpec, newFilePath);
                            File.Delete(assembly.ItemSpec);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogErrorFromException(e);
                    }
                }
            }
            
            return !Log.HasLoggedErrors;
        }
    }
}
