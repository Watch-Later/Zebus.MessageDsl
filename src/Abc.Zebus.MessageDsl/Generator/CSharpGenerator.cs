﻿using System;
using System.Linq;
using System.Text;
using Abc.Zebus.MessageDsl.Ast;

namespace Abc.Zebus.MessageDsl.Generator
{
    public sealed class CSharpGenerator : GeneratorBase
    {
        private static readonly AttributeDefinition _attrProtoContract = new AttributeDefinition("ProtoContract");
        private static readonly AttributeDefinition _attrNonUserCode = new AttributeDefinition("System.Diagnostics.DebuggerNonUserCode");
        private static readonly AttributeDefinition _attrGeneratedCode = new AttributeDefinition("System.CodeDom.Compiler.GeneratedCode", $@"""{GeneratorName}"", ""{GeneratorVersion}""");
        private static readonly TypeName _protoMemberType = new TypeName("ProtoMember");
        private static readonly TypeName _attrObsoleteType = new TypeName("Obsolete");

        private ParsedContracts Contracts { get; }

        private CSharpGenerator(ParsedContracts contracts)
        {
            Contracts = contracts;
        }

        public static string Generate(ParsedContracts contracts)
        {
            using (var generator = new CSharpGenerator(contracts))
            {
                return generator.Generate();
            }
        }

        private string Generate()
        {
            Reset();

            WriteHeader();
            WriteUsings();
            WritePragmas();

            var hasNamespace = !string.IsNullOrEmpty(Contracts.Namespace);

            if (hasNamespace)
                Writer.WriteLine("namespace {0}", Identifier(Contracts.Namespace));

            using (hasNamespace ? Block() : null)
            {
                var firstMember = true;

                foreach (var enumDef in Contracts.Enums)
                {
                    if (!firstMember)
                        Writer.WriteLine();

                    WriteEnum(enumDef);
                    firstMember = false;
                }

                foreach (var message in Contracts.Messages)
                {
                    if (!firstMember)
                        Writer.WriteLine();

                    WriteMessage(message);
                    firstMember = false;
                }
            }

            return GeneratedOutput();
        }

        private void WriteHeader()
        {
            Writer.WriteLine("//------------------------------------------------------------------------------");
            Writer.WriteLine("// <auto-generated>");
            Writer.WriteLine("//     This code was generated by a tool.");
            Writer.WriteLine("// </auto-generated>");
            Writer.WriteLine("//------------------------------------------------------------------------------");
            Writer.WriteLine();
        }

        private void WriteUsings()
        {
            var orderedNamespaces = Contracts.ImportedNamespaces
                                             .OrderByDescending(ns => ns == "System" || ns.StartsWith("System."))
                                             .ThenBy(ns => ns, StringComparer.OrdinalIgnoreCase);

            foreach (var ns in orderedNamespaces)
                Writer.WriteLine("using {0};", Identifier(ns));

            Writer.WriteLine();
        }

        private void WritePragmas()
        {
            var hasObsolete = Contracts.Messages.Any(m => m.Attributes.HasAttribute(_attrObsoleteType))
                              || Contracts.Messages.SelectMany(m => m.Parameters).Any(p => p.Attributes.HasAttribute(_attrObsoleteType))
                              || Contracts.Enums.Any(m => m.Attributes.HasAttribute(_attrObsoleteType))
                              || Contracts.Enums.SelectMany(m => m.Members).Any(m => m.Attributes.HasAttribute(_attrObsoleteType));

            if (hasObsolete)
            {
                Writer.WriteLine("#pragma warning disable 612");
                Writer.WriteLine("");
            }
        }

        private void WriteEnum(EnumDefinition enumDef)
        {
            if (!enumDef.Attributes.HasAttribute(_attrProtoContract.TypeName))
                WriteAttributeLine(_attrProtoContract);

            WriteAttributeLine(_attrGeneratedCode);

            foreach (var attribute in enumDef.Attributes)
                WriteAttributeLine(attribute);

            Writer.Write("public enum {0}", Identifier(enumDef.Name));

            if (enumDef.UnderlyingType.NetType != "int")
                Writer.Write(" : {0}", enumDef.UnderlyingType.NetType);

            Writer.WriteLine();

            using (Block())
            {
                var hasAnyAttributeOnMembers = enumDef.Members.Any(m => m.Attributes.Count > 0);
                var lastMember = enumDef.Members.LastOrDefault();

                foreach (var member in enumDef.Members)
                {
                    foreach (var attribute in member.Attributes)
                        WriteAttributeLine(attribute);

                    Writer.Write(Identifier(member.Name));

                    if (!string.IsNullOrEmpty(member.Value))
                        Writer.Write(" = {0}", member.Value);

                    if (member != lastMember)
                    {
                        Writer.Write(",");

                        if (hasAnyAttributeOnMembers)
                            Writer.WriteLine();
                    }

                    Writer.WriteLine();
                }
            }
        }

