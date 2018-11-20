// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    public sealed class EnumBuilder : TypeInfo
    {
        internal TypeBuilder _typeBuilder;
        private FieldBuilder _underlyingField;

        public override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            return typeInfo != null && IsAssignableFrom(typeInfo.AsType());
        }

        public FieldBuilder DefineLiteral(string literalName, object literalValue)
        {
            // Define the underlying field for the enum. It will be a non-static, private field with special name bit set. 
            FieldBuilder fieldBuilder = _typeBuilder.DefineField(
                literalName,
                this,
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal);
            fieldBuilder.SetConstant(literalValue);
            return fieldBuilder;
        }

        public TypeInfo CreateTypeInfo() => _typeBuilder.CreateTypeInfo();

        public Type CreateType() => _typeBuilder.CreateType();

        // Get the internal metadata token for this class.
        public TypeToken TypeToken => _typeBuilder.TypeToken;

        public FieldBuilder UnderlyingField => _underlyingField;

        public override string Name => _typeBuilder.Name;

        public override Guid GUID => _typeBuilder.GUID;

        public override object InvokeMember(
            string name,
            BindingFlags invokeAttr,
            Binder binder,
            object target,
            object[] args,
            ParameterModifier[] modifiers,
            CultureInfo culture,
            string[] namedParameters)
        {
            return _typeBuilder.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
        }

        public override Module Module => _typeBuilder.Module;

        public override Assembly Assembly => _typeBuilder.Assembly;

        public override RuntimeTypeHandle TypeHandle => _typeBuilder.TypeHandle;

        public override string FullName => _typeBuilder.FullName;

        public override string AssemblyQualifiedName => _typeBuilder.AssemblyQualifiedName;

        public override string Namespace => _typeBuilder.Namespace;

        public override Type BaseType => _typeBuilder.BaseType;

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder,
                CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            return _typeBuilder.GetConstructor(bindingAttr, binder, callConvention,
                            types, modifiers);
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetConstructors(bindingAttr);
        }

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder,
                CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            if (types == null)
            {
                return _typeBuilder.GetMethod(name, bindingAttr);
            }
            
            return _typeBuilder.GetMethod(name, bindingAttr, binder, callConvention, types, modifiers);
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetMethods(bindingAttr);
        }

        public override FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            return _typeBuilder.GetField(name, bindingAttr);
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetFields(bindingAttr);
        }

        public override Type GetInterface(string name, bool ignoreCase)
        {
            return _typeBuilder.GetInterface(name, ignoreCase);
        }

        public override Type[] GetInterfaces()
        {
            return _typeBuilder.GetInterfaces();
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
        {
            return _typeBuilder.GetEvent(name, bindingAttr);
        }

        public override EventInfo[] GetEvents()
        {
            return _typeBuilder.GetEvents();
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder,
                Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetProperties(bindingAttr);
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetNestedTypes(bindingAttr);
        }

        public override Type GetNestedType(string name, BindingFlags bindingAttr)
        {
            return _typeBuilder.GetNestedType(name, bindingAttr);
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            return _typeBuilder.GetMember(name, type, bindingAttr);
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetMembers(bindingAttr);
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            return _typeBuilder.GetInterfaceMap(interfaceType);
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            return _typeBuilder.GetEvents(bindingAttr);
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            return _typeBuilder.Attributes;
        }

        public override bool IsTypeDefinition => true;

        public override bool IsSZArray => false;

        protected override bool IsArrayImpl() => false;

        protected override bool IsPrimitiveImpl() => false;

        protected override bool IsValueTypeImpl() => true;

        protected override bool IsByRefImpl() => false;

        protected override bool IsPointerImpl() => false;

        protected override bool IsCOMObjectImpl() => false;

        public override bool IsConstructedGenericType => false;

        public override Type GetElementType() => _typeBuilder.GetElementType();

        protected override bool HasElementTypeImpl() => _typeBuilder.HasElementType;

        public override Type GetEnumUnderlyingType() => _underlyingField.FieldType;

        public override Type UnderlyingSystemType => GetEnumUnderlyingType();

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _typeBuilder.GetCustomAttributes(inherit);
        }
    
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _typeBuilder.GetCustomAttributes(attributeType, inherit);
        }
        
        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
            _typeBuilder.SetCustomAttribute(con, binaryAttribute);
        }
        
        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            _typeBuilder.SetCustomAttribute(customBuilder);
        }

        public override Type DeclaringType => _typeBuilder.DeclaringType;

        public override Type ReflectedType => _typeBuilder.ReflectedType;

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _typeBuilder.IsDefined(attributeType, inherit);
        }

        public override Type MakePointerType() => SymbolType.FormCompoundType("*", this, 0);

        public override Type MakeByRefType() => SymbolType.FormCompoundType("&", this, 0);

        public override Type MakeArrayType() => SymbolType.FormCompoundType("[]", this, 0);

        public override Type MakeArrayType(int rank)
        {
            if (rank <= 0)
            {
                throw new IndexOutOfRangeException();
            }

            string szrank = "";
            if (rank == 1)
            {
                szrank = "*";
            }
            else
            {
                for (int i = 1; i < rank; i++)
                {
                    szrank += ",";
                }
            }

            string s = string.Format(CultureInfo.InvariantCulture, "[{0}]", szrank); // [,,]
            return SymbolType.FormCompoundType(s, this, 0);
        }


        // Constructs a EnumBuilder.
        // EnumBuilder can only be a top-level (not nested) enum type.
        internal EnumBuilder(
            string name,                       // name of type
            Type underlyingType,             // underlying type for an Enum
            TypeAttributes visibility,              // any bits on TypeAttributes.VisibilityMask)
            ModuleBuilder module)                     // module containing this type
        {
            // Client should not set any bits other than the visibility bits.
            if ((visibility & ~TypeAttributes.VisibilityMask) != 0)
            {
                throw new ArgumentException(SR.Argument_ShouldOnlySetVisibilityFlags, nameof(name));
            }

            _typeBuilder = new TypeBuilder(name, visibility | TypeAttributes.Sealed, typeof(Enum), null, module, PackingSize.Unspecified, TypeBuilder.UnspecifiedTypeSize, null);

            // Define the underlying field for the enum. It will be a non-static, private field with special name bit set. 
            _underlyingField = _typeBuilder.DefineField("value__", underlyingType, FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName);
        }
    }
}
