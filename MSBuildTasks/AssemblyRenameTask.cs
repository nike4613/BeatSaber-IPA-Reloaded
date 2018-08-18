using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;

namespace MSBuildTasks
{
    public class AssemblyRename : Task
    {
        private ITaskItem[] assemblies;

        [Required]
        public ITaskItem[] Assemblies
        {
            get => assemblies;
            set => assemblies = value;
        }

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
                        var newFilePath = Path.Combine(Path.GetDirectoryName(assembly.ItemSpec), newFilen);

                        Log.LogMessage(MessageImportance.Normal, $"Old file: {assembly.ItemSpec}, new file: {newFilePath}");

                        if (File.Exists(newFilePath))
                            File.Delete(newFilePath);

                        File.Move(assembly.ItemSpec, newFilePath);
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
