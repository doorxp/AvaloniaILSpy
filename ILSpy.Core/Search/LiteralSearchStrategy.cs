﻿using System;
using ICSharpCode.Decompiler.Util;
using ICSharpCode.Decompiler.Disassembler;
using SRM = System.Reflection.Metadata;
using ILOpCode = System.Reflection.Metadata.ILOpCode;
using ICSharpCode.Decompiler;

using static System.Reflection.Metadata.PEReaderExtensions;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Metadata;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Collections.Concurrent;

namespace ICSharpCode.ILSpy.Search
{
    class LiteralSearchStrategy : AbstractSearchStrategy
    {
        readonly TypeCode searchTermLiteralType;
        readonly object searchTermLiteralValue;

        public LiteralSearchStrategy(Language language, ApiVisibility apiVisibility, IProducerConsumerCollection<SearchResult> resultQueue, params string[] terms)
            : base(language, apiVisibility, resultQueue, terms)
        {
            if (terms.Length != 1) return;
            var lexer = new Lexer(new LATextReader(new System.IO.StringReader(terms[0])));
            var value = lexer.NextToken();

            if (value?.LiteralValue == null) return;
            var valueType = Type.GetTypeCode(value.LiteralValue.GetType());
            switch (valueType)
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    searchTermLiteralType = TypeCode.Int64;
                    searchTermLiteralValue = CSharpPrimitiveCast.Cast(TypeCode.Int64, value.LiteralValue, false);
                    break;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.String:
                    searchTermLiteralType = valueType;
                    searchTermLiteralValue = value.LiteralValue;
                    break;
                default:
                    break;
            }
        }

        public override void Search(PEFile module, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = module.Metadata;
            var typeSystem = module.GetTypeSystemOrNull();
            if (typeSystem == null) return;

            foreach (var handle in metadata.MethodDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var md = metadata.GetMethodDefinition(handle);
                if (!md.HasBody() || !MethodIsLiteralMatch(module, md)) continue;
                var method = ((MetadataModule)typeSystem.MainModule).GetDefinition(handle);
                if (!CheckVisibility(method)) continue;
                OnFoundResult(method);
            }

            foreach (var handle in metadata.FieldDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fd = metadata.GetFieldDefinition(handle);
                if (!fd.HasFlag(System.Reflection.FieldAttributes.Literal))
                    continue;
                var constantHandle = fd.GetDefaultValue();
                if (constantHandle.IsNil)
                    continue;
                var constant = metadata.GetConstant(constantHandle);
                var blob = metadata.GetBlobReader(constant.Value);
                if (!IsLiteralMatch(metadata, blob.ReadConstant(constant.TypeCode)))
                    continue;
                var field = ((MetadataModule)typeSystem.MainModule).GetDefinition(handle);
                if (!CheckVisibility(field)) continue;
                OnFoundResult(field);
            }
        }

        bool IsLiteralMatch(MetadataReader metadata, object val)
        {
            if (val == null)
                return false;
            switch (searchTermLiteralType)
            {
                case TypeCode.Int64:
                    var tc = Type.GetTypeCode(val.GetType());
                    if (tc >= TypeCode.SByte && tc <= TypeCode.UInt64)
                        return CSharpPrimitiveCast.Cast(TypeCode.Int64, val, false).Equals(searchTermLiteralValue);
                    return false;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.String:
                    return searchTermLiteralValue.Equals(val);
                default:
                    // substring search with searchTerm
                    return IsMatch(val.ToString());
            }
        }

        bool MethodIsLiteralMatch(PEFile module, MethodDefinition methodDefinition)
        {
            var blob = module.Reader.GetMethodBody(methodDefinition.RelativeVirtualAddress).GetILReader();
            var val = (long)searchTermLiteralValue;
            if (searchTermLiteralType == TypeCode.Int64)
            {
                while (blob.RemainingBytes > 0)
                {
                    ILOpCode code;
                    switch (code = blob.DecodeOpCode())
                    {
                        case ILOpCode.Ldc_i8:
                            if (val == blob.ReadInt64())
                                return true;
                            break;
                        case ILOpCode.Ldc_i4:
                            if (val == blob.ReadInt32())
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_s:
                            if (val == blob.ReadSByte())
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_m1:
                            if (val == -1)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_0:
                            if (val == 0)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_1:
                            if (val == 1)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_2:
                            if (val == 2)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_3:
                            if (val == 3)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_4:
                            if (val == 4)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_5:
                            if (val == 5)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_6:
                            if (val == 6)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_7:
                            if (val == 7)
                                return true;
                            break;
                        case ILOpCode.Ldc_i4_8:
                            if (val == 8)
                                return true;
                            break;
                        default:
                            blob.SkipOperand(code);
                            break;
                    }
                }
            }
            else if (searchTermLiteralType != TypeCode.Empty)
            {
                var expectedCode = searchTermLiteralType switch
                {
                    TypeCode.Single => ILOpCode.Ldc_r4,
                    TypeCode.Double => ILOpCode.Ldc_r8,
                    TypeCode.String => ILOpCode.Ldstr,
                    _ => throw new InvalidOperationException()
                };
                while (blob.RemainingBytes > 0)
                {
                    var code = blob.DecodeOpCode();
                    if (code != expectedCode)
                    {
                        blob.SkipOperand(code);
                        continue;
                    }
                    switch (code)
                    {
                        case ILOpCode.Ldc_r4:
                            if ((float)searchTermLiteralValue == blob.ReadSingle())
                                return true;
                            break;
                        case ILOpCode.Ldc_r8:
                            if ((double)searchTermLiteralValue == blob.ReadDouble())
                                return true;
                            break;
                        case ILOpCode.Ldstr:
                            if ((string)searchTermLiteralValue == blob.DecodeUserString(module.Metadata))
                                return true;
                            break;
                    }
                }
            }
            else
            {
                while (blob.RemainingBytes > 0)
                {
                    var code = blob.DecodeOpCode();
                    if (code != ILOpCode.Ldstr)
                    {
                        blob.SkipOperand(code);
                        continue;
                    }
                    if (IsMatch(blob.DecodeUserString(module.Metadata)))
                        return true;
                }
            }
            return false;
        }
    }
}
