// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    internal enum TypeKind
    {
        IsArray = 1,
        IsPointer = 2,
        IsByRef = 3,
    }

    /// <summary>
    /// This is a kind of Type object that will represent the compound expression of a
    /// parameter type or field type.
    /// </summary>
    internal sealed class SymbolType : TypeInfo
    {
        internal static Type FormCompoundType(string format, Type baseType, int curIndex)
        {
            // This function takes a string to describe the compound type, such as "[,][]", and a baseType.
            // 
            // Example: [2..4]  - one dimension array with lower bound 2 and size of 3
            // Example: [3, 5, 6] - three dimension array with lower bound 3, 5, 6
            // Example: [-3, ] [] - one dimensional array of two dimensional array (with lower bound -3 for 
            //          the first dimension)
            // Example: []* - pointer to a one dimensional array
            // Example: *[] - one dimensional array. The element type is a pointer to the baseType
            // Example: []& - ByRef of a single dimensional array. Only one & is allowed and it must appear the last!
            // Example: [?] - Array with unknown bound
            if (format == null || curIndex == format.Length)
            {
                // we have consumed all of the format string
                return baseType;
            }

            if (format[curIndex] == '&')
            {
                // ByRef case
                var symbolType = new SymbolType(TypeKind.IsByRef);
                symbolType.SetFormat(format, curIndex, 1);
                curIndex++;

                if (curIndex != format.Length)
                    // ByRef has to be the last char!!
                    throw new ArgumentException(SR.Argument_BadSigFormat);

                symbolType.SetElementType(baseType);
                return symbolType;
            }

            if (format[curIndex] == '[')
            {
                // Array type.
                var symbolType = new SymbolType(TypeKind.IsArray);
                int startIndex = curIndex;
                curIndex++;

                int lowerBound = 0;
                int upperBound = -1;

                // Example: [2..4]  - one dimension array with lower bound 2 and size of 3
                // Example: [3, 5, 6] - three dimension array with lower bound 3, 5, 6
                // Example: [-3, ] [] - one dimensional array of two dimensional array (with lower bound -3 sepcified)

                while (format[curIndex] != ']')
                {
                    if (format[curIndex] == '*')
                    {
                        symbolType._isSzArray = false;
                        curIndex++;
                    }
                    // consume, one dimension at a time
                    if ((format[curIndex] >= '0' && format[curIndex] <= '9') || format[curIndex] == '-')
                    {
                        bool isNegative = false;
                        if (format[curIndex] == '-')
                        {
                            isNegative = true;
                            curIndex++;
                        }

                        // lower bound is specified. Consume the low bound
                        while (format[curIndex] >= '0' && format[curIndex] <= '9')
                        {
                            lowerBound = lowerBound * 10;
                            lowerBound += format[curIndex] - '0';
                            curIndex++;
                        }

                        if (isNegative)
                        {
                            lowerBound = 0 - lowerBound;
                        }

                        // set the upper bound to be less than LowerBound to indicate that upper bound it not specified yet!
                        upperBound = lowerBound - 1;
                    }
                    if (format[curIndex] == '.')
                    {
                        // upper bound is specified

                        // skip over ".."
                        curIndex++;
                        if (format[curIndex] != '.')
                        {
                            // bad format!! Throw exception
                            throw new ArgumentException(SR.Argument_BadSigFormat);
                        }

                        curIndex++;
                        // consume the upper bound
                        if ((format[curIndex] >= '0' && format[curIndex] <= '9') || format[curIndex] == '-')
                        {
                            bool isNegative = false;
                            upperBound = 0;
                            if (format[curIndex] == '-')
                            {
                                isNegative = true;
                                curIndex++;
                            }

                            // lower bound is specified. Consume the low bound
                            while (format[curIndex] >= '0' && format[curIndex] <= '9')
                            {
                                upperBound = upperBound * 10;
                                upperBound += format[curIndex] - '0';
                                curIndex++;
                            }
                            if (isNegative)
                            {
                                upperBound = 0 - upperBound;
                            }
                            if (upperBound < lowerBound)
                            {
                                // User specified upper bound less than lower bound, this is an error.
                                // Throw error exception.
                                throw new ArgumentException(SR.Argument_BadSigFormat);
                            }
                        }
                    }

                    if (format[curIndex] == ',')
                    {
                        // We have more dimension to deal with.
                        // now set the lower bound, the size, and increase the dimension count!
                        curIndex++;
                        symbolType.SetBounds(lowerBound, upperBound);

                        // clear the lower and upper bound information for next dimension
                        lowerBound = 0;
                        upperBound = -1;
                    }
                    else if (format[curIndex] != ']')
                    {
                        throw new ArgumentException(SR.Argument_BadSigFormat);
                    }
                }

                // The last dimension information
                symbolType.SetBounds(lowerBound, upperBound);

                // skip over ']'
                curIndex++;

                symbolType.SetFormat(format, startIndex, curIndex - startIndex);

                // set the base type of array
                symbolType.SetElementType(baseType);
                return FormCompoundType(format, symbolType, curIndex);
            }
            else if (format[curIndex] == '*')
            {
                // pointer type.
                var symbolType = new SymbolType(TypeKind.IsPointer);
                symbolType.SetFormat(format, curIndex, 1);
                curIndex++;
                symbolType.SetElementType(baseType);
                return FormCompoundType(format, symbolType, curIndex);
            }

            return null;
        }

        private readonly TypeKind _typeKind;
        internal Type _baseType;
        internal int _rank;        // count of dimension
        // If LowerBound and UpperBound is equal, that means one element. 
        // If UpperBound is less than LowerBound, then the size is not specified.
        internal int[] _lowerBounds;
        internal int[] _upperBounds; // count of dimension
        private string _format;      // format string to form the full name.
        private bool _isSzArray = true;

        internal SymbolType(TypeKind typeKind)
        {
            _typeKind = typeKind;
            _lowerBounds = new int[4];
            _upperBounds = new int[4];
        }

        internal void SetElementType(Type baseType)
        {
            _baseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
        }

        private void SetBounds(int lower, int upper)
        {
            // Increase the rank, set lower and upper bound
            if (lower != 0 || upper != -1)
            {
                _isSzArray = false;
            }

            if (_lowerBounds.Length <= _rank)
            {
                // resize the bound array
                int[] lowerBounds = new int[_rank * 2];
                Array.Copy(_lowerBounds, 0, lowerBounds, 0, _rank);
                _lowerBounds = lowerBounds;
                Array.Copy(_upperBounds, 0, lowerBounds, 0, _rank);
                _upperBounds = lowerBounds;
            }

            _lowerBounds[_rank] = lower;
            _upperBounds[_rank] = upper;
            _rank++;
        }

        internal void SetFormat(string format, int curIndex, int length)
        {
            // Cache the text display format for this SymbolType
            _format = format.Substring(curIndex, length);
        }

        public override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            return typeInfo != null && IsAssignableFrom(typeInfo.AsType());
        }

        public override bool IsTypeDefinition => false;

        public override bool IsSZArray => _rank <= 1 && _isSzArray;

        public override Type MakePointerType() => FormCompoundType(_format + "*", _baseType, 0);

        public override Type MakeByRefType() => FormCompoundType(_format + "&", _baseType, 0);

        public override Type MakeArrayType() => FormCompoundType(_format + "[]", _baseType, 0);

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
            return FormCompoundType(_format + s, _baseType, 0) as SymbolType;
        }

        public override int GetArrayRank()
        {
            if (!IsArray)
            {
                throw new NotSupportedException(SR.NotSupported_SubclassOverride);
            }

            return _rank;
        }

        public override Guid GUID => throw new NotSupportedException(SR.NotSupported_NonReflectedType);

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target,
            object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override Module Module
        {
            get
            {
                Type baseType;

                for (baseType = _baseType; baseType is SymbolType; baseType = ((SymbolType)baseType)._baseType) ;

                return baseType.Module;
            }
        }

        public override Assembly Assembly
        {
            get
            {
                Type baseType;

                for (baseType = _baseType; baseType is SymbolType; baseType = ((SymbolType)baseType)._baseType) ;

                return baseType.Assembly;
            }
        }

        public override RuntimeTypeHandle TypeHandle => throw new NotSupportedException(SR.NotSupported_NonReflectedType);

        public override string Name
        {
            get
            {
                Type baseType;
                string sFormat = _format;

                for (baseType = _baseType; baseType is SymbolType; baseType = ((SymbolType)baseType)._baseType)
                {
                    sFormat = ((SymbolType)baseType)._format + sFormat;
                }

                return baseType.Name + sFormat;
            }
        }

        public override string FullName => TypeNameBuilder.ToString(this, TypeNameBuilder.Format.FullName);

        public override string AssemblyQualifiedName => TypeNameBuilder.ToString(this, TypeNameBuilder.Format.AssemblyQualifiedName);

        public override string ToString() => TypeNameBuilder.ToString(this, TypeNameBuilder.Format.ToString);

        public override string Namespace => _baseType.Namespace;

        public override Type BaseType => typeof(Array);

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder,
                CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder,
                CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override Type GetInterface(string name, bool ignoreCase)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override Type[] GetInterfaces()
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override EventInfo[] GetEvents()
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder,
                Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override Type GetNestedType(string name, BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            // Return the attribute flags of the base type?
            Type baseType;
            for (baseType = _baseType; baseType is SymbolType; baseType = ((SymbolType)baseType)._baseType) ;
            return baseType.Attributes;
        }

        protected override bool IsArrayImpl() => _typeKind == TypeKind.IsArray;

        protected override bool IsPointerImpl() => _typeKind == TypeKind.IsPointer;

        protected override bool IsByRefImpl() => _typeKind == TypeKind.IsByRef;

        protected override bool IsPrimitiveImpl() => false;

        protected override bool IsValueTypeImpl() => false;

        protected override bool IsCOMObjectImpl() => false;

        public override bool IsConstructedGenericType => false;

        public override Type GetElementType() => _baseType;

        protected override bool HasElementTypeImpl() => _baseType != null;

        public override Type UnderlyingSystemType => this;

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_NonReflectedType);
        }
    }
}