        private void WriteMessage(MessageDefinition message)
        {
            if (!message.Attributes.HasAttribute(_attrProtoContract.TypeName))
                WriteAttributeLine(_attrProtoContract);

            WriteAttributeLine(_attrNonUserCode);
            WriteAttributeLine(_attrGeneratedCode);

            foreach (var attribute in message.Attributes)
                WriteAttributeLine(attribute);

            Writer.Write("public sealed partial class {0}", Identifier(message.Name));

            if (message.GenericParameters.Count > 0)
            {
                var firstTemplateParam = true;
                foreach (var templateParameter in message.GenericParameters)
                {
                    Writer.Write(firstTemplateParam ? "<" : ", ");
                    Writer.Write(Identifier(templateParameter));
                    firstTemplateParam = false;
                }

                Writer.Write(">");
            }

            var interfaces = message.Interfaces
                                    .Distinct()
                                    .OrderBy(i => i.NetType, StringComparer.OrdinalIgnoreCase);

            var firstInterface = true;
            foreach (var iface in interfaces)
            {
                Writer.Write(firstInterface ? " : " : ", ");
                Writer.Write(iface.NetType);
                firstInterface = false;
            }

            Writer.WriteLine();

            WriteGenericConstraints(message);

            using (Block())
            {
                if (message.Parameters.Count > 0)
                {
                    foreach (var param in message.Parameters)
                        WriteParameterMember(message, param);

                    if (HasConstructorParameters(message))
                    {
                        WriteDefaultConstructor(message);
                        WriteMessageConstructor(message);
                    }
                }
            }
        }

        private void WriteGenericConstraints(MessageDefinition message)
        {
            if (message.GenericConstraints.Count == 0)
                return;

            using (Indent())
            {
                foreach (var genericConstraint in message.GenericConstraints)
                {
                    Writer.Write("where ");
                    Writer.Write(Identifier(genericConstraint.GenericParameterName));
                    Writer.Write(" : ");

                    var firstConstraint = false;

                    if (genericConstraint.IsClass)
                        Writer.Write("class");
                    else if (genericConstraint.IsStruct)
                        Writer.Write("struct");
                    else
                        firstConstraint = true;

                    foreach (var type in genericConstraint.Types)
                    {
                        if (!firstConstraint)
                            Writer.Write(", ");

                        Writer.Write(type.NetType);
                        firstConstraint = false;
                    }

                    if (genericConstraint.HasDefaultConstructor && !genericConstraint.IsStruct)
                    {
                        if (!firstConstraint)
                            Writer.Write(", ");

                        Writer.Write("new()");
                    }

                    Writer.WriteLine();
                }
            }
        }

        private void WriteParameterMember(MessageDefinition message, ParameterDefinition param)
        {
            if (!param.Attributes.HasAttribute(_protoMemberType))
            {
                var protoMemberParams = new StringBuilder();

                protoMemberParams.Append(param.Tag);
                protoMemberParams.AppendFormat(", IsRequired = {0}", param.Rules == FieldRules.Required ? "true" : "false");

                if (param.IsPacked)
                    protoMemberParams.Append(", IsPacked = true");

                WriteAttributeLine(new AttributeDefinition(_protoMemberType, protoMemberParams.ToString()));
            }

            foreach (var attribute in param.Attributes)
                WriteAttributeLine(attribute);

            Writer.Write("public ");

            var isWritable = param.IsWritableProperty || message.Options.Mutable;
            var isProperty = isWritable || message.Options.Prop;

            if (!isProperty)
                Writer.Write("readonly ");

            Writer.Write("{0} {1}", param.Type.NetType, Identifier(MemberCase(param.Name)));

            Writer.WriteLine(isProperty
                                 ? isWritable
                                     ? " { get; set; }"
                                     : " { get; private set; }"
                                 : ";");

            Writer.WriteLine();
        }

        private static bool HasConstructorParameters(MessageDefinition message)
            => message.Parameters.Any(param => !param.IsWritableProperty);

        private void WriteDefaultConstructor(MessageDefinition message)
        {
            Writer.Write("{0} {1}()", message.Options.Mutable ? "public" : "private", Identifier(message.Name));

            var repeatedParams = message.Parameters
                                        .Where(param => param.Type.IsRepeated)
                                        .ToList();

            if (repeatedParams.Count == 0)
            {
                Writer.WriteLine(" { }");
                return;
            }

            Writer.WriteLine();
            using (Block())
            {
                foreach (var param in repeatedParams)
                {
                    if (param.Type.IsArray)
                        Writer.WriteLine("{0} = Array.Empty<{1}>();", Identifier(MemberCase(param.Name)), param.Type.GetRepeatedItemType().NetType);
                    else if (param.Type.IsList)
                        Writer.WriteLine("{0} = new {1}();", Identifier(MemberCase(param.Name)), param.Type);
                }
            }
        }

        private void WriteMessageConstructor(MessageDefinition message)
        {
            var paramsToInitialize = message.Parameters
                                            .Where(param => !param.IsWritableProperty)
                                            .ToList();

            Writer.WriteLine();
            Writer.Write("public {0}(", Identifier(message.Name));

            var firstParam = true;
            foreach (var param in paramsToInitialize)
            {
                if (firstParam)
                    firstParam = false;
                else
                    Writer.Write(", ");

                Writer.Write("{0} {1}", param.Type.NetType, Identifier(ParameterCase(param.Name)));

                if (!string.IsNullOrEmpty(param.DefaultValue))
                    Writer.Write(" = {0}", param.DefaultValue);
            }

            Writer.WriteLine(")");

            using (Block())
            {
                foreach (var param in paramsToInitialize)
                    Writer.WriteLine("{0} = {1};", Identifier(MemberCase(param.Name)), Identifier(ParameterCase(param.Name)));
            }
        }

        private void WriteAttributeLine(AttributeDefinition attribute)
        {
            Writer.Write("[");
            WriteAttribute(attribute);
            Writer.WriteLine("]");
        }

        private void WriteAttribute(AttributeDefinition attribute)
        {
            Writer.Write(Identifier(attribute.TypeName.NetType));

            if (!string.IsNullOrEmpty(attribute.Parameters))
                Writer.Write("({0})", attribute.Parameters);
        }

        private static string Identifier(string id) => CSharpSyntax.Identifier(id);
    }
}
