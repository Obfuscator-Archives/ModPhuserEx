using Confuser.Core;
using Confuser.Renamer.Analyzers;
using dnlib.DotNet;
using System.Collections.Generic;
using System.Linq;

namespace Confuser.Renamer
{
    internal class AnalyzePhase : ProtectionPhase
    {
        public AnalyzePhase(NameProtection parent)
            : base(parent) { }

        public override bool ProcessAll
        {
            get { return true; }
        }

        public override ProtectionTargets Targets
        {
            get { return ProtectionTargets.AllDefinitions; }
        }

        public override string Name
        {
            get { return "Name analysis"; }
        }

        private void ParseParameters(IDnlibDef def, ConfuserContext context, NameService service, ProtectionParameters parameters)
        {
            var mode = parameters.GetParameter<RenameMode?>(context, def, "mode", null);
            if (mode != null)
                service.SetRenameMode(def, mode.Value);
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            var service = (NameService)context.Registry.GetService<INameService>();
            context.Logger.Debug("Building VTables & identifier list...");
            foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger))
            {
                ParseParameters(def, context, service, parameters);

                if (def is ModuleDef)
                {
                    var module = (ModuleDef)def;
                    foreach (Resource res in module.Resources)
                        service.SetOriginalName(res, res.Name);
                }
                else
                    service.SetOriginalName(def, def.Name);

                if (def is TypeDef)
                {
                    service.GetVTables().GetVTable((TypeDef)def);
                    service.SetOriginalNamespace(def, ((TypeDef)def).Namespace);
                }
                context.CheckCancellation();
            }

