﻿using System.Collections.Generic;
using System.ComponentModel;
using Abc.Zebus.MessageDsl.Ast;

namespace Abc.Zebus.MessageDsl.Analysis
{
    internal class AstProcessor
    {
        private readonly ParsedContracts _contracts;

        public AstProcessor(ParsedContracts contracts)
        {
            _contracts = contracts;
        }

        public void PreProcess()
        {
            _contracts.ImportedNamespaces.Add("System");
            _contracts.ImportedNamespaces.Add("ProtoBuf");
            _contracts.ImportedNamespaces.Add("Abc.Zebus");
        }

        public void PostProcess()
        {
            foreach (var message in _contracts.Messages)
            {
                ResolveTags(message);
                AddInterfaces(message);
                AddImplicitNamespaces(message);
            }

            foreach (var enumDef in _contracts.Enums)
            {
                ResolveEnumValues(enumDef);
                AddImplicitNamespaces(enumDef.Attributes);

                foreach (var memberDef in enumDef.Members)
                    AddImplicitNamespaces(memberDef.Attributes);
            }
        }

        private static void AddInterfaces(MessageDefinition message)
        {
            switch (message.Type)
            {
                case MessageType.Event:
                    message.Interfaces.Add("IEvent");
                    break;

                case MessageType.Command:
                    message.Interfaces.Add("ICommand");
                    break;

                case MessageType.Custom:
                    message.Interfaces.Add("IMessage");
                    break;
            }
        }

        private static void ResolveTags(MessageDefinition message)
        {
            var nextTag = AstValidator.ProtoMinTag;

            foreach (var param in message.Parameters)
            {
                if (param.Tag == 0)
                    param.Tag = nextTag;

                nextTag = param.Tag + 1;

                if (nextTag >= AstValidator.ProtoFirstReservedTag && nextTag <= AstValidator.ProtoLastReservedTag)
                    nextTag = AstValidator.ProtoLastReservedTag + 1;
            }
        }

        private static void ResolveEnumValues(EnumDefinition enumDef)
        {
            if (!enumDef.Options.Proto)
                return;

            if (enumDef.UnderlyingType.NetType != "int")
                return;

            var nextValue = (int?)0;

            foreach (var member in enumDef.Members)
            {
                member.ProtoValue = string.IsNullOrEmpty(member.Value)
                    ? nextValue
                    : enumDef.GetValidUnderlyingValue(member.Value) as int?;

                nextValue = member.ProtoValue + 1;
            }
        }

        private void AddImplicitNamespaces(MessageDefinition message)
        {
            AddImplicitNamespaces(message.Attributes);

            foreach (var paramDef in message.Parameters)
            {
                AddImplicitNamespaces(paramDef.Attributes);

                if (paramDef.Type.IsList)
                    Add(_contracts.ImportedNamespaces, typeof(List<>).Namespace);
                if (paramDef.Type.IsDictionary)
                    Add(_contracts.ImportedNamespaces, typeof(Dictionary<,>).Namespace);
                if (paramDef.Type.IsHashSet)
                    Add(_contracts.ImportedNamespaces, typeof(HashSet<>).Namespace);
            }
        }

        private void AddImplicitNamespaces(AttributeSet attributes)
        {
            if (attributes.HasAttribute(nameof(DescriptionAttribute)))
                Add(_contracts.ImportedNamespaces, typeof(DescriptionAttribute).Namespace);
        }

        private void Add<T>(ICollection<T> collection, T element)
        {
            if (collection.Contains(element))
                return;
            collection.Add(element);
        }
    }
}
