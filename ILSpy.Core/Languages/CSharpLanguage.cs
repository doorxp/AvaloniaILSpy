﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.Solution;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;

namespace ICSharpCode.ILSpy
{
    /// <summary>
    /// C# decompiler integration into ILSpy.
    /// Note: if you're interested in using the decompiler without the ILSpy UI,
    /// please directly use the CSharpDecompiler class.
    /// </summary>
    [Export(typeof(Language))]
    public class CSharpLanguage : Language
    {
        string name = "C#";
        bool showAllMembers = false;
        int transformCount = int.MaxValue;

#if DEBUG
        internal static IEnumerable<CSharpLanguage> GetDebugLanguages()
        {
            var lastTransformName = "no transforms";
            var transformCount = 0;
            foreach (var transform in CSharpDecompiler.GetAstTransforms())
            {
                yield return new CSharpLanguage
                {
                    transformCount = transformCount,
                    name = "C# - " + lastTransformName,
                    showAllMembers = true
                };
                lastTransformName = "after " + transform.GetType().Name;
                transformCount++;
            }
            yield return new CSharpLanguage
            {
                name = "C# - " + lastTransformName,
                showAllMembers = true
            };
        }
#endif

        public override string Name => name;

        public override string FileExtension => ".cs";

        public override string ProjectFileExtension => ".csproj";

        IReadOnlyList<LanguageVersion> versions;

