﻿namespace Abc.Zebus.MessageDsl.Ast
{
    public class AttributeDefinition : AstNode
    {
        private TypeName _typeName;

        public TypeName TypeName
        {
            get => _typeName;
            private set => _typeName = NormalizeAttributeTypeName(value);
        }

        public string Parameters { get; set; }

        public AttributeDefinition(TypeName attributeType, string parameters = null)
        {
            TypeName = attributeType;
            Parameters = parameters;
        }

        public static TypeName NormalizeAttributeTypeName(TypeName typeName)
        {
            const string attributeSuffix = "Attribute";

            if (typeName != null && typeName.NetType.EndsWith(attributeSuffix))
                return typeName.NetType.Substring(0, typeName.NetType.Length - attributeSuffix.Length);

            return typeName;
        }

        public AttributeDefinition Clone()
        {
            return new AttributeDefinition(TypeName, Parameters)
            {
                ParseContext = ParseContext
            };
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Parameters)
                ? TypeName.ToString()
                : $"{TypeName}({Parameters})";
        }
    }
}
