using Mono.Cecil;
using System;
using System.IO;
using System.Reflection;

namespace IPA.Injector
{
    internal class VirtualizedModule : IDisposable
    {
        private readonly FileInfo file;
        private ModuleDefinition module;

        public static VirtualizedModule Load(string engineFile)
        {
            return new VirtualizedModule(engineFile);
        }

        private VirtualizedModule(string assemblyFile)
        {
            file = new FileInfo(assemblyFile);

            LoadModules();
        }

        private void LoadModules()
        {
            module = ModuleDefinition.ReadModule(file.FullName, new ReaderParameters
            {
                ReadWrite = false,
                InMemory = true,
                ReadingMode = ReadingMode.Immediate
            });
        }
        
        public void Virtualize(AssemblyName selfName, Action beforeChangeCallback = null)
        {
            var changed = false;
            var virtualize = true;
            foreach (var r in module.AssemblyReferences)
            {
                if (r.Name == selfName.Name)
                {
                    virtualize = false;
                    if (r.Version != selfName.Version)
                    {
                        r.Version = selfName.Version;
                        changed = true;
                    }
                }
            }

            if (virtualize)
            {
                changed = true;
                module.AssemblyReferences.Add(new AssemblyNameReference(selfName.Name, selfName.Version));

                foreach (var type in module.Types)
                {
                    VirtualizeType(type);
                }
            }

            if (changed)
            {
                beforeChangeCallback?.Invoke();
                module.Write(file.FullName);
            }
        }

        private void VirtualizeType(TypeDefinition type)
        {
            if(type.IsSealed)
            {
                // Unseal
                type.IsSealed = false;
            }

            if (type.IsInterface) return;
            if (type.IsAbstract) return;

            // These two don't seem to work.
            if (type.Name == "SceneControl" || type.Name == "ConfigUI") return;
            
            // Take care of sub types
            foreach (var subType in type.NestedTypes)
            {
                VirtualizeType(subType);
            }

            foreach (var method in type.Methods)
            {
                if (method.IsManaged
                    && method.IsIL
                    && !method.IsStatic
                    && !method.IsVirtual
                    && !method.IsAbstract
                    && !method.IsAddOn
                    && !method.IsConstructor
                    && !method.IsSpecialName
                    && !method.IsGenericInstance
                    && !method.HasOverrides)
                {
                    method.IsVirtual = true;
                    method.IsPublic = true;
                    method.IsPrivate = false;
                    method.IsNewSlot = true;
                    method.IsHideBySig = true;
                }
            }

            foreach (var field in type.Fields)
            {
                if (field.IsPrivate) field.IsFamily = true;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    module.Dispose();
                }

                disposedValue = true;
            }
        }

        ~VirtualizedModule()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
