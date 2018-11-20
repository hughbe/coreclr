// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Globalization;
using System.Diagnostics;

namespace System.Reflection.Emit
{
    internal sealed class TypeBuilderInstantiation : TypeInfo
    {
        internal static Type MakeGenericType(Type type, Type[] typeArguments)
        {
            Debug.Assert(type != null, "this is only called from RuntimeType.MakeGenericType and TypeBuilder.MakeGenericType so 'type' cannot be null");

            if (!type.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException();
            }

            if (typeArguments == null)
            {
                throw new ArgumentNullException(nameof(typeArguments));
            }

            foreach (Type t in typeArguments)
            {
                if (t == null)
                {
                    throw new ArgumentNullException(nameof(typeArguments));
                }
            }

            return new TypeBuilderInstantiation(type, typeArguments);
        }

        private Type _type;
        private Type[] _genericArguments;
        private string _fullName;
        internal Hashtable _hashtable = new Hashtable();

        private TypeBuilderInstantiation(Type type, Type[] inst)
        {
            _type = type;
            _genericArguments = inst;
            _hashtable = new Hashtable();
        }

        public override string ToString()
        {
            return TypeNameBuilder.ToString(this, TypeNameBuilder.Format.ToString);
        }

        public override Type DeclaringType => _type.DeclaringType;

        public override Type ReflectedType => _type.ReflectedType;

        public override string Name => _type.Name;

        public override Module Module => _type.Module;

        public override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            return typeInfo != null && IsAssignableFrom(typeInfo.AsType());
        }

        public override Type MakePointerType() => SymbolType.FormCompoundType("*", this, 0);

        public override Type MakeByRefType() => SymbolType.FormCompoundType("&", this, 0);

        public override Type MakeArrayType()
        {
            return SymbolType.FormCompoundType("[]", this, 0);
        }

        public override Type MakeArrayType(int rank)
        {
            if (rank <= 0)
            {
                throw new IndexOutOfRangeException();
            }

            string comma = "";
            for (int i = 1; i < rank; i++)
            {
                comma += ",";
            }

            string s = string.Format(CultureInfo.InvariantCulture, "[{0}]", comma);
            return SymbolType.FormCompoundType(s, this, 0);
        }

        public override Guid GUID => throw new NotSupportedException();

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        {
            throw new NotSupportedException();
        }

        public override Assembly Assembly => _type.Assembly;

        public override RuntimeTypeHandle TypeHandle => throw new NotSupportedException();

        public override string FullName
        {
            get => _fullName ?? (_fullName = TypeNameBuilder.ToString(this, TypeNameBuilder.Format.FullName));
        }

        public override string Namespace => _type.Namespace;

        public override string AssemblyQualifiedName
        {
            get => TypeNameBuilder.ToString(this, TypeNameBuilder.Format.AssemblyQualifiedName);
        }

        private Type Substitute(Type[] substitutes)
        {
            Type[] inst = GetGenericArguments();
            Type[] instSubstituted = new Type[inst.Length];

            for (int i = 0; i < instSubstituted.Length; i++)
            {
                Type t = inst[i];

                if (t is TypeBuilderInstantiation tbi)
                {
                    instSubstituted[i] = tbi.Substitute(substitutes);
                }
                else if (t is GenericTypeParameterBuilder)
                {
                    // Substitute
                    instSubstituted[i] = substitutes[t.GenericParameterPosition];
                }
                else
                {
                    instSubstituted[i] = t;
                }
            }

            return GetGenericTypeDefinition().MakeGenericType(instSubstituted);
        }

        public override Type BaseType
        {
            // B<A,B,C>
            // D<T,S> : B<S,List<T>,char>

            // D<string,int> : B<int,List<string>,char>
            // D<S,T> : B<T,List<S>,char>        
            // D<S,string> : B<string,List<S>,char>        
            get
            {
                Type typeBldrBase = _type.BaseType;
                TypeBuilderInstantiation typeBldrBaseAs = typeBldrBase as TypeBuilderInstantiation;
                if (typeBldrBaseAs == null)
                {
                    return typeBldrBase;
                }

                return typeBldrBaseAs.Substitute(GetGenericArguments());
            }
        }

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

        protected override TypeAttributes GetAttributeFlagsImpl() => _type.Attributes;

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

        public override Type[] GetGenericArguments() => _genericArguments;

        public override bool IsGenericTypeDefinition => false;

        public override bool IsGenericType => true;

        public override bool IsConstructedGenericType => true;

        public override bool IsGenericParameter => false;

        public override int GenericParameterPosition => throw new InvalidOperationException();

        protected override bool IsValueTypeImpl() => _type.IsValueType;

        public override bool ContainsGenericParameters
        {
            get
            {
                for (int i = 0; i < _genericArguments.Length; i++)
                {
                    if (_genericArguments[i].ContainsGenericParameters)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public override MethodBase DeclaringMethod => null;

        public override Type GetGenericTypeDefinition() => _type;

        public override Type MakeGenericType(params Type[] inst)
        {
            throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericTypeDefinition, this));
        }

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
    }
}
