using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IPA.Patcher
{
    internal class PatchedModule
    {
        private static readonly string[] EntryTypes = { "Input", "Display" };

        private readonly FileInfo _file;
        private ModuleDefinition _module;

        internal struct PatchData {
            public bool IsPatched;
            public Version Version;
        }

        public static PatchedModule Load(string engineFile)
        {
            return new PatchedModule(engineFile);
        }

        private PatchedModule(string engineFile)
        {
            _file = new FileInfo(engineFile);

            LoadModules();
        }

        private void LoadModules()
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(_file.DirectoryName);

            var parameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
            };
            
            _module = ModuleDefinition.ReadModule(_file.FullName, parameters);
        }
        
        public PatchData Data
        {
            get
            {
                var data = new PatchData { IsPatched = false, Version = null };
                foreach (var @ref in _module.AssemblyReferences) {
                    switch (@ref.Name)
                    {
                        case "IllusionInjector":
                        case "IllusionPlugin":
                            data = new PatchData { IsPatched = true, Version = new Version(0, 0, 0, 0) };
                            break;
                        case "IPA.Injector":
                            return new PatchData { IsPatched = true, Version = @ref.Version };
                    }
                }
                return data;
            }
        }

        public void Patch(Version v)
        {
            // First, let's add the reference
            var nameReference = new AssemblyNameReference("IPA.Injector", v);
            var injectorPath = Path.Combine(_file.DirectoryName ?? throw new InvalidOperationException(), "IPA.Injector.dll");
            var injector = ModuleDefinition.ReadModule(injectorPath);

            bool hasIPAInjector = false;
            for (int i = 0; i < _module.AssemblyReferences.Count; i++)
            {
                if (_module.AssemblyReferences[i].Name == "IllusionInjector")
                    _module.AssemblyReferences.RemoveAt(i--);
                if (_module.AssemblyReferences[i].Name == "IllusionPlugin")
                    _module.AssemblyReferences.RemoveAt(i--);
                if (_module.AssemblyReferences[i].Name == "IPA.Injector")
                {
                    hasIPAInjector = true;
                    _module.AssemblyReferences[i].Version = v;
                }
            }

            if (!hasIPAInjector)
            {
                _module.AssemblyReferences.Add(nameReference);

                int patched = 0;
                foreach (var type in FindEntryTypes())
                {
                    if (PatchType(type, injector))
                    {
                        patched++;
                    }
                }

                if (patched > 0)
                {
                    _module.Write(_file.FullName);
                }
                else
                {
                    throw new Exception("Could not find any entry type!");
                }
            }
            else
            {
                _module.Write(_file.FullName);
            }
        }

        private bool PatchType(TypeDefinition targetType, ModuleDefinition injector)
        {
            var targetMethod = targetType.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
            if (targetMethod != null)
            {
                var methodReference = _module.ImportReference(injector.GetType("IPA.Injector.Injector").Methods.First(m => m.Name == "Inject"));
                targetMethod.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, methodReference));
                return true;
            }
            return false;
        }


        private IEnumerable<TypeDefinition> FindEntryTypes()
        {
            return _module.GetTypes().Where(m => EntryTypes.Contains(m.Name));
        }
    }
}