        public override IReadOnlyList<LanguageVersion> LanguageVersions =>
            versions ??= new List<LanguageVersion>()
            {
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp1), "C# 1.0 / VS .NET"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp2), "C# 2.0 / VS 2005"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp3), "C# 3.0 / VS 2008"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp4), "C# 4.0 / VS 2010"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp5), "C# 5.0 / VS 2012"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp6), "C# 6.0 / VS 2015"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp7), "C# 7.0 / VS 2017"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp7_1), "C# 7.1 / VS 2017.3"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp7_2), "C# 7.2 / VS 2017.4"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp7_3), "C# 7.3 / VS 2017.7"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp8_0), "C# 8.0 / VS 2019"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp9_0), "C# 9.0 / VS 2019.8"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp10_0), "C# 10.0 / VS 2022"),
                new LanguageVersion(nameof(Decompiler.CSharp.LanguageVersion.CSharp11_0), "C# 11.0 / VS 2022.4"),
            };

        CSharpDecompiler CreateDecompiler(PEFile module, DecompilationOptions options)
        {
            var decompiler = new CSharpDecompiler(module, module.GetAssemblyResolver(), options.DecompilerSettings)
                {
                    CancellationToken = options.CancellationToken,
                    DebugInfoProvider = module.GetDebugInfoOrNull()
                };
            while (decompiler.AstTransforms.Count > transformCount)
                decompiler.AstTransforms.RemoveAt(decompiler.AstTransforms.Count - 1);
            return decompiler;
        }

        void WriteCode(ITextOutput output, DecompilerSettings settings, SyntaxTree syntaxTree, IDecompilerTypeSystem typeSystem)
        {
            syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
            output.IndentationString = settings.CSharpFormattingOptions.IndentationString;
            TokenWriter tokenWriter = new TextTokenWriter(output, settings, typeSystem);
            if (output is ISmartTextOutput highlightingOutput)
            {
                tokenWriter = new CSharpHighlightingTokenWriter(tokenWriter, highlightingOutput);
            }
            syntaxTree.AcceptVisitor(new CSharpOutputVisitor(tokenWriter, settings.CSharpFormattingOptions));
        }

        public override void DecompileMethod(IMethod method, ITextOutput output, DecompilationOptions options)
        {
            var assembly = (PEFile)method.ParentModule?.MetadataFile;
            var decompiler = CreateDecompiler(assembly, options);
            AddReferenceAssemblyWarningMessage(assembly, output);
            AddReferenceWarningMessage(assembly, output);
            WriteCommentLine(output, TypeToString(method.DeclaringType, includeNamespace: true));
            if (decompiler.TypeSystem.MainModule.ResolveEntity(method.MetadataToken) is IMethod { IsConstructor: true } methodDefinition && methodDefinition.DeclaringType.IsReferenceType != false)
            {
                var members = CollectFieldsAndCtors(methodDefinition.DeclaringTypeDefinition, methodDefinition.IsStatic);
                decompiler.AstTransforms.Add(new SelectCtorTransform(methodDefinition));
                WriteCode(output, options.DecompilerSettings, decompiler.Decompile(members), decompiler.TypeSystem);
            }
            else
            {
                WriteCode(output, options.DecompilerSettings, decompiler.Decompile(method.MetadataToken), decompiler.TypeSystem);
            }
        }

        class SelectCtorTransform : IAstTransform
        {
            readonly IMethod ctor;
            readonly HashSet<ISymbol> removedSymbols = new HashSet<ISymbol>();

            public SelectCtorTransform(IMethod ctor)
            {
                this.ctor = ctor;
            }

            public void Run(AstNode rootNode, TransformContext context)
            {
                ConstructorDeclaration ctorDecl = null;
                foreach (var node in rootNode.Children)
                {
                    switch (node)
                    {
                        case ConstructorDeclaration ctor:
                            if (Equals(ctor.GetSymbol(), this.ctor))
                            {
                                ctorDecl = ctor;
                            }
                            else
                            {
                                // remove other ctors
                                ctor.Remove();
                                removedSymbols.Add(ctor.GetSymbol());
                            }
                            break;
                        case FieldDeclaration fd:
                            // Remove any fields without initializers
                            if (fd.Variables.All(v => v.Initializer.IsNull))
                            {
                                fd.Remove();
                                removedSymbols.Add(fd.GetSymbol());
                            }
                            break;
                    }
                }
                if (ctorDecl?.Initializer.ConstructorInitializerType == ConstructorInitializerType.This)
                {
                    // remove all fields
                    foreach (var node in rootNode.Children)
                    {
                        switch (node)
                        {
                            case FieldDeclaration fd:
                                fd.Remove();
                                removedSymbols.Add(fd.GetSymbol());
                                break;
                        }
                    }
                }
                foreach (var node in rootNode.Children)
                {
                    if (node is Comment && removedSymbols.Contains(node.GetSymbol()))
                        node.Remove();
                }
            }
        }

        public override void DecompileProperty(IProperty property, ITextOutput output, DecompilationOptions options)
        {
            var assembly = (PEFile)property.ParentModule?.MetadataFile;
            var decompiler = CreateDecompiler(assembly, options);
            AddReferenceAssemblyWarningMessage(assembly, output);
            AddReferenceWarningMessage(assembly, output);
            WriteCommentLine(output, TypeToString(property.DeclaringType, includeNamespace: true));
            WriteCode(output, options.DecompilerSettings, decompiler.Decompile(property.MetadataToken), decompiler.TypeSystem);
        }

        public override void DecompileField(IField field, ITextOutput output, DecompilationOptions options)
        {
            var assembly = (PEFile)field.ParentModule?.MetadataFile;
            var decompiler = CreateDecompiler(assembly, options);
            AddReferenceAssemblyWarningMessage(assembly, output);
            AddReferenceWarningMessage(assembly, output);
            WriteCommentLine(output, TypeToString(field.DeclaringType, includeNamespace: true));
            if (field.IsConst)
            {
                WriteCode(output, options.DecompilerSettings, decompiler.Decompile(field.MetadataToken), decompiler.TypeSystem);
            }
            else
            {
                var members = CollectFieldsAndCtors(field.DeclaringTypeDefinition, field.IsStatic);
                var resolvedField = decompiler.TypeSystem.MainModule.GetDefinition((FieldDefinitionHandle)field.MetadataToken);
                decompiler.AstTransforms.Add(new SelectFieldTransform(resolvedField));
                WriteCode(output, options.DecompilerSettings, decompiler.Decompile(members), decompiler.TypeSystem);
            }
        }

        static List<EntityHandle> CollectFieldsAndCtors(ITypeDefinition type, bool isStatic)
        {
            var members = (from field in type.Fields where !field.MetadataToken.IsNil && field.IsStatic == isStatic select field.MetadataToken).ToList();
            members.AddRange(from ctor in type.Methods where !ctor.MetadataToken.IsNil && ctor.IsConstructor && ctor.IsStatic == isStatic select ctor.MetadataToken);

            return members;
        }

        /// <summary>
        /// Removes all top-level members except for the specified fields.
        /// </summary>
        sealed class SelectFieldTransform : IAstTransform
        {
            readonly IField field;

            public SelectFieldTransform(IField field)
            {
                this.field = field;
            }

            public void Run(AstNode rootNode, TransformContext context)
            {
                foreach (var node in rootNode.Children)
                {
                    switch (node)
                    {
                        case EntityDeclaration ed:
                            if (!Equals(node.GetSymbol(), field))
                                node.Remove();
                            break;
                        case Comment c:
                            if (!Equals(c.GetSymbol(), field))
                                node.Remove();
                            break;
                    }
                }
            }
        }

        public override void DecompileEvent(IEvent @event, ITextOutput output, DecompilationOptions options)
        {
            using var assembly = (PEFile)@event.ParentModule?.MetadataFile;
            var decompiler = CreateDecompiler(assembly, options);
            AddReferenceAssemblyWarningMessage(assembly, output);
            AddReferenceWarningMessage(assembly, output);
            WriteCommentLine(output, TypeToString(@event.DeclaringType, includeNamespace: true));
            WriteCode(output, options.DecompilerSettings, decompiler.Decompile(@event.MetadataToken), decompiler.TypeSystem);
        }

        public override void DecompileType(ITypeDefinition type, ITextOutput output, DecompilationOptions options)
        {
            var assembly = (PEFile)type.ParentModule?.MetadataFile;
            var decompiler = CreateDecompiler(assembly, options);
            AddReferenceAssemblyWarningMessage(assembly, output);
            AddReferenceWarningMessage(assembly, output);
            WriteCommentLine(output, TypeToString(type, includeNamespace: true));
            WriteCode(output, options.DecompilerSettings, decompiler.Decompile(type.MetadataToken), decompiler.TypeSystem);
        }

        void AddReferenceWarningMessage(PEFile module, ITextOutput output)
        {
            var loadedAssembly = MainWindow.Instance.CurrentAssemblyList.GetAssemblies().FirstOrDefault(la => la.GetPEFileOrNull() == module);
            if (loadedAssembly == null || !loadedAssembly.LoadedAssemblyReferencesInfo.HasErrors)
                return;
            var line1 = Properties.Resources.WarningSomeAssemblyReference;
            var line2 = Properties.Resources.PropertyManuallyMissingReferencesListLoadedAssemblies;

            AddWarningMessage(module, output, line1, line2, Properties.Resources.ShowAssemblyLoad, Images.ViewCode, delegate
            {
                var assemblyNode = MainWindow.Instance.FindTreeNode(module);
                assemblyNode.EnsureLazyChildren();
                MainWindow.Instance.SelectNode(assemblyNode.Children.OfType<ReferenceFolderTreeNode>().Single());
            });
        }

        void AddReferenceAssemblyWarningMessage(PEFile module, ITextOutput output)
        {
            var metadata = module.Metadata;
            if (!metadata.GetCustomAttributes(Handle.AssemblyDefinition).HasKnownAttribute(metadata, KnownAttribute.ReferenceAssembly))
                return;
            var line1 = Properties.Resources.WarningAsmMarkedRef;
            AddWarningMessage(module, output, line1);
        }

        void AddWarningMessage(PEFile module, ITextOutput output, string line1, string line2 = null,
            string buttonText = null, IBitmap buttonImage = null, EventHandler<Avalonia.Interactivity.RoutedEventArgs> buttonClickHandler = null)
        {
            var text = line1;
            if (output is ISmartTextOutput fancyOutput)
            {
                if (!string.IsNullOrEmpty(line2))
                    text += Environment.NewLine + line2;
                fancyOutput.AddUIElement(() => new StackPanel
                {
                    Margin = new Thickness(5),
                    Orientation = Orientation.Horizontal,
                    Children = {
                        new Image {
                            Width = 32,
                            Height = 32,
                            Source = Images.LoadImage(this, "Images/Warning.png")
                        },
                        new TextBlock {
                            Margin = new Thickness(5, 0, 0, 0),
                            Text = text
                        }
                    }
                });
                fancyOutput.WriteLine();
                if (buttonText == null || buttonClickHandler == null) return;
                fancyOutput.AddButton(buttonImage, buttonText, buttonClickHandler);
                fancyOutput.WriteLine();
            }
            else
            {
                WriteCommentLine(output, line1);
                if (!string.IsNullOrEmpty(line2))
                    WriteCommentLine(output, line2);
            }
        }

        public override ProjectId DecompileAssembly(LoadedAssembly assembly, ITextOutput output, DecompilationOptions options)
        {
            var module = assembly.GetPEFileOrNull();
            if (options.FullDecompilation && options.SaveAsProjectDirectory != null)
            {
                var decompiler = new ILSpyWholeProjectDecompiler(assembly, options);
                return decompiler.DecompileProject(module, options.SaveAsProjectDirectory, new TextOutputWriter(output), options.CancellationToken);
            }

            AddReferenceAssemblyWarningMessage(module, output);
            AddReferenceWarningMessage(module, output);
            output.WriteLine();
            base.DecompileAssembly(assembly, output, options);

            // don't automatically load additional assemblies when an assembly node is selected in the tree view
            var assemblyResolver = assembly.GetAssemblyResolver();
            var runtimeName = GetRuntimeDisplayName(module);
            using (options.FullDecompilation ? null : LoadedAssembly.DisableAssemblyLoad())
            {
                var typeSystem = new DecompilerTypeSystem(module, assemblyResolver, options.DecompilerSettings);
                var globalType = typeSystem.MainModule.TypeDefinitions.FirstOrDefault();
                if (globalType != null)
                {
                    output.Write("// Global type: ");
                    output.WriteReference(globalType, globalType.FullName);
                    output.WriteLine();
                }
                var metadata = module.Metadata;
                var corHeader = module.Reader.PEHeaders.CorHeader;
                var entrypointHandle = MetadataTokenHelpers.EntityHandleOrNil(corHeader.EntryPointTokenOrRelativeVirtualAddress);
                if (entrypointHandle is { IsNil: false, Kind: HandleKind.MethodDefinition })
                {
                    var entrypoint = typeSystem.MainModule.ResolveMethod(entrypointHandle, new Decompiler.TypeSystem.GenericContext());
                    if (entrypoint != null)
                    {
                        output.Write("// Entry point: ");
                        output.WriteReference(entrypoint, entrypoint.DeclaringType.FullName + "." + entrypoint.Name);
                        output.WriteLine();
                    }
                }
                output.WriteLine("// Architecture: " + GetPlatformDisplayName(module));
                if ((corHeader.Flags & System.Reflection.PortableExecutable.CorFlags.ILOnly) == 0)
                {
                    output.WriteLine("// This assembly contains unmanaged code.");
                }

                if (runtimeName != null)
                {
                    output.WriteLine("// Runtime: " + runtimeName);
                }
                if ((corHeader.Flags & System.Reflection.PortableExecutable.CorFlags.StrongNameSigned) != 0)
                {
                    output.WriteLine("// This assembly is signed with a strong name key.");
                }
                if (metadata.IsAssembly)
                {
                    var asm = metadata.GetAssemblyDefinition();
                    if (asm.HashAlgorithm != AssemblyHashAlgorithm.None)
                        output.WriteLine("// Hash algorithm: " + asm.HashAlgorithm.ToString().ToUpper());
                    if (!asm.PublicKey.IsNil)
                    {
                        output.Write("// Public key: ");
                        var reader = metadata.GetBlobReader(asm.PublicKey);
                        while (reader.RemainingBytes > 0)
                            output.Write(reader.ReadByte().ToString("x2"));
                        output.WriteLine();
                    }
                }
                var debugInfo = assembly.GetDebugInfoOrNull();
                if (debugInfo != null)
                {
                    output.WriteLine("// Debug info: " + debugInfo.Description);
                }
                output.WriteLine();

                var decompiler = new CSharpDecompiler(typeSystem, options.DecompilerSettings)
                    {
                        CancellationToken = options.CancellationToken
                    };
                var st = options.FullDecompilation ? decompiler.DecompileWholeModuleAsSingleFile() : decompiler.DecompileModuleAndAssemblyAttributes();
                WriteCode(output, options.DecompilerSettings, st, decompiler.TypeSystem);
            }
            return null;
        }

        class ILSpyWholeProjectDecompiler : WholeProjectDecompiler
        {
            readonly LoadedAssembly assembly;
            readonly DecompilationOptions options;

            public ILSpyWholeProjectDecompiler(LoadedAssembly assembly, DecompilationOptions options)
            : base(
                options.DecompilerSettings,
                assembly.GetAssemblyResolver(),
                null,
                null,
                assembly.GetDebugInfoOrNull()
            )
            {
                this.assembly = assembly;
                this.options = options;
            }

            protected override IEnumerable<ProjectItemInfo> WriteResourceToFile(string fileName, string resourceName, Stream entryStream)
            {
                foreach (var handler in App.ExportProvider.GetExportedValues<IResourceFileHandler>())
                {
                    if (!handler.CanHandle(fileName, options)) continue;
                    entryStream.Position = 0;
                    fileName = handler.WriteResourceToFile(assembly, fileName, entryStream, options);
                    return new[] { new ProjectItemInfo(handler.EntryType, fileName) };
                }
                return base.WriteResourceToFile(fileName, resourceName, entryStream);
            }
        }

        static CSharpAmbience CreateAmbience()
        {
            var ambience = new CSharpAmbience
            {
                // Do not forget to update CSharpAmbienceTests.ILSpyMainTreeViewTypeFlags, if this ever changes.
                ConversionFlags = ConversionFlags.ShowTypeParameterList | ConversionFlags.PlaceReturnTypeAfterParameterList
            };
            return ambience;
        }

        static string EntityToString(IEntity entity, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            // Do not forget to update CSharpAmbienceTests, if this ever changes.
            var ambience = CreateAmbience();
            ambience.ConversionFlags |= ConversionFlags.ShowReturnType | ConversionFlags.ShowParameterList | ConversionFlags.ShowParameterModifiers;
            if (includeDeclaringTypeName)
                ambience.ConversionFlags |= ConversionFlags.ShowDeclaringType;
            if (includeNamespace)
                ambience.ConversionFlags |= ConversionFlags.UseFullyQualifiedTypeNames;
            if (includeNamespaceOfDeclaringTypeName)
                ambience.ConversionFlags |= ConversionFlags.UseFullyQualifiedEntityNames;
            return ambience.ConvertSymbol(entity);
        }

        public override string TypeToString(IType type, bool includeNamespace)
        {
            ArgumentNullException.ThrowIfNull(type);
            var ambience = CreateAmbience();
            // Do not forget to update CSharpAmbienceTests.ILSpyMainTreeViewFlags, if this ever changes.
            if (includeNamespace)
            {
                ambience.ConversionFlags |= ConversionFlags.UseFullyQualifiedTypeNames;
                ambience.ConversionFlags |= ConversionFlags.UseFullyQualifiedEntityNames;
            }
            if (type is ITypeDefinition definition)
            {
                return ambience.ConvertSymbol(definition);
                // HACK : UnknownType is not supported by CSharpAmbience.
            }

            if (type.Kind == TypeKind.Unknown)
            {
                return (includeNamespace ? type.FullName : type.Name)
                       + (type.TypeParameterCount > 0 ? "<" + string.Join(",", type.TypeArguments.Select(t => t.Name)) + ">" : "");
            }

            return ambience.ConvertType(type);
        }

        public override string FieldToString(IField field, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            return field == null
                ? throw new ArgumentNullException(nameof(field))
                : EntityToString(field, includeDeclaringTypeName, includeNamespace,
                    includeNamespaceOfDeclaringTypeName);
        }

        public override string PropertyToString(IProperty property, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            ArgumentNullException.ThrowIfNull(property);
            return EntityToString(property, includeDeclaringTypeName, includeNamespace, includeNamespaceOfDeclaringTypeName);
        }

        public override string MethodToString(IMethod method, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            ArgumentNullException.ThrowIfNull(method);
            return EntityToString(method, includeDeclaringTypeName, includeNamespace, includeNamespaceOfDeclaringTypeName);
        }

        public override string EventToString(IEvent @event, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            return @event == null
                ? throw new ArgumentNullException(nameof(@event))
                : EntityToString(@event, includeDeclaringTypeName, includeNamespace,
                    includeNamespaceOfDeclaringTypeName);
        }

        string ToCSharpString(MetadataReader metadata, TypeDefinitionHandle handle, bool fullName, bool omitGenerics)
        {
            var builder = new StringBuilder();
            var currentTypeDefHandle = handle;
            var typeDef = metadata.GetTypeDefinition(currentTypeDefHandle);

            while (!currentTypeDefHandle.IsNil)
            {
                if (builder.Length > 0)
                    builder.Insert(0, '.');
                typeDef = metadata.GetTypeDefinition(currentTypeDefHandle);
                var part = ReflectionHelper.SplitTypeParameterCountFromReflectionName(metadata.GetString(typeDef.Name), out int typeParamCount);
                var genericParams = typeDef.GetGenericParameters();
                if (!omitGenerics && genericParams.Count > 0)
                {
                    builder.Insert(0, '>');
                    var firstIndex = genericParams.Count - typeParamCount;
                    for (var i = genericParams.Count - 1; i >= genericParams.Count - typeParamCount; i--)
                    {
                        builder.Insert(0, metadata.GetString(metadata.GetGenericParameter(genericParams[i]).Name));
                        builder.Insert(0, i == firstIndex ? '<' : ',');
                    }
                }
                builder.Insert(0, part);
                currentTypeDefHandle = typeDef.GetDeclaringType();
                if (!fullName) break;
            }

            if (!fullName || typeDef.Namespace.IsNil) return builder.ToString();
            builder.Insert(0, '.');
            builder.Insert(0, metadata.GetString(typeDef.Namespace));

            return builder.ToString();
        }

        public override string GetEntityName(PEFile module, EntityHandle handle, bool fullName, bool omitGenerics)
        {
            var metadata = module.Metadata;
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return ToCSharpString(metadata, (TypeDefinitionHandle)handle, fullName, omitGenerics);
                case HandleKind.FieldDefinition:
                    var fd = metadata.GetFieldDefinition((FieldDefinitionHandle)handle);
                    var declaringType = fd.GetDeclaringType();
                    if (fullName)
                        return ToCSharpString(metadata, declaringType, true, omitGenerics) + "." + metadata.GetString(fd.Name);
                    return metadata.GetString(fd.Name);
                case HandleKind.MethodDefinition:
                    var md = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                    declaringType = md.GetDeclaringType();
                    var methodName = metadata.GetString(md.Name);
                    switch (methodName)
                    {
                        case ".ctor":
                        case ".cctor":
                            var td = metadata.GetTypeDefinition(declaringType);
                            methodName = ReflectionHelper.SplitTypeParameterCountFromReflectionName(metadata.GetString(td.Name));
                            break;
                        case "Finalize":
                            const MethodAttributes finalizerAttributes = (MethodAttributes.Virtual | MethodAttributes.Family | MethodAttributes.HideBySig);
                            if ((md.Attributes & finalizerAttributes) != finalizerAttributes)
                                goto default;
                            MethodSignature<IType> methodSignature = md.DecodeSignature(MetadataExtensions.MinimalSignatureTypeProvider, default);
                            if (methodSignature.GenericParameterCount != 0 || methodSignature.ParameterTypes.Length != 0)
                                goto default;
                            td = metadata.GetTypeDefinition(declaringType);
                            methodName = "~" + ReflectionHelper.SplitTypeParameterCountFromReflectionName(metadata.GetString(td.Name));
                            break;
                        default:
                            var genericParams = md.GetGenericParameters();
                            if (!omitGenerics && genericParams.Count > 0)
                            {
                                methodName += "<";
                                var i = 0;
                                foreach (var h in genericParams)
                                {
                                    if (i > 0)
                                        methodName += ",";
                                    var gp = metadata.GetGenericParameter(h);
                                    methodName += metadata.GetString(gp.Name);
                                    i++;
                                }
                                methodName += ">";
                            }
                            break;
                    }
                    if (fullName)
                        return ToCSharpString(metadata, declaringType, true, omitGenerics) + "." + methodName;
                    return methodName;
                case HandleKind.EventDefinition:
                    var ed = metadata.GetEventDefinition((EventDefinitionHandle)handle);
                    declaringType = metadata.GetMethodDefinition(ed.GetAccessors().GetAny()).GetDeclaringType();
                    if (fullName)
                        return ToCSharpString(metadata, declaringType, true, omitGenerics) + "." + metadata.GetString(ed.Name);
                    return metadata.GetString(ed.Name);
                case HandleKind.PropertyDefinition:
                    var pd = metadata.GetPropertyDefinition((PropertyDefinitionHandle)handle);
                    declaringType = metadata.GetMethodDefinition(pd.GetAccessors().GetAny()).GetDeclaringType();
                    if (fullName)
                        return ToCSharpString(metadata, declaringType, true, omitGenerics) + "." + metadata.GetString(pd.Name);
                    return metadata.GetString(pd.Name);
                default:
                    return null;
            }
        }

        public override bool ShowMember(IEntity member)
        {
            var assembly = (PEFile)member.ParentModule?.MetadataFile;
            return showAllMembers || !CSharpDecompiler.MemberIsHidden(assembly, member.MetadataToken, new DecompilationOptions().DecompilerSettings);
        }

        public override string GetTooltip(IEntity entity)
        {
            var flags = ConversionFlags.All & ~(ConversionFlags.ShowBody | ConversionFlags.PlaceReturnTypeAfterParameterList);
            return new CSharpAmbience() { ConversionFlags = flags }.ConvertSymbol(entity);
        }

        public override CodeMappingInfo GetCodeMappingInfo(PEFile module, EntityHandle member)
        {
            return CSharpDecompiler.GetCodeMappingInfo(module, member);
        }

        CSharpBracketSearcher bracketSearcher = new CSharpBracketSearcher();

        public override IBracketSearcher BracketSearcher => bracketSearcher;

    }
}
