using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModPhuserEx.Protections.XorCryptor
{
    internal class XorCryptorContext
    {
        public AssemblyDef Assembly;
        public byte[] EncryptedModule;
        public MethodDef EntryPoint;
        public uint EntryPointToken;
        public byte[] KeySig;
        public uint KeyToken;
        public ModuleKind Kind;
        public List<Tuple<uint, uint, string>> ManifestResources;
        public int ModuleIndex;
        public string ModuleName;
        public byte[] OriginModule;
        public ModuleDef OriginModuleDef;
        public bool CompatMode;
    }
}