            context.Logger.Debug("Analyzing...");
            RegisterRenamers(context, service);
            IList<IRenamer> renamers = service.Renamers;
            foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger))
            {
                Analyze(service, context, parameters, def, true);
                context.CheckCancellation();
            }
        }

        private void RegisterRenamers(ConfuserContext context, NameService service)
        {
            bool wpf = false,
                 caliburn = false,
                 winforms = false,
                 json = false,
                 aspnetcore = false,
                 nancyfx = false;

            foreach (var module in context.Modules)
                foreach (var asmRef in module.GetAssemblyRefs())
                {
                    if (asmRef.Name == "WindowsBase" || asmRef.Name == "PresentationCore" ||
                        asmRef.Name == "PresentationFramework" || asmRef.Name == "System.Xaml")
                    {
                        wpf = true;
                    }
                    else if (asmRef.Name == "Caliburn.Micro")
                    {
                        caliburn = true;
                    }
                    else if (asmRef.Name == "System.Windows.Forms")
                    {
                        winforms = true;
                    }
                    else if (asmRef.Name == "Newtonsoft.Json")
                    {
                        json = true;
                    }
                    else if (asmRef.Name.StartsWith("Microsoft.AspNetCore."))
                    {
                        aspnetcore = true;
                    }
                    else if (asmRef.Name == "Nancy")
                    {
                        nancyfx = true;
                    }
                }

            if (wpf)
            {
                var wpfAnalyzer = new WPFAnalyzer();
                context.Logger.Debug("WPF found, enabling compatibility.");
                service.Renamers.Add(wpfAnalyzer);
                if (caliburn)
                {
                    context.Logger.Debug("Caliburn.Micro found, enabling compatibility.");
                    service.Renamers.Add(new CaliburnAnalyzer(wpfAnalyzer));
                }
            }

            if (winforms)
            {
                var winformsAnalyzer = new WinFormsAnalyzer();
                context.Logger.Debug("WinForms found, enabling compatibility.");
                service.Renamers.Add(winformsAnalyzer);
            }

            if (json)
            {
                var jsonAnalyzer = new JsonAnalyzer();
                context.Logger.Debug("Newtonsoft.Json found, enabling compatibility.");
                service.Renamers.Add(jsonAnalyzer);
            }

            if (aspnetcore)
            {
                var aspnetCoreAnalyzer = new AspNetCoreAnalyzer();
                context.Logger.Debug("ASP.NET Core found, enabling compatibility.");
                service.Renamers.Add(aspnetCoreAnalyzer);
            }

            if (nancyfx)
            {
                var nancyFxAnalyzer = new NancyFxAnalyzer();
                context.Logger.Debug("NancyFx found, enabling compatibility.");
                service.Renamers.Add(nancyFxAnalyzer);
            }
        }

        internal void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, IDnlibDef def, bool runAnalyzer)
        {
            if (def is TypeDef)
                Analyze(service, context, parameters, (TypeDef)def);
            else if (def is MethodDef)
                Analyze(service, context, parameters, (MethodDef)def);
            else if (def is FieldDef)
                Analyze(service, context, parameters, (FieldDef)def);
            else if (def is PropertyDef)
                Analyze(service, context, parameters, (PropertyDef)def);
            else if (def is EventDef)
                Analyze(service, context, parameters, (EventDef)def);
            else if (def is ModuleDef)
            {
                var pass = parameters.GetParameter<string>(context, def, "password", null);
                if (pass != null)
                    service.reversibleRenamer = new ReversibleRenamer(pass);

                var idOffset = parameters.GetParameter<uint>(context, def, "idOffset", 0);
                if (idOffset != 0)
                    service.SetNameId(idOffset);

                service.SetCanRename(def, false);
            }

            if (!runAnalyzer || parameters.GetParameter(context, def, "forceRen", false))
                return;

            foreach (IRenamer renamer in service.Renamers)
                renamer.Analyze(context, service, parameters, def);
        }

        private static bool IsVisibleOutside(ConfuserContext context, ProtectionParameters parameters, IMemberDef def)
        {
            var type = def as TypeDef;
            if (type == null)
                type = def.DeclaringType;

            var renPublic = parameters.GetParameter<bool?>(context, def, "renPublic", null);
            if (renPublic == null)
                return type.IsVisibleOutside();
            else
                return type.IsVisibleOutside(false) && !renPublic.Value;
        }

        private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, TypeDef type)
        {
            if (IsVisibleOutside(context, parameters, type))
            {
                service.SetCanRename(type, false);
            }
            else if (type.IsRuntimeSpecialName || type.IsGlobalModuleType)
            {
                service.SetCanRename(type, false);
            }
            else if (type.FullName == "ModPhusedByAttribute")
            {
                // Courtesy
                service.SetCanRename(type, false);
            }

            if (type.CustomAttributes.Any(x => x.AttributeType.FullName == "System.Reflection.ObfuscationAttribute"))
            {
                var obfuscationAttr = type.CustomAttributes.First(x => x.AttributeType.FullName == "System.Reflection.ObfuscationAttribute");
                var excludeParam = obfuscationAttr.NamedArguments.FirstOrDefault(x => x.Name == "Exclude");
                if (excludeParam != null && ((bool)excludeParam.Value))
                {
                    // Obfuscation exclude, don't rename
                    service.SetCanRename(type, false);
                    // Also leave properties/methods alone
                    foreach (var method in type.Methods)
                    {
                        service.SetCanRename(method, false);
                    }
                    // Also leave CONST fields alone
                    foreach (var constField in type.Fields.Where(x => x.IsStatic && x.IsPublic))
                    {
                        service.SetCanRename(constField, false);
                    }
                    // Remove attribute
                    type.CustomAttributes.Remove(obfuscationAttr);
                }
            }

            if (parameters.GetParameter(context, type, "forceRen", false))
                return;

            if (type.InheritsFromCorlib("System.Attribute"))
            {
                service.ReduceRenameMode(type, RenameMode.ASCII);
            }

            if (type.InheritsFrom("System.Configuration.SettingsBase"))
            {
                service.SetCanRename(type, false);
            }
        }

        private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, MethodDef method)
        {
            if (IsVisibleOutside(context, parameters, method.DeclaringType) &&
                (method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic) &&
                IsVisibleOutside(context, parameters, method))
                service.SetCanRename(method, false);
            else if (method.IsRuntimeSpecialName)
                service.SetCanRename(method, false);
            else if (parameters.GetParameter(context, method, "forceRen", false))
                return;
            else if (method.DeclaringType.IsComImport() && !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
                service.SetCanRename(method, false);
            else if (method.DeclaringType.IsDelegate())
                service.SetCanRename(method, false);
        }

        private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, FieldDef field)
        {
            if (IsVisibleOutside(context, parameters, field.DeclaringType) &&
                (field.IsFamily || field.IsFamilyOrAssembly || field.IsPublic) &&
                IsVisibleOutside(context, parameters, field))
                service.SetCanRename(field, false);
            else if (field.IsRuntimeSpecialName)
                service.SetCanRename(field, false);
            else if (parameters.GetParameter(context, field, "forceRen", false))
                return;
            else if (field.DeclaringType.IsSerializable && !field.IsNotSerialized)
                service.SetCanRename(field, false);
            else if (field.IsLiteral && field.DeclaringType.IsEnum &&
                !parameters.GetParameter(context, field, "renEnum", false))
                service.SetCanRename(field, false);
        }

        private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, PropertyDef property)
        {
            if (IsVisibleOutside(context, parameters, property.DeclaringType) &&
                IsVisibleOutside(context, parameters, property))
                service.SetCanRename(property, false);
            else if (property.IsRuntimeSpecialName)
                service.SetCanRename(property, false);
            else if (parameters.GetParameter(context, property, "forceRen", false))
                return;
            else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
                service.SetCanRename(property, false);
            else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
                service.SetCanRename(property, false);
        }

        private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, EventDef evt)
        {
            if (IsVisibleOutside(context, parameters, evt.DeclaringType) &&
                IsVisibleOutside(context, parameters, evt))
                service.SetCanRename(evt, false);
            else if (evt.IsRuntimeSpecialName)
                service.SetCanRename(evt, false);
        }
    }
}