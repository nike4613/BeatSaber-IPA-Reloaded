using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IPA.Injector
{
    internal class VirtualizedModule
    {
        private const string ENTRY_TYPE = "Display";

        public FileInfo file;
        public ModuleDefinition module;

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
            module = ModuleDefinition.ReadModule(file.FullName);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="module"></param>
        public void Virtualize(AssemblyName selfName, Action beforeChangeCallback = null)
        {
            bool changed = false;
            bool virtualize = true;
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
    }
}
