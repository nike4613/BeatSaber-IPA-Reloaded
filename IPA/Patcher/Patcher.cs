using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IPA.Patcher
{
    class PatchedModule
    {
        private static readonly string[] ENTRY_TYPES = { "Input", "Display" };

        private FileInfo _File;
        private ModuleDefinition _Module;

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
            _File = new FileInfo(engineFile);

            LoadModules();
        }

        private void LoadModules()
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(_File.DirectoryName);

            var parameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
            };
            
            _Module = ModuleDefinition.ReadModule(_File.FullName, parameters);
        }
        
        public PatchData Data
        {
            get
            {
                var IIdata = new PatchData { IsPatched = false, Version = null };
                foreach (var @ref in _Module.AssemblyReferences) {
                    if (@ref.Name == "IllusionInjector") IIdata = new PatchData { IsPatched = true, Version = new Version(0, 0, 0, 0) }; 
                    if (@ref.Name == "IllusionPlugin") IIdata = new PatchData { IsPatched = true, Version = new Version(0, 0, 0, 0) };
                    if (@ref.Name == "IPA.Injector") return new PatchData { IsPatched = true, Version = @ref.Version };
                }
                return IIdata;
            }
        }

        public void Patch(Version v)
        {
            // First, let's add the reference
            var nameReference = new AssemblyNameReference("IPA.Injector", Program.Version);
            var injectorPath = Path.Combine(_File.DirectoryName, "IPA.Injector.dll");
            var injector = ModuleDefinition.ReadModule(injectorPath);

            for (int i = 0; i < _Module.AssemblyReferences.Count; i++)
            {
                if (_Module.AssemblyReferences[i].Name == "IllusionInjector")
                    _Module.AssemblyReferences.RemoveAt(i--);
                if (_Module.AssemblyReferences[i].Name == "IllusionPlugin")
                    _Module.AssemblyReferences.RemoveAt(i--);
                if (_Module.AssemblyReferences[i].Name == "IPA.Injector")
                    _Module.AssemblyReferences.RemoveAt(i--);
            }

            _Module.AssemblyReferences.Add(nameReference);

            int patched = 0;
            foreach(var type in FindEntryTypes())
            {
                if(PatchType(type, injector))
                {
                    patched++;
                }
            }
            
            if(patched > 0)
            {
                _Module.Write(_File.FullName);
            } else
            {
                throw new Exception("Could not find any entry type!");
            }
        }

        private bool PatchType(TypeDefinition targetType, ModuleDefinition injector)
        {
            var targetMethod = targetType.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
            if (targetMethod != null)
            {
                var methodReference = _Module.Import(injector.GetType("IPA.Injector.Injector").Methods.First(m => m.Name == "Inject"));
                targetMethod.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, methodReference));
                return true;
            }
            return false;
        }


        private IEnumerable<TypeDefinition> FindEntryTypes()
        {
            return _Module.GetTypes().Where(m => ENTRY_TYPES.Contains(m.Name));
        }
    }
}
