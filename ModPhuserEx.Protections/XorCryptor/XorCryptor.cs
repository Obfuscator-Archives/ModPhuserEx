using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using dnlib.PE;
using ModPhuserEx.Protections.XorCryptor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using FileAttributes = dnlib.DotNet.FileAttributes;
using SR = System.Reflection;

namespace Confuser.Protections
{
    internal class XorCryptor : Packer
    {
        public const string _Id = "xorcryptor";
        public const string _FullId = "0xFireball.XorCryptor";
        public const string _ServiceId = "0xFireball.XorCryptor";
        public static readonly object ContextKey = new object();

        public override string Name
        {
            get { return "XorCryptor Packer"; }
        }

        public override string Description
        {
            get { return "This packer encrypts the output with a simple XOR cipher."; }
        }

        public override string Id
        {
            get { return _Id; }
        }

        public override string FullId
        {
            get { return _FullId; }
        }

        protected override void Initialize(ConfuserContext context)
        {
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            //TODO: Add something here
        }

        protected override void Pack(ConfuserContext context, ProtectionParameters parameters)
        {
            var ctx = context.Annotations.Get<XorCryptorContext>(context, ContextKey);
            if (ctx == null)
            {
                context.Logger.Error("No executable module!");
                throw new ConfuserException(null);
            }

            ModuleDefMD originModule = context.Modules[ctx.ModuleIndex];
            ctx.OriginModuleDef = originModule;

            var stubModule = new ModuleDefUser(ctx.ModuleName, originModule.Mvid, originModule.CorLibTypes.AssemblyRef);
            if (ctx.CompatMode)
            {
                var assembly = new AssemblyDefUser(originModule.Assembly);
                assembly.Name += ".mpx";
                assembly.Modules.Add(stubModule);
            }
            else
            {
                ctx.Assembly.Modules.Insert(0, stubModule);
                ImportAssemblyTypeReferences(originModule, stubModule);
            }
            stubModule.Characteristics = originModule.Characteristics;
            stubModule.Cor20HeaderFlags = originModule.Cor20HeaderFlags;
            stubModule.Cor20HeaderRuntimeVersion = originModule.Cor20HeaderRuntimeVersion;
            stubModule.DllCharacteristics = originModule.DllCharacteristics;
            stubModule.EncBaseId = originModule.EncBaseId;
            stubModule.EncId = originModule.EncId;
            stubModule.Generation = originModule.Generation;
            stubModule.Kind = ctx.Kind;
            stubModule.Machine = originModule.Machine;
            stubModule.RuntimeVersion = originModule.RuntimeVersion;
            stubModule.TablesHeaderVersion = originModule.TablesHeaderVersion;
            stubModule.Win32Resources = originModule.Win32Resources;

            byte[] executableModuleBytes = null; // TODO: LOL!
        }

        private static string GetId(byte[] module)
        {
            var md = MetaDataCreator.CreateMetaData(new PEImage(module));
            var assemblyRow = md.TablesStream.ReadAssemblyRow(1);
            var assembly = new AssemblyNameInfo();
            assembly.Name = md.StringsStream.ReadNoNull(assemblyRow.Name);
            assembly.Culture = md.StringsStream.ReadNoNull(assemblyRow.Locale);
            assembly.PublicKeyOrToken = new PublicKey(md.BlobStream.Read(assemblyRow.PublicKey));
            assembly.HashAlgId = (AssemblyHashAlgorithm)assemblyRow.HashAlgId;
            assembly.Version = new Version(assemblyRow.MajorVersion, assemblyRow.MinorVersion, assemblyRow.BuildNumber, assemblyRow.RevisionNumber);
            assembly.Attributes = (AssemblyAttributes)assemblyRow.Flags;
            return GetId(assembly);
        }

        private static string GetId(IAssembly assembly)
        {
            return new SR.AssemblyName(assembly.FullName).FullName.ToUpperInvariant();
        }

        void ImportAssemblyTypeReferences(ModuleDef originModule, ModuleDef stubModule)
        {
            var assembly = stubModule.Assembly;
            foreach (var ca in assembly.CustomAttributes)
            {
                if (ca.AttributeType.Scope == originModule)
                    ca.Constructor = (ICustomAttributeType)stubModule.Import(ca.Constructor);
            }
            foreach (var ca in assembly.DeclSecurities.SelectMany(declSec => declSec.CustomAttributes))
            {
                if (ca.AttributeType.Scope == originModule)
                    ca.Constructor = (ICustomAttributeType)stubModule.Import(ca.Constructor);
            }
        }

        class KeyInjector : IModuleWriterListener
        {
            readonly XorCryptorContext ctx;

            public KeyInjector(XorCryptorContext context)
            {
                ctx = context;
            }

            public void OnWriterEvent(ModuleWriterBase writer, ModuleWriterEvent evt)
            {
                if (evt == ModuleWriterEvent.MDBeginCreateTables)
                {
                    // Add key signature
                    uint sigBlob = writer.MetaData.BlobHeap.Add(ctx.KeySig);
                    uint sigRid = writer.MetaData.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(sigBlob));
                    Debug.Assert(sigRid == 1);
                    uint sigToken = 0x11000000 | sigRid;
                    ctx.KeyToken = sigToken;
                    MutationHelper.InjectKey(writer.Module.EntryPoint, 2, (int)sigToken);
                }
                else if (evt == ModuleWriterEvent.MDBeginAddResources && !ctx.CompatMode)
                {
                    // Compute hash
                    byte[] hash = SHA1.Create().ComputeHash(ctx.OriginModule);
                    uint hashBlob = writer.MetaData.BlobHeap.Add(hash);

                    MDTable<RawFileRow> fileTbl = writer.MetaData.TablesHeap.FileTable;
                    uint fileRid = fileTbl.Add(new RawFileRow(
                                                   (uint)FileAttributes.ContainsMetaData,
                                                   writer.MetaData.StringsHeap.Add("koi"),
                                                   hashBlob));
                    uint impl = CodedToken.Implementation.Encode(new MDToken(Table.File, fileRid));

                    // Add resources
                    MDTable<RawManifestResourceRow> resTbl = writer.MetaData.TablesHeap.ManifestResourceTable;
                    foreach (var resource in ctx.ManifestResources)
                        resTbl.Add(new RawManifestResourceRow(resource.Item1, resource.Item2, writer.MetaData.StringsHeap.Add(resource.Item3), impl));

                    // Add exported types
                    var exTbl = writer.MetaData.TablesHeap.ExportedTypeTable;
                    foreach (var type in ctx.OriginModuleDef.GetTypes())
                    {
                        if (!type.IsVisibleOutside())
                            continue;
                        exTbl.Add(new RawExportedTypeRow((uint)type.Attributes, 0,
                                                         writer.MetaData.StringsHeap.Add(type.Name),
                                                         writer.MetaData.StringsHeap.Add(type.Namespace), impl));
                    }
                }
            }
        }
    }
}