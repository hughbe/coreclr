// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    public sealed class FieldBuilder : FieldInfo
    {
        private TypeBuilder _typeBuilder;
        private readonly string _name;
        private Type _type;
        private readonly FieldAttributes _attributes;
        private FieldToken _fieldToken;

        internal FieldBuilder(TypeBuilder typeBuilder, string fieldName, Type type,
            Type[] requiredCustomModifiers, Type[] optionalCustomModifiers, FieldAttributes attributes)
        {
            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }
            if (fieldName.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(fieldName));
            }
            if (fieldName[0] == '\0')
            {
                throw new ArgumentException(SR.Argument_IllegalName, nameof(fieldName));
            }
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (type == typeof(void))
            {
                throw new ArgumentException(SR.Argument_BadFieldType);
            }

            _name = fieldName;
            _typeBuilder = typeBuilder;
            _type = type;
            _attributes = attributes & ~FieldAttributes.ReservedMask;

            SignatureHelper sigHelp = SignatureHelper.GetFieldSigHelper(_typeBuilder.Module);
            sigHelp.AddArgument(type, requiredCustomModifiers, optionalCustomModifiers);

            byte[] signature = sigHelp.InternalGetSignature(out int sigLength);

            int token = TypeBuilder.DefineField(_typeBuilder.GetModuleBuilder().GetNativeHandle(),
                typeBuilder.TypeToken.Token, fieldName, signature, sigLength, _attributes);
            _fieldToken = new FieldToken(token, type);
        }

        internal void SetData(byte[] data, int size)
        {
            ModuleBuilder.SetFieldRVAContent(_typeBuilder.GetModuleBuilder().GetNativeHandle(), _fieldToken.Token, data, size);
        }

        internal int MetadataTokenInternal => _fieldToken.Token;

        public override Module Module => _typeBuilder.Module;

        public override string Name => _name;

        public override Type DeclaringType => _typeBuilder._isHiddenGlobalType ? null : _typeBuilder;

        public override Type ReflectedType => _typeBuilder._isHiddenGlobalType ? null : _typeBuilder;

        public override Type FieldType => _type;

        public override object GetValue(object obj)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override void SetValue(object obj, object val, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override RuntimeFieldHandle FieldHandle
        {
            get => throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override FieldAttributes Attributes => _attributes;

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public FieldToken GetToken() => _fieldToken;

        public void SetOffset(int iOffset)
        {
            _typeBuilder.ThrowIfCreated();

            TypeBuilder.SetFieldLayoutOffset(_typeBuilder.GetModuleBuilder().GetNativeHandle(), GetToken().Token, iOffset);
        }

        public void SetConstant(object defaultValue)
        {
            _typeBuilder.ThrowIfCreated();

            if (defaultValue == null && _type.IsValueType)
            {
                // nullable types can hold null value.
                if (!(_type.IsGenericType && _type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    throw new ArgumentException(SR.Argument_ConstantNull);
                }
            }

            TypeBuilder.SetConstantValue(_typeBuilder.GetModuleBuilder(), GetToken().Token, _type, defaultValue);
        }


        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
            if (con == null)
            {
                throw new ArgumentNullException(nameof(con));
            }
            if (binaryAttribute == null)
            {
                throw new ArgumentNullException(nameof(binaryAttribute));
            }

            _typeBuilder.ThrowIfCreated();

            ModuleBuilder module = _typeBuilder.Module as ModuleBuilder;
            TypeBuilder.DefineCustomAttribute(module,
                _fieldToken.Token, module.GetConstructorToken(con).Token, binaryAttribute, false, false);
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            if (customBuilder == null)
            {
                throw new ArgumentNullException(nameof(customBuilder));
            }

            _typeBuilder.ThrowIfCreated();

            ModuleBuilder module = _typeBuilder.Module as ModuleBuilder;
            customBuilder.CreateCustomAttribute(module, _fieldToken.Token);
        }
    }
}
