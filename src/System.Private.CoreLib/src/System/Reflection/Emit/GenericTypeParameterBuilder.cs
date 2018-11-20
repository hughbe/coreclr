// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    public sealed class GenericTypeParameterBuilder : TypeInfo
    {
        internal TypeBuilder _type;

        internal GenericTypeParameterBuilder(TypeBuilder type)
        {
            _type = type;
        }

        public override string ToString() => _type.Name;

        public override bool Equals(object o)
        {
            return o is GenericTypeParameterBuilder g && ReferenceEquals(g._type, _type);
        }

        public override int GetHashCode() => _type.GetHashCode();

        public override Type DeclaringType => _type.DeclaringType;

        public override Type ReflectedType => _type.ReflectedType;

        public override string Name => _type.Name;

        public override Module Module => _type.Module;

        internal int MetadataTokenInternal => _type.MetadataTokenInternal;

        public override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            return typeInfo != null && IsAssignableFrom(typeInfo.AsType());
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
            return SymbolType.FormCompoundType(s, this, 0) as SymbolType;
        }

        public override Guid GUID => throw new NotSupportedException();

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        {
            throw new NotSupportedException();
        }

        public override Assembly Assembly => _type.Assembly;

        public override RuntimeTypeHandle TypeHandle => throw new NotSupportedException();

        public override string FullName => null;

        public override string Namespace => null;

        public override string AssemblyQualifiedName => null;

        public override Type BaseType => _type.BaseType;

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException();
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException();
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override Type GetInterface(string name, bool ignoreCase)
        {
            throw new NotSupportedException();
        }

        public override Type[] GetInterfaces()
        {
            throw new NotSupportedException();
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override EventInfo[] GetEvents()
        {
            throw new NotSupportedException();
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException();
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override Type GetNestedType(string name, BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            throw new NotSupportedException();
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            throw new NotSupportedException();
        }

        protected override TypeAttributes GetAttributeFlagsImpl() => TypeAttributes.Public;

        public override bool IsTypeDefinition => false;

        public override bool IsSZArray => false;

        protected override bool IsArrayImpl() => false;

        protected override bool IsByRefImpl() => false;

        protected override bool IsPointerImpl() => false;

        protected override bool IsPrimitiveImpl() => false;

        protected override bool IsCOMObjectImpl() => false;

        public override Type GetElementType() => throw new NotSupportedException();

        protected override bool HasElementTypeImpl() => false;

        public override Type UnderlyingSystemType => this;

        public override Type[] GetGenericArguments() => throw new InvalidOperationException();

        public override bool IsGenericTypeDefinition => false;

        public override bool IsGenericType => false;

        public override bool IsGenericParameter => true;

        public override bool IsConstructedGenericType => false;

        public override int GenericParameterPosition => _type.GenericParameterPosition;

        public override bool ContainsGenericParameters => _type.ContainsGenericParameters;

        public override GenericParameterAttributes GenericParameterAttributes => _type.GenericParameterAttributes;

        public override MethodBase DeclaringMethod => _type.DeclaringMethod;

        public override Type GetGenericTypeDefinition() => throw new InvalidOperationException();

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericTypeDefinition, this));
        }

        protected override bool IsValueTypeImpl() => false;

        public override bool IsAssignableFrom(Type c) => throw new NotSupportedException();

        public override bool IsSubclassOf(Type c) => throw new NotSupportedException();

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotSupportedException();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotSupportedException();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotSupportedException();
        }

        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
            _type.SetGenParamCustomAttribute(con, binaryAttribute);
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            _type.SetGenParamCustomAttribute(customBuilder);
        }

        public void SetBaseTypeConstraint(Type baseTypeConstraint)
        {
            _type.Module.CheckContext(baseTypeConstraint);
            _type.SetParent(baseTypeConstraint);
        }

        public void SetInterfaceConstraints(params Type[] interfaceConstraints)
        {
            _type.Module.CheckContext(interfaceConstraints);
            _type.SetInterfaces(interfaceConstraints);
        }

        public void SetGenericParameterAttributes(GenericParameterAttributes genericParameterAttributes)
        {
            _type.SetGenParamAttributes(genericParameterAttributes);
        }
    }
}

