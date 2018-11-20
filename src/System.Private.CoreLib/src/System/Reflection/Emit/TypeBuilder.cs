// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Reflection.Emit
{
    public sealed class TypeBuilder : TypeInfo
    {
        private class CustAttr
        {
            private readonly ConstructorInfo _constructor;
            private readonly byte[] _binaryAttribute;
            private CustomAttributeBuilder _customBuilder;

            public CustAttr(ConstructorInfo con, byte[] binaryAttribute)
            {
                _constructor = con ?? throw new ArgumentNullException(nameof(con));
                _binaryAttribute = binaryAttribute ?? throw new ArgumentNullException(nameof(binaryAttribute));
            }

            public CustAttr(CustomAttributeBuilder customBuilder)
            {
                _customBuilder = customBuilder ?? throw new ArgumentNullException(nameof(customBuilder));
            }

            public void Bake(ModuleBuilder module, int token)
            {
                if (_customBuilder == null)
                {
                    DefineCustomAttribute(module, token, module.GetConstructorToken(_constructor).Token,
                        _binaryAttribute, false, false);
                }
                else
                {
                    _customBuilder.CreateCustomAttribute(module, token);
                }
            }
        }

        public static MethodInfo GetMethod(Type type, MethodInfo method)
        {
            if (!(type is TypeBuilder) && !(type is TypeBuilderInstantiation))
            {
                throw new ArgumentException(SR.Argument_MustBeTypeBuilder);
            }

            // The following checks establishes invariants that more simply put require type to be generic and
            // method to be a generic method definition declared on the generic type definition of type.
            // To create generic method G<Foo>.M<Bar> these invariants require that G<Foo>.M<S> be created by calling
            // this function followed by MakeGenericMethod on the resulting MethodInfo to finally get G<Foo>.M<Bar>.
            // We could also allow G<T>.M<Bar> to be created before G<Foo>.M<Bar> (BindGenParm followed by this method)
            // if we wanted to but that just complicates things so these checks are designed to prevent that scenario.
            if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
            {
                throw new ArgumentException(SR.Argument_NeedGenericMethodDefinition, nameof(method));
            }
            if (method.DeclaringType == null || !method.DeclaringType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(SR.Argument_MethodNeedGenericDeclaringType, nameof(method));
            }
            if (type.GetGenericTypeDefinition() != method.DeclaringType)
            {
                throw new ArgumentException(SR.Argument_InvalidMethodDeclaringType, nameof(type));
            }
            // The following converts from Type or TypeBuilder of G<T> to TypeBuilderInstantiation G<T>. These types
            // both logically represent the same thing. The runtime displays a similar convention by having
            // G<M>.M() be encoded by a typeSpec whose parent is the typeDef for G<M> and whose instantiation is also G<M>.
            if (type.IsGenericTypeDefinition)
            {
                type = type.MakeGenericType(type.GetGenericArguments());
            }

            if (!(type is TypeBuilderInstantiation))
            {
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(type));
            }

            return MethodOnTypeBuilderInstantiation.GetMethod(method, type as TypeBuilderInstantiation);
        }

        public static ConstructorInfo GetConstructor(Type type, ConstructorInfo constructor)
        {
            if (!(type is TypeBuilder) && !(type is TypeBuilderInstantiation))
            {
                throw new ArgumentException(SR.Argument_MustBeTypeBuilder);
            }
            if (!constructor.DeclaringType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(SR.Argument_ConstructorNeedGenericDeclaringType, nameof(constructor));
            }
            if (!(type is TypeBuilderInstantiation))
            {
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(type));
            }
            // TypeBuilder G<T> ==> TypeBuilderInstantiation G<T>
            if (type is TypeBuilder && type.IsGenericTypeDefinition)
            {
                type = type.MakeGenericType(type.GetGenericArguments());
            }
            if (type.GetGenericTypeDefinition() != constructor.DeclaringType)
            {
                throw new ArgumentException(SR.Argument_InvalidConstructorDeclaringType, nameof(type));
            }

            return ConstructorOnTypeBuilderInstantiation.GetConstructor(constructor, type as TypeBuilderInstantiation);
        }

        public static FieldInfo GetField(Type type, FieldInfo field)
        {
            if (!(type is TypeBuilder) && !(type is TypeBuilderInstantiation))
            {
                throw new ArgumentException(SR.Argument_MustBeTypeBuilder);
            }
            if (!field.DeclaringType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(SR.Argument_FieldNeedGenericDeclaringType, nameof(field));
            }
            if (!(type is TypeBuilderInstantiation))
            {
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(type));
            }

            // TypeBuilder G<T> ==> TypeBuilderInstantiation G<T>
            if (type is TypeBuilder && type.IsGenericTypeDefinition)
            {
                type = type.MakeGenericType(type.GetGenericArguments());
            }
            if (type.GetGenericTypeDefinition() != field.DeclaringType)
            {
                throw new ArgumentException(SR.Argument_InvalidFieldDeclaringType, nameof(type));
            }

            return FieldOnTypeBuilderInstantiation.GetField(field, type as TypeBuilderInstantiation);
        }

        public const int UnspecifiedTypeSize = 0;

        #region FCalls
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern void SetParentType(RuntimeModule module, int tdTypeDef, int tkParent);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern void AddInterfaceImpl(RuntimeModule module, int tdTypeDef, int tkInterface);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int DefineMethod(RuntimeModule module, int tkParent, string name, byte[] signature, int sigLength,
            MethodAttributes attributes);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int DefineMethodSpec(RuntimeModule module, int tkParent, byte[] signature, int sigLength);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int DefineField(RuntimeModule module, int tkParent, string name, byte[] signature, int sigLength,
            FieldAttributes attributes);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern void SetMethodIL(RuntimeModule module, int tk, bool isInitLocals,
            byte[] body, int bodyLength,
            byte[] LocalSig, int sigLength,
            int maxStackSize,
            ExceptionHandler[] exceptions, int numExceptions,
            int[] tokenFixups, int numTokenFixups);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern void DefineCustomAttribute(RuntimeModule module, int tkAssociate, int tkConstructor,
            byte[] attr, int attrLength, bool toDisk, bool updateCompilerFlags);

        internal static void DefineCustomAttribute(ModuleBuilder module, int tkAssociate, int tkConstructor,
            byte[] attr, bool toDisk, bool updateCompilerFlags)
        {
            byte[] localAttr = null;

            if (attr != null)
            {
                localAttr = new byte[attr.Length];
                Buffer.BlockCopy(attr, 0, localAttr, 0, attr.Length);
            }

            DefineCustomAttribute(module.GetNativeHandle(), tkAssociate, tkConstructor,
                localAttr, (localAttr != null) ? localAttr.Length : 0, toDisk, updateCompilerFlags);
        }

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int DefineProperty(RuntimeModule module, int tkParent, string name, PropertyAttributes attributes,
            byte[] signature, int sigLength);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int DefineEvent(RuntimeModule module, int tkParent, string name, EventAttributes attributes, int tkEventType);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern void DefineMethodSemantics(RuntimeModule module, int tkAssociation,
            MethodSemanticsAttributes semantics, int tkMethod);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern void DefineMethodImpl(RuntimeModule module, int tkType, int tkBody, int tkDecl);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern void SetMethodImpl(RuntimeModule module, int tkMethod, MethodImplAttributes MethodImplAttributes);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int SetParamInfo(RuntimeModule module, int tkMethod, int iSequence,
            ParameterAttributes iParamAttributes, string strParamName);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern int GetTokenFromSig(RuntimeModule module, byte[] signature, int sigLength);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern void SetFieldLayoutOffset(RuntimeModule module, int fdToken, int iOffset);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        internal static extern void SetClassLayout(RuntimeModule module, int tk, PackingSize iPackingSize, int iTypeSize);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern unsafe void SetConstantValue(RuntimeModule module, int tk, int corType, void* pValue);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern void SetPInvokeData(RuntimeModule module, string DllName, string name, int token, int linkFlags);

        #endregion

        internal static bool IsTypeEqual(Type t1, Type t2)
        {
            // Maybe we are lucky that they are equal in the first place
            if (t1 == t2)
            {
                return true;
            }

            TypeBuilder tb1 = null;
            TypeBuilder tb2 = null;
            Type runtimeType1 = null;
            Type runtimeType2 = null;

            // set up the runtimeType and TypeBuilder type corresponding to t1 and t2
            if (t1 is TypeBuilder)
            {
                tb1 = (TypeBuilder)t1;
                // This will be null if it is not baked.
                runtimeType1 = tb1._bakedRuntimeType;
            }
            else
            {
                runtimeType1 = t1;
            }

            if (t2 is TypeBuilder)
            {
                tb2 = (TypeBuilder)t2;
                // This will be null if it is not baked.
                runtimeType2 = tb2._bakedRuntimeType;
            }
            else
            {
                runtimeType2 = t2;
            }

            // If the type builder view is equal then it is equal
            if (tb1 != null && tb2 != null && ReferenceEquals(tb1, tb2))
            {
                return true;
            }

            // If the runtimetype view is equal than it is equal
            return runtimeType1 != null && runtimeType2 != null && runtimeType1 == runtimeType2;
        }

        internal static unsafe void SetConstantValue(ModuleBuilder module, int tk, Type destType, object value)
        {
            // This is a helper function that is used by ParameterBuilder, PropertyBuilder,
            // and FieldBuilder to validate a default value and save it in the meta-data.
            if (value != null)
            {
                Type type = value.GetType();

                // We should allow setting a constant value on a ByRef parameter
                if (destType.IsByRef)
                {
                    destType = destType.GetElementType();
                }

                // Convert nullable types to their underlying type.
                // This is necessary for nullable enum types to pass the IsEnum check that's coming next.
                destType = Nullable.GetUnderlyingType(destType) ?? destType;

                if (destType.IsEnum)
                {
                    //                                   |  UnderlyingSystemType     |  Enum.GetUnderlyingType() |  IsEnum
                    // ----------------------------------|---------------------------|---------------------------|---------
                    // runtime Enum Type                 |  self                     |  underlying type of enum  |  TRUE
                    // EnumBuilder                       |  underlying type of enum  |  underlying type of enum* |  TRUE
                    // TypeBuilder of enum types**       |  underlying type of enum  |  Exception                |  TRUE
                    // TypeBuilder of enum types (baked) |  runtime enum type        |  Exception                |  TRUE

                    //  *: the behavior of Enum.GetUnderlyingType(EnumBuilder) might change in the future
                    //     so let's not depend on it.
                    // **: created with System.Enum as the parent type.

                    // The above behaviors might not be the most consistent but we have to live with them.

                    Type underlyingType;
                    if (destType is EnumBuilder enumBuilder)
                    {
                        underlyingType = enumBuilder.GetEnumUnderlyingType();

                        // The constant value supplied should match either the baked enum type or its underlying type
                        // we don't need to compare it with the EnumBuilder itself because you can never have an object of that type
                        if (type != enumBuilder._typeBuilder._bakedRuntimeType && type != underlyingType)
                        {
                            throw new ArgumentException(SR.Argument_ConstantDoesntMatch);
                        }
                    }
                    else if (destType is TypeBuilder typeBuilder)
                    {
                        underlyingType = typeBuilder._enumUnderlyingType;

                        // The constant value supplied should match either the baked enum type or its underlying type
                        // typeBldr.m_enumUnderlyingType is null if the user hasn't created a "value__" field on the enum
                        if (underlyingType == null || (type != typeBuilder.UnderlyingSystemType && type != underlyingType))
                        {
                            throw new ArgumentException(SR.Argument_ConstantDoesntMatch);
                        }
                    }
                    else // must be a runtime Enum Type
                    {
                        Debug.Assert(destType is RuntimeType, "destType is not a runtime type, an EnumBuilder, or a TypeBuilder.");

                        underlyingType = Enum.GetUnderlyingType(destType);

                        // The constant value supplied should match either the enum itself or its underlying type
                        if (type != destType && type != underlyingType)
                        {
                            throw new ArgumentException(SR.Argument_ConstantDoesntMatch);
                        }
                    }

                    type = underlyingType;
                }
                else
                {
                    // Note that it is non CLS compliant if destType != type. But RefEmit never guarantees CLS-Compliance.
                    if (!destType.IsAssignableFrom(type))
                    {
                        throw new ArgumentException(SR.Argument_ConstantDoesntMatch);
                    }
                }

                CorElementType corType = RuntimeTypeHandle.GetCorElementType((RuntimeType)type);

                switch (corType)
                {
                    case CorElementType.I1:
                    case CorElementType.U1:
                    case CorElementType.Boolean:
                    case CorElementType.I2:
                    case CorElementType.U2:
                    case CorElementType.Char:
                    case CorElementType.I4:
                    case CorElementType.U4:
                    case CorElementType.R4:
                    case CorElementType.I8:
                    case CorElementType.U8:
                    case CorElementType.R8:
                        fixed (byte* pData = &JitHelpers.GetPinningHelper(value).m_data)
                        {
                            SetConstantValue(module.GetNativeHandle(), tk, (int)corType, pData);
                        }
                        break;

                    default:
                        if (type == typeof(string))
                        {
                            fixed (char* pString = (string)value)
                            {
                                SetConstantValue(module.GetNativeHandle(), tk, (int)CorElementType.String, pString);
                            }
                        }
                        else if (type == typeof(DateTime))
                        {
                            //date is a I8 representation
                            long ticks = ((DateTime)value).Ticks;
                            SetConstantValue(module.GetNativeHandle(), tk, (int)CorElementType.I8, &ticks);
                        }
                        else
                        {
                            throw new ArgumentException(SR.Format(SR.Argument_ConstantNotSupported, type.ToString()));
                        }
                        break;
                }
            }
            else
            {
                // A null default value in metadata is permissible even for non-nullable value types.
                // (See ECMA-335 II.15.4.1.4 "The .param directive" and II.22.9 "Constant" for details.)
                // This is how the Roslyn compilers generally encode `default(TValueType)` default values.

                SetConstantValue(module.GetNativeHandle(), tk, (int)CorElementType.Class, null);
            }
        }

        private List<CustAttr> _ca;
        private TypeToken _typeToken;
        private ModuleBuilder _module;
        private string _name;
        private string _namespace;
        private string _fullName;
        private Type _baseType;
        private List<Type> _interfaces;
        private TypeAttributes _attributes;
        private GenericParameterAttributes _genericParameterAttributes;
        internal List<MethodBuilder> _methods;
        internal int _lastTokenizedMethod;
        private int _constructorCount;
        private TypeBuilder _declaringType;

        // We cannot store this on EnumBuilder because users can define enum types manually using TypeBuilder.
        private Type _enumUnderlyingType;
        internal bool _isHiddenGlobalType;
        private bool _hasBeenCreated;
        private RuntimeType _bakedRuntimeType;

        private int _genericParameterPosition;
        private GenericTypeParameterBuilder[] _genericArguments;
        private bool _isGenericParameter;
        private MethodBuilder _declaringMethod;
        private readonly TypeBuilder _genericTypeInstantiation;
    
        // ctor for the global (module) type
        internal TypeBuilder(ModuleBuilder module)
        {
            _typeToken = new TypeToken((int)MetadataTokenType.TypeDef);
            _isHiddenGlobalType = true;
            _module = module;
            _methods = new List<MethodBuilder>();
            // No token has been created so let's initialize it to -1
            // The first time we call MethodBuilder.GetToken this will incremented.
            _lastTokenizedMethod = -1;
        }

        // ctor for generic method parameter
        internal TypeBuilder(string szName, int genParamPos, MethodBuilder declMeth)
        {
            Debug.Assert(declMeth != null);
            _declaringMethod = declMeth;
            _declaringType = _declaringMethod.GetTypeBuilder();
            _module = declMeth.GetModuleBuilder();
            InitAsGenericParam(szName, genParamPos);
        }

        // ctor for generic type parameter
        private TypeBuilder(string szName, int genParamPos, TypeBuilder declType)
        {
            Debug.Assert(declType != null);
            _declaringType = declType;
            _module = declType.GetModuleBuilder();
            InitAsGenericParam(szName, genParamPos);
        }

        private void InitAsGenericParam(string szName, int genParamPos)
        {
            _name = szName;
            _genericParameterPosition = genParamPos;
            _isGenericParameter = true;
            _interfaces = new List<Type>();
        }

        internal TypeBuilder(
            string name,
            TypeAttributes attr,
            Type parent,
            Type[] interfaces,
            ModuleBuilder module,
            PackingSize iPackingSize,
            int iTypeSize,
            TypeBuilder enclosingType)
        {
            Init(name, attr, parent, interfaces, module, iPackingSize, iTypeSize, enclosingType);
        }

        private void Init(string fullname, TypeAttributes attr, Type parent, Type[] interfaces, ModuleBuilder module,
            PackingSize iPackingSize, int iTypeSize, TypeBuilder enclosingType)
        {
            if (fullname == null)
            {
                throw new ArgumentNullException(nameof(fullname));
            }
            if (fullname.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(fullname));
            }
            if (fullname[0] == '\0')
            {
                throw new ArgumentException(SR.Argument_IllegalName, nameof(fullname));
            }
            if (fullname.Length > 1023)
            {
                throw new ArgumentException(SR.Argument_TypeNameTooLong, nameof(fullname));
            }

            int i;
            _module = module;
            _declaringType = enclosingType;
            AssemblyBuilder containingAssem = _module.ContainingAssemblyBuilder;

            // cannot have two types within the same assembly of the same name
            containingAssem._assemblyData.CheckTypeNameConflict(fullname, enclosingType);

            if (enclosingType != null)
            {
                // Nested Type should have nested attribute set.
                // If we are renumbering TypeAttributes' bit, we need to change the logic here.
                if (((attr & TypeAttributes.VisibilityMask) == TypeAttributes.Public) || ((attr & TypeAttributes.VisibilityMask) == TypeAttributes.NotPublic))
                {
                    throw new ArgumentException(SR.Argument_BadNestedTypeFlags, nameof(attr));
                }
            }

            int[] interfaceTokens = null;
            if (interfaces != null)
            {
                for (i = 0; i < interfaces.Length; i++)
                {
                    if (interfaces[i] == null)
                    {
                        throw new ArgumentNullException(nameof(interfaces));
                    }
                }
                interfaceTokens = new int[interfaces.Length + 1];
                for (i = 0; i < interfaces.Length; i++)
                {
                    interfaceTokens[i] = _module.GetTypeTokenInternal(interfaces[i]).Token;
                }
            }

            int iLast = fullname.LastIndexOf('.');
            if (iLast == -1 || iLast == 0)
            {
                // no name space
                _namespace = string.Empty;
                _name = fullname;
            }
            else
            {
                // split the name space
                _namespace = fullname.Substring(0, iLast);
                _name = fullname.Substring(iLast + 1);
            }

            VerifyTypeAttributes(attr);

            _attributes = attr;

            SetParent(parent);

            _methods = new List<MethodBuilder>();
            _lastTokenizedMethod = -1;

            SetInterfaces(interfaces);

            int tkParent = _baseType != null ? _module.GetTypeTokenInternal(_baseType).Token : 0;
            int tkEnclosingType = enclosingType != null ? enclosingType._typeToken.Token : 0;

            _typeToken = new TypeToken(DefineType(_module.GetNativeHandle(),
                fullname, tkParent, _attributes, tkEnclosingType, interfaceTokens));

            PackingSize = iPackingSize;
            Size = iTypeSize;
            if ((PackingSize != 0) || (Size != 0))
            {
                SetClassLayout(GetModuleBuilder().GetNativeHandle(), _typeToken.Token, PackingSize, Size);
            }

            _module.AddType(FullName, this);
        }

        private FieldBuilder DefineDataHelper(string name, byte[] data, int size, FieldAttributes attributes)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }
            if (size <= 0 || size >= 0x003f0000)
            {
                throw new ArgumentException(SR.Argument_BadSizeForData);
            }

            ThrowIfCreated();

            // form the value class name
            string valueClassName = ModuleBuilderData.MultiByteValueClass + size.ToString();

            // Is this already defined in this module?
            Type temp = _module.FindTypeBuilderWithName(valueClassName, false);
            TypeBuilder valueClassType = temp as TypeBuilder;

            if (valueClassType == null)
            {
                const TypeAttributes typeAttributes = TypeAttributes.Public | TypeAttributes.ExplicitLayout | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.AnsiClass;

                // Define the backing value class
                valueClassType = _module.DefineType(valueClassName, typeAttributes, typeof(ValueType), PackingSize.Size1, size);
                valueClassType.CreateType();
            }

            FieldBuilder fieldBuilder = DefineField(name, valueClassType, attributes | FieldAttributes.Static);

            // now we need to set the RVA
            fieldBuilder.SetData(data, size);
            return fieldBuilder;
        }

        private void VerifyTypeAttributes(TypeAttributes attr)
        {
            // Verify attr consistency for Nesting or otherwise.
            if (DeclaringType == null)
            {
                // Not a nested class.
                if (((attr & TypeAttributes.VisibilityMask) != TypeAttributes.NotPublic) && ((attr & TypeAttributes.VisibilityMask) != TypeAttributes.Public))
                {
                    throw new ArgumentException(SR.Argument_BadTypeAttrNestedVisibilityOnNonNestedType);
                }
            }
            else
            {
                // Nested class.
                if (((attr & TypeAttributes.VisibilityMask) == TypeAttributes.NotPublic) || ((attr & TypeAttributes.VisibilityMask) == TypeAttributes.Public))
                {
                    throw new ArgumentException(SR.Argument_BadTypeAttrNonNestedVisibilityNestedType);
                }
            }

            // Verify that the layout mask is valid.
            if (((attr & TypeAttributes.LayoutMask) != TypeAttributes.AutoLayout) && ((attr & TypeAttributes.LayoutMask) != TypeAttributes.SequentialLayout) && ((attr & TypeAttributes.LayoutMask) != TypeAttributes.ExplicitLayout))
            {
                throw new ArgumentException(SR.Argument_BadTypeAttrInvalidLayout);
            }

            // Check if the user attempted to set any reserved bits.
            if ((attr & TypeAttributes.ReservedMask) != 0)
            {
                throw new ArgumentException(SR.Argument_BadTypeAttrReservedBitsSet);
            }
        }

        public bool IsCreated() => _hasBeenCreated;

        #region FCalls
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern int DefineType(RuntimeModule module,
            string fullname, int tkParent, TypeAttributes attributes, int tkEnclosingType, int[] interfaceTokens);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern int DefineGenericParam(RuntimeModule module,
            string name, int tkParent, GenericParameterAttributes attributes, int position, int[] constraints);

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        private static extern void TermCreateClass(RuntimeModule module, int tk, ObjectHandleOnStack type);
        #endregion

        #region Internal Methods

        internal void ThrowIfCreated()
        {
            if (IsCreated())
            {
                throw new InvalidOperationException(SR.InvalidOperation_TypeHasBeenCreated);
            }
        }

        internal object SyncRoot => _module.SyncRoot;

        internal ModuleBuilder GetModuleBuilder() => _module;

        internal RuntimeType BakedRuntimeType => _bakedRuntimeType;

        internal void SetGenParamAttributes(GenericParameterAttributes genericParameterAttributes)
        {
            _genericParameterAttributes = genericParameterAttributes;
        }

        internal void SetGenParamCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
            CustAttr ca = new CustAttr(con, binaryAttribute);

            lock (SyncRoot)
            {
                SetGenParamCustomAttributeNoLock(ca);
            }
        }

        internal void SetGenParamCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            CustAttr ca = new CustAttr(customBuilder);

            lock (SyncRoot)
            {
                SetGenParamCustomAttributeNoLock(ca);
            }
        }

        private void SetGenParamCustomAttributeNoLock(CustAttr ca)
        {
            if (_ca == null)
            {
                _ca = new List<CustAttr>();
            }

            _ca.Add(ca);
        }

        #endregion

        public override string ToString()
        {
            return TypeNameBuilder.ToString(this, TypeNameBuilder.Format.ToString);
        }

        public override Type DeclaringType => _declaringType;

        public override Type ReflectedType => _declaringType;

        public override string Name => _name;

        public override Module Module => GetModuleBuilder();

        internal int MetadataTokenInternal => _typeToken.Token;

        public override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            return typeInfo != null && IsAssignableFrom(typeInfo.AsType());
        }

        public override Guid GUID
        {
            get
            {
                if (!IsCreated())
                {
                    throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
                }

                return _bakedRuntimeType.GUID;
            }
        }

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target,
            object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
        }

        public override Assembly Assembly => _module.Assembly;

        public override RuntimeTypeHandle TypeHandle
        {
            get => throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override string FullName
        {
            get => _fullName ?? (_fullName = TypeNameBuilder.ToString(this, TypeNameBuilder.Format.FullName));
        }

        public override string Namespace => _namespace;

        public override string AssemblyQualifiedName
        {
            get => TypeNameBuilder.ToString(this, TypeNameBuilder.Format.AssemblyQualifiedName);
        }

        public override Type BaseType => _baseType;

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder,
                CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetConstructor(bindingAttr, binder, callConvention, types, modifiers);
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetConstructors(bindingAttr);
        }

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder,
                CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            if (types == null)
            {
                return _bakedRuntimeType.GetMethod(name, bindingAttr);
            }
            else
            {
                return _bakedRuntimeType.GetMethod(name, bindingAttr, binder, callConvention, types, modifiers);
            }
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetMethods(bindingAttr);
        }

        public override FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetField(name, bindingAttr);
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetFields(bindingAttr);
        }

        public override Type GetInterface(string name, bool ignoreCase)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetInterface(name, ignoreCase);
        }

        public override Type[] GetInterfaces()
        {
            if (_bakedRuntimeType != null)
            {
                return _bakedRuntimeType.GetInterfaces();
            }

            if (_interfaces == null)
            {
                return Array.Empty<Type>();
            }

            return _interfaces.ToArray();
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetEvent(name, bindingAttr);
        }

        public override EventInfo[] GetEvents()
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetEvents();
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder,
                Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetProperties(bindingAttr);
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetNestedTypes(bindingAttr);
        }

        public override Type GetNestedType(string name, BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetNestedType(name, bindingAttr);
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetMember(name, type, bindingAttr);
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetInterfaceMap(interfaceType);
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetEvents(bindingAttr);
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return _bakedRuntimeType.GetMembers(bindingAttr);
        }

        public override bool IsAssignableFrom(Type c)
        {
            if (IsTypeEqual(c, this))
            {
                return true;
            }

            Type fromRuntimeType = null;
            TypeBuilder fromTypeBuilder = c as TypeBuilder;

            if (fromTypeBuilder != null)
            {
                fromRuntimeType = fromTypeBuilder._bakedRuntimeType;
            }
            else
            {
                fromRuntimeType = c;
            }

            if (fromRuntimeType != null && fromRuntimeType is RuntimeType)
            {
                // fromType is baked. So if this type is not baked, it cannot be assignable to!
                if (_bakedRuntimeType == null)
                {
                    return false;
                }

                // since toType is also baked, delegate to the base
                return _bakedRuntimeType.IsAssignableFrom(fromRuntimeType);
            }

            // So if c is not a runtimeType nor TypeBuilder. We don't know how to deal with it.
            // return false then.
            if (fromTypeBuilder == null)
            {
                return false;
            }

            // If fromTypeBuilder is a subclass of this class, then c can be cast to this type.
            if (fromTypeBuilder.IsSubclassOf(this))
            {
                return true;
            }
            if (!IsInterface)
            {
                return false;
            }

            // now is This type a base type on one of the interface impl?
            Type[] interfaces = fromTypeBuilder.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                // unfortunately, IsSubclassOf does not cover the case when they are the same type.
                if (IsTypeEqual(interfaces[i], this) || interfaces[i].IsSubclassOf(this))
                {
                    return true;
                }
            }
            return false;
        }

        protected override TypeAttributes GetAttributeFlagsImpl() => _attributes;

        public override bool IsTypeDefinition => true;

        public override bool IsSZArray => false;

        protected override bool IsArrayImpl() => false;

        protected override bool IsByRefImpl() => false;

        protected override bool IsPointerImpl() => false;

        protected override bool IsPrimitiveImpl() => false;

        protected override bool IsCOMObjectImpl() => (GetAttributeFlagsImpl() & TypeAttributes.Import) != 0;

        public override Type GetElementType() => throw new NotSupportedException(SR.NotSupported_DynamicModule);

        protected override bool HasElementTypeImpl() => false;

        public override bool IsSecurityCritical => true;

        public override bool IsSecuritySafeCritical => false;

        public override bool IsSecurityTransparent => false;

        public override bool IsSubclassOf(Type c)
        {
            Type p = this;
            if (IsTypeEqual(p, c))
            {
                return false;
            }

            p = p.BaseType;
            while (p != null)
            {
                if (IsTypeEqual(p, c))
                {
                    return true;
                }

                p = p.BaseType;
            }

            return false;
        }

        public override Type UnderlyingSystemType
        {
            get
            {
                if (_bakedRuntimeType != null)
                {
                    return _bakedRuntimeType;
                }

                if (IsEnum)
                {
                    if (_enumUnderlyingType == null)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_NoUnderlyingTypeOnEnum);
                    }

                    return _enumUnderlyingType;
                }
                else
                {
                    return this;
                }
            }
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

        public override object[] GetCustomAttributes(bool inherit)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }

            return CustomAttribute.GetCustomAttributes(_bakedRuntimeType, typeof(object) as RuntimeType, inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }
            if (attributeType == null)
            {
                throw new ArgumentNullException(nameof(attributeType));
            }
            if (!(attributeType.UnderlyingSystemType is RuntimeType attributeRuntimeType))
            {
                throw new ArgumentException(SR.Arg_MustBeType, nameof(attributeType));
            }

            return CustomAttribute.GetCustomAttributes(_bakedRuntimeType, attributeRuntimeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            if (!IsCreated())
            {
                throw new NotSupportedException(SR.NotSupported_TypeNotYetCreated);
            }
            if (attributeType == null)
            {
                throw new ArgumentNullException(nameof(attributeType));
            }
            if (!(attributeType.UnderlyingSystemType is RuntimeType attributeRuntimeType))
            {
                throw new ArgumentException(SR.Arg_MustBeType, nameof(attributeType));
            }

            return CustomAttribute.IsDefined(_bakedRuntimeType, attributeRuntimeType, inherit);
        }

        #region DefineType

        public override GenericParameterAttributes GenericParameterAttributes => _genericParameterAttributes;

        internal void SetInterfaces(params Type[] interfaces)
        {
            ThrowIfCreated();

            _interfaces = new List<Type>();
            if (interfaces != null)
            {
                _interfaces.AddRange(interfaces);
            }
        }

        public GenericTypeParameterBuilder[] DefineGenericParameters(params string[] names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }
            if (names.Length == 0)
            {
                throw new ArgumentException();
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null)
                {
                    throw new ArgumentNullException(nameof(names));
                }
            }

            if (_genericArguments != null)
            {
                throw new InvalidOperationException();
            }

            _genericArguments = new GenericTypeParameterBuilder[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                _genericArguments[i] = new GenericTypeParameterBuilder(new TypeBuilder(names[i], i, this));
            }

            return _genericArguments;
        }


        public override Type MakeGenericType(params Type[] typeArguments)
        {
            _module.CheckContext(typeArguments);

            return TypeBuilderInstantiation.MakeGenericType(this, typeArguments);
        }

        public override Type[] GetGenericArguments() => _genericArguments;

        // If a TypeBuilder is generic, it must be a generic type definition
        // All instantiated generic types are TypeBuilderInstantiation.
        public override bool IsGenericTypeDefinition => IsGenericType;

        public override bool IsGenericType => _genericArguments != null;

        public override bool IsGenericParameter => _isGenericParameter;

        public override bool IsConstructedGenericType => false;

        public override int GenericParameterPosition => _genericParameterPosition;

        public override MethodBase DeclaringMethod => _declaringMethod;

        public override Type GetGenericTypeDefinition()
        {
            if (IsGenericTypeDefinition)
            {
                return this;
            }

            return _genericTypeInstantiation ?? throw new InvalidOperationException();
        }

        #endregion

        #region Define Method

        public void DefineMethodOverride(MethodInfo methodInfoBody, MethodInfo methodInfoDeclaration)
        {
            lock (SyncRoot)
            {
                DefineMethodOverrideNoLock(methodInfoBody, methodInfoDeclaration);
            }
        }

        private void DefineMethodOverrideNoLock(MethodInfo methodInfoBody, MethodInfo methodInfoDeclaration)
        {
            if (methodInfoBody == null)
            {
                throw new ArgumentNullException(nameof(methodInfoBody));
            }
            if (methodInfoDeclaration == null)
            {
                throw new ArgumentNullException(nameof(methodInfoDeclaration));
            }

            ThrowIfCreated();

            // Loader restriction: body method has to be from this class
            if (!ReferenceEquals(methodInfoBody.DeclaringType, this))
            {
                throw new ArgumentException(SR.ArgumentException_BadMethodImplBody);
            }

            MethodToken bodyToken = _module.GetMethodTokenInternal(methodInfoBody);
            MethodToken declarationToken = _module.GetMethodTokenInternal(methodInfoDeclaration);

            DefineMethodImpl(_module.GetNativeHandle(), _typeToken.Token, bodyToken.Token, declarationToken.Token);
        }

        public MethodBuilder DefineMethod(string name, MethodAttributes attributes, Type returnType, Type[] parameterTypes)
        {
            return DefineMethod(name, attributes, CallingConventions.Standard, returnType, parameterTypes);
        }

        public MethodBuilder DefineMethod(string name, MethodAttributes attributes)
        {
            return DefineMethod(name, attributes, CallingConventions.Standard, null, null);
        }

        public MethodBuilder DefineMethod(string name, MethodAttributes attributes, CallingConventions callingConvention)
        {
            return DefineMethod(name, attributes, callingConvention, null, null);
        }

        public MethodBuilder DefineMethod(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] parameterTypes)
        {
            return DefineMethod(name, attributes, callingConvention, returnType, null, null, parameterTypes, null, null);
        }

        public MethodBuilder DefineMethod(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        {
            lock (SyncRoot)
            {
                return DefineMethodNoLock(name, attributes, callingConvention, returnType, returnTypeRequiredCustomModifiers,
                                          returnTypeOptionalCustomModifiers, parameterTypes, parameterTypeRequiredCustomModifiers,
                                          parameterTypeOptionalCustomModifiers);
            }
        }

        private MethodBuilder DefineMethodNoLock(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }

            _module.CheckContext(returnType);
            _module.CheckContext(returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers, parameterTypes);
            _module.CheckContext(parameterTypeRequiredCustomModifiers);
            _module.CheckContext(parameterTypeOptionalCustomModifiers);

            if (parameterTypes != null)
            {
                if (parameterTypeOptionalCustomModifiers != null && parameterTypeOptionalCustomModifiers.Length != parameterTypes.Length)
                {
                    throw new ArgumentException(SR.Format(SR.Argument_MismatchedArrays, nameof(parameterTypeOptionalCustomModifiers), nameof(parameterTypes)));
                }
                if (parameterTypeRequiredCustomModifiers != null && parameterTypeRequiredCustomModifiers.Length != parameterTypes.Length)
                {
                    throw new ArgumentException(SR.Format(SR.Argument_MismatchedArrays, nameof(parameterTypeRequiredCustomModifiers), nameof(parameterTypes)));
                }
            }

            ThrowIfCreated();

#if !FEATURE_DEFAULT_INTERFACES
            if (!m_isHiddenGlobalType)
            {
                if (((m_iAttr & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface) &&
                   (attributes & MethodAttributes.Abstract) == 0 && (attributes & MethodAttributes.Static) == 0)
                    throw new ArgumentException(SR.Argument_BadAttributeOnInterfaceMethod);
            }
#endif

            // pass in Method attributes
            MethodBuilder method = new MethodBuilder(
                name, attributes, callingConvention,
                returnType, returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers,
                parameterTypes, parameterTypeRequiredCustomModifiers, parameterTypeOptionalCustomModifiers,
                _module, this, false);

            if (!_isHiddenGlobalType)
            {
                //If this method is declared to be a constructor, increment our constructor count.
                if ((method.Attributes & MethodAttributes.SpecialName) != 0 && method.Name.Equals(ConstructorInfo.ConstructorName))
                {
                    _constructorCount++;
                }
            }

            _methods.Add(method);

            return method;
        }

        public MethodBuilder DefinePInvokeMethod(string name, string dllName, MethodAttributes attributes,
            CallingConventions callingConvention, Type returnType, Type[] parameterTypes,
            CallingConvention nativeCallConv, CharSet nativeCharSet)
        {
            MethodBuilder method = DefinePInvokeMethodHelper(
                name, dllName, name, attributes, callingConvention, returnType, null, null,
                parameterTypes, null, null, nativeCallConv, nativeCharSet);
            return method;
        }

        public MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, MethodAttributes attributes,
            CallingConventions callingConvention, Type returnType, Type[] parameterTypes,
            CallingConvention nativeCallConv, CharSet nativeCharSet)
        {
            MethodBuilder method = DefinePInvokeMethodHelper(
                name, dllName, entryName, attributes, callingConvention, returnType, null, null,
                parameterTypes, null, null, nativeCallConv, nativeCharSet);
            return method;
        }

        public MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, MethodAttributes attributes,
            CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers,
            CallingConvention nativeCallConv, CharSet nativeCharSet)
        {
            MethodBuilder method = DefinePInvokeMethodHelper(
            name, dllName, entryName, attributes, callingConvention, returnType, returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers,
            parameterTypes, parameterTypeRequiredCustomModifiers, parameterTypeOptionalCustomModifiers, nativeCallConv, nativeCharSet);
            return method;
        }

        private MethodBuilder DefinePInvokeMethodHelper(
            string name, string dllName, string importName, MethodAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers,
            CallingConvention nativeCallConv, CharSet nativeCharSet)
        {
            _module.CheckContext(returnType);
            _module.CheckContext(returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers, parameterTypes);
            _module.CheckContext(parameterTypeRequiredCustomModifiers);
            _module.CheckContext(parameterTypeOptionalCustomModifiers);

            lock (SyncRoot)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }
                if (name.Length == 0)
                {
                    throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
                }
                if (dllName == null)
                {
                    throw new ArgumentNullException(nameof(dllName));
                }
                if (dllName.Length == 0)
                {
                    throw new ArgumentException(SR.Argument_EmptyName, nameof(dllName));
                }
                if (importName == null)
                {
                    throw new ArgumentNullException(nameof(importName));
                }
                if (importName.Length == 0)
                {
                    throw new ArgumentException(SR.Argument_EmptyName, nameof(importName));
                }
                if ((attributes & MethodAttributes.Abstract) != 0)
                {
                    throw new ArgumentException(SR.Argument_BadPInvokeMethod);
                }
                if ((_attributes & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface)
                {
                    throw new ArgumentException(SR.Argument_BadPInvokeOnInterface);
                }
                ThrowIfCreated();

                attributes = attributes | MethodAttributes.PinvokeImpl;
                MethodBuilder method = new MethodBuilder(name, attributes, callingConvention,
                    returnType, returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers,
                    parameterTypes, parameterTypeRequiredCustomModifiers, parameterTypeOptionalCustomModifiers,
                    _module, this, false);

                //The signature grabbing code has to be up here or the signature won't be finished
                //and our equals check won't work.
                byte[] sigBytes = method.GetMethodSignature().InternalGetSignature(out int sigLength);

                if (_methods.Contains(method))
                {
                    throw new ArgumentException(SR.Argument_MethodRedefined);
                }
                _methods.Add(method);

                MethodToken token = method.GetToken();

                int linkFlags = 0;
                switch (nativeCallConv)
                {
                    case CallingConvention.Winapi:
                        linkFlags = (int)PInvokeMap.CallConvWinapi;
                        break;
                    case CallingConvention.Cdecl:
                        linkFlags = (int)PInvokeMap.CallConvCdecl;
                        break;
                    case CallingConvention.StdCall:
                        linkFlags = (int)PInvokeMap.CallConvStdcall;
                        break;
                    case CallingConvention.ThisCall:
                        linkFlags = (int)PInvokeMap.CallConvThiscall;
                        break;
                    case CallingConvention.FastCall:
                        linkFlags = (int)PInvokeMap.CallConvFastcall;
                        break;
                }
                switch (nativeCharSet)
                {
                    case CharSet.None:
                        linkFlags |= (int)PInvokeMap.CharSetNotSpec;
                        break;
                    case CharSet.Ansi:
                        linkFlags |= (int)PInvokeMap.CharSetAnsi;
                        break;
                    case CharSet.Unicode:
                        linkFlags |= (int)PInvokeMap.CharSetUnicode;
                        break;
                    case CharSet.Auto:
                        linkFlags |= (int)PInvokeMap.CharSetAuto;
                        break;
                }

                SetPInvokeData(_module.GetNativeHandle(),
                    dllName,
                    importName,
                    token.Token,
                    linkFlags);
                method.SetToken(token);

                return method;
            }
        }
        #endregion

        #region Define Constructor
        public ConstructorBuilder DefineTypeInitializer()
        {
            lock (SyncRoot)
            {
                return DefineTypeInitializerNoLock();
            }
        }

        private ConstructorBuilder DefineTypeInitializerNoLock()
        {
            ThrowIfCreated();

            // change the attributes and the class constructor's name
            MethodAttributes attr = MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName;

            ConstructorBuilder constBuilder = new ConstructorBuilder(
                ConstructorInfo.TypeConstructorName, attr, CallingConventions.Standard, null, _module, this);

            return constBuilder;
        }

        public ConstructorBuilder DefineDefaultConstructor(MethodAttributes attributes)
        {
            if ((_attributes & TypeAttributes.Interface) == TypeAttributes.Interface)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ConstructorNotAllowedOnInterface);
            }

            lock (SyncRoot)
            {
                return DefineDefaultConstructorNoLock(attributes);
            }
        }

        private ConstructorBuilder DefineDefaultConstructorNoLock(MethodAttributes attributes)
        {
            ConstructorBuilder constBuilder;

            // get the parent class's default constructor
            // We really don't want(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic) here.  We really want
            // constructors visible from the subclass, but that is not currently
            // available in BindingFlags.  This more open binding is open to
            // runtime binding failures(like if we resolve to a private
            // constructor).
            ConstructorInfo con = null;

            if (_baseType is TypeBuilderInstantiation)
            {
                Type genericTypeDefinition = _baseType.GetGenericTypeDefinition();

                if (genericTypeDefinition is TypeBuilder)
                {
                    genericTypeDefinition = ((TypeBuilder)genericTypeDefinition)._bakedRuntimeType;
                }
                if (genericTypeDefinition == null)
                {
                    throw new NotSupportedException(SR.NotSupported_DynamicModule);
                }

                Type inst = genericTypeDefinition.MakeGenericType(_baseType.GetGenericArguments());

                if (inst is TypeBuilderInstantiation)
                    con = GetConstructor(inst, genericTypeDefinition.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, EmptyTypes, null));
                else
                    con = inst.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, EmptyTypes, null);
            }

            if (con == null)
            {
                con = _baseType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, EmptyTypes, null);
            }

            if (con == null)
            {
                throw new NotSupportedException(SR.NotSupported_NoParentDefaultConstructor);
            }

            // Define the constructor Builder
            constBuilder = DefineConstructor(attributes, CallingConventions.Standard, null);
            _constructorCount++;

            // generate the code to call the parent's default constructor
            ILGenerator il = constBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, con);
            il.Emit(OpCodes.Ret);

            constBuilder._isDefaultConstructor = true;
            return constBuilder;
        }

        public ConstructorBuilder DefineConstructor(MethodAttributes attributes, CallingConventions callingConvention, Type[] parameterTypes)
        {
            return DefineConstructor(attributes, callingConvention, parameterTypes, null, null);
        }

        public ConstructorBuilder DefineConstructor(MethodAttributes attributes, CallingConventions callingConvention,
            Type[] parameterTypes, Type[][] requiredCustomModifiers, Type[][] optionalCustomModifiers)
        {
            if ((_attributes & TypeAttributes.Interface) == TypeAttributes.Interface && (attributes & MethodAttributes.Static) != MethodAttributes.Static)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ConstructorNotAllowedOnInterface);
            }

            lock (SyncRoot)
            {
                return DefineConstructorNoLock(attributes, callingConvention, parameterTypes, requiredCustomModifiers, optionalCustomModifiers);
            }
        }

        private ConstructorBuilder DefineConstructorNoLock(MethodAttributes attributes, CallingConventions callingConvention,
            Type[] parameterTypes, Type[][] requiredCustomModifiers, Type[][] optionalCustomModifiers)
        {
            _module.CheckContext(parameterTypes);
            _module.CheckContext(requiredCustomModifiers);
            _module.CheckContext(optionalCustomModifiers);

            ThrowIfCreated();

            string name;

            if ((attributes & MethodAttributes.Static) == 0)
            {
                name = ConstructorInfo.ConstructorName;
            }
            else
            {
                name = ConstructorInfo.TypeConstructorName;
            }

            attributes = attributes | MethodAttributes.SpecialName;

            ConstructorBuilder constBuilder =
                new ConstructorBuilder(name, attributes, callingConvention,
                    parameterTypes, requiredCustomModifiers, optionalCustomModifiers, _module, this);

            _constructorCount++;

            return constBuilder;
        }

        #endregion

        #region Define Nested Type
        public TypeBuilder DefineNestedType(string name)
        {
            lock (SyncRoot)
            {
                return DefineNestedTypeNoLock(name, TypeAttributes.NestedPrivate, null, null, PackingSize.Unspecified, UnspecifiedTypeSize);
            }
        }

        public TypeBuilder DefineNestedType(string name, TypeAttributes attr, Type parent, Type[] interfaces)
        {
            lock (SyncRoot)
            {
                _module.CheckContext(parent);
                _module.CheckContext(interfaces);

                return DefineNestedTypeNoLock(name, attr, parent, interfaces, PackingSize.Unspecified, UnspecifiedTypeSize);
            }
        }

        public TypeBuilder DefineNestedType(string name, TypeAttributes attr, Type parent)
        {
            lock (SyncRoot)
            {
                return DefineNestedTypeNoLock(name, attr, parent, null, PackingSize.Unspecified, UnspecifiedTypeSize);
            }
        }

        public TypeBuilder DefineNestedType(string name, TypeAttributes attr)
        {
            lock (SyncRoot)
            {
                return DefineNestedTypeNoLock(name, attr, null, null, PackingSize.Unspecified, UnspecifiedTypeSize);
            }
        }

        public TypeBuilder DefineNestedType(string name, TypeAttributes attr, Type parent, int typeSize)
        {
            lock (SyncRoot)
            {
                return DefineNestedTypeNoLock(name, attr, parent, null, PackingSize.Unspecified, typeSize);
            }
        }

        public TypeBuilder DefineNestedType(string name, TypeAttributes attr, Type parent, PackingSize packSize)
        {
            lock (SyncRoot)
            {
                return DefineNestedTypeNoLock(name, attr, parent, null, packSize, UnspecifiedTypeSize);
            }
        }

        public TypeBuilder DefineNestedType(string name, TypeAttributes attr, Type parent, PackingSize packSize, int typeSize)
        {
            lock (SyncRoot)
            {
                return DefineNestedTypeNoLock(name, attr, parent, null, packSize, typeSize);
            }
        }

        private TypeBuilder DefineNestedTypeNoLock(string name, TypeAttributes attr, Type parent, Type[] interfaces, PackingSize packSize, int typeSize)
        {
            return new TypeBuilder(name, attr, parent, interfaces, _module, packSize, typeSize, this);
        }

        #endregion

        #region Define Field
        public FieldBuilder DefineField(string fieldName, Type type, FieldAttributes attributes)
        {
            return DefineField(fieldName, type, null, null, attributes);
        }

        public FieldBuilder DefineField(string fieldName, Type type, Type[] requiredCustomModifiers,
            Type[] optionalCustomModifiers, FieldAttributes attributes)
        {
            lock (SyncRoot)
            {
                return DefineFieldNoLock(fieldName, type, requiredCustomModifiers, optionalCustomModifiers, attributes);
            }
        }

        private FieldBuilder DefineFieldNoLock(string fieldName, Type type, Type[] requiredCustomModifiers,
            Type[] optionalCustomModifiers, FieldAttributes attributes)
        {
            ThrowIfCreated();
            _module.CheckContext(type);
            _module.CheckContext(requiredCustomModifiers);

            if (_enumUnderlyingType == null && IsEnum == true)
            {
                if ((attributes & FieldAttributes.Static) == 0)
                {
                    // remember the underlying type for enum type
                    _enumUnderlyingType = type;
                }
            }

            return new FieldBuilder(this, fieldName, type, requiredCustomModifiers, optionalCustomModifiers, attributes);
        }

        public FieldBuilder DefineInitializedData(string name, byte[] data, FieldAttributes attributes)
        {
            lock (SyncRoot)
            {
                return DefineInitializedDataNoLock(name, data, attributes);
            }
        }

        private FieldBuilder DefineInitializedDataNoLock(string name, byte[] data, FieldAttributes attributes)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // This method will define an initialized Data in .sdata.
            // We will create a fake TypeDef to represent the data with size. This TypeDef
            // will be the signature for the Field.
            return DefineDataHelper(name, data, data.Length, attributes);
        }

        public FieldBuilder DefineUninitializedData(string name, int size, FieldAttributes attributes)
        {
            lock (SyncRoot)
            {
                return DefineUninitializedDataNoLock(name, size, attributes);
            }
        }

        private FieldBuilder DefineUninitializedDataNoLock(string name, int size, FieldAttributes attributes)
        {
            // This method will define an uninitialized Data in .sdata.
            // We will create a fake TypeDef to represent the data with size. This TypeDef
            // will be the signature for the Field.
            return DefineDataHelper(name, null, size, attributes);
        }

        #endregion

        #region Define Properties and Events
        public PropertyBuilder DefineProperty(string name, PropertyAttributes attributes, Type returnType, Type[] parameterTypes)
        {
            return DefineProperty(name, attributes, returnType, null, null, parameterTypes, null, null);
        }

        public PropertyBuilder DefineProperty(string name, PropertyAttributes attributes,
            CallingConventions callingConvention, Type returnType, Type[] parameterTypes)
        {
            return DefineProperty(name, attributes, callingConvention, returnType, null, null, parameterTypes, null, null);
        }


        public PropertyBuilder DefineProperty(string name, PropertyAttributes attributes,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        {
            return DefineProperty(name, attributes, (CallingConventions)0, returnType,
                returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers,
                parameterTypes, parameterTypeRequiredCustomModifiers, parameterTypeOptionalCustomModifiers);
        }

        public PropertyBuilder DefineProperty(string name, PropertyAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        {
            lock (SyncRoot)
            {
                return DefinePropertyNoLock(name, attributes, callingConvention, returnType, returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers,
                                            parameterTypes, parameterTypeRequiredCustomModifiers, parameterTypeOptionalCustomModifiers);
            }
        }

        private PropertyBuilder DefinePropertyNoLock(string name, PropertyAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }

            _module.CheckContext(returnType);
            _module.CheckContext(returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers, parameterTypes);
            _module.CheckContext(parameterTypeRequiredCustomModifiers);
            _module.CheckContext(parameterTypeOptionalCustomModifiers);

            ThrowIfCreated();

            // get the signature in SignatureHelper form
            SignatureHelper sigHelper = SignatureHelper.GetPropertySigHelper(
                _module, callingConvention,
                returnType, returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers,
                parameterTypes, parameterTypeRequiredCustomModifiers, parameterTypeOptionalCustomModifiers);

            // get the signature in byte form
            byte[] sigBytes = sigHelper.InternalGetSignature(out int sigLength);

            PropertyToken prToken = new PropertyToken(DefineProperty(
                _module.GetNativeHandle(),
                _typeToken.Token,
                name,
                attributes,
                sigBytes,
                sigLength));

            // create the property builder now.
            return new PropertyBuilder(
                    _module,
                    name,
                    sigHelper,
                    attributes,
                    returnType,
                    prToken,
                    this);
        }

        public EventBuilder DefineEvent(string name, EventAttributes attributes, Type eventtype)
        {
            lock (SyncRoot)
            {
                return DefineEventNoLock(name, attributes, eventtype);
            }
        }

        private EventBuilder DefineEventNoLock(string name, EventAttributes attributes, Type eventtype)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }
            if (name[0] == '\0')
            {
                throw new ArgumentException(SR.Argument_IllegalName, nameof(name));
            }

            _module.CheckContext(eventtype);
            ThrowIfCreated();

            int tkType = _module.GetTypeTokenInternal(eventtype).Token;

            // Internal helpers to define property records
            EventToken evToken = new EventToken(DefineEvent(
                _module.GetNativeHandle(),
                _typeToken.Token,
                name,
                attributes,
                tkType));

            // create the property builder now.
            return new EventBuilder(
                    _module,
                    name,
                    attributes,
                    //tkType,
                    this,
                    evToken);
        }

        #endregion

        #region Create Type

        public TypeInfo CreateTypeInfo()
        {
            lock (SyncRoot)
            {
                return CreateTypeNoLock();
            }
        }

        public Type CreateType()
        {
            lock (SyncRoot)
            {
                return CreateTypeNoLock();
            }
        }

        private TypeInfo CreateTypeNoLock()
        {
            if (IsCreated())
            {
                return _bakedRuntimeType;
            }

            ThrowIfCreated();

            if (_interfaces == null)
            {
                _interfaces = new List<Type>();
            }

            int[] interfaceTokens = new int[_interfaces.Count];
            for (int i = 0; i < _interfaces.Count; i++)
            {
                interfaceTokens[i] = _module.GetTypeTokenInternal(_interfaces[i]).Token;
            }

            int tkParent = 0;
            if (_baseType != null)
                tkParent = _module.GetTypeTokenInternal(_baseType).Token;

            if (IsGenericParameter)
            {
                int[] constraints; // Array of token constrains terminated by null token

                if (_baseType != null)
                {
                    constraints = new int[_interfaces.Count + 2];
                    constraints[constraints.Length - 2] = tkParent;
                }
                else
                {
                    constraints = new int[_interfaces.Count + 1];
                }

                for (int i = 0; i < _interfaces.Count; i++)
                {
                    constraints[i] = _module.GetTypeTokenInternal(_interfaces[i]).Token;
                }

                int declMember = _declaringMethod == null ? _declaringType._typeToken.Token : _declaringMethod.GetToken().Token;
                _typeToken = new TypeToken(DefineGenericParam(_module.GetNativeHandle(),
                    _name, declMember, _genericParameterAttributes, _genericParameterPosition, constraints));

                if (_ca != null)
                {
                    foreach (CustAttr ca in _ca)
                        ca.Bake(_module, MetadataTokenInternal);
                }

                _hasBeenCreated = true;

                // Baking a generic parameter does not put sufficient information into the metadata to actually be able to load it as a type,
                // the associated generic type/method needs to be baked first. So we return this rather than the baked type.
                return this;
            }
            else
            {
                // Check for global typebuilder
                if (((_typeToken.Token & 0x00FFFFFF) != 0) && ((tkParent & 0x00FFFFFF) != 0))
                {
                    SetParentType(_module.GetNativeHandle(), _typeToken.Token, tkParent);
                }

                if (_genericArguments != null)
                {
                    foreach (Type tb in _genericArguments)
                    {
                        if (tb is GenericTypeParameterBuilder)
                        {
                            ((GenericTypeParameterBuilder)tb)._type.CreateType();
                        }
                    }
                }
            }

            if (!_isHiddenGlobalType)
            {
                // create a public default constructor if this class has no constructor.
                // except if the type is Interface, ValueType, Enum, or a static class.
                if (_constructorCount == 0 && ((_attributes & TypeAttributes.Interface) == 0) && !IsValueType && ((_attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed)) != (TypeAttributes.Abstract | TypeAttributes.Sealed)))
                {
                    DefineDefaultConstructor(MethodAttributes.Public);
                }
            }

            int size = _methods.Count;

            for (int i = 0; i < size; i++)
            {
                MethodBuilder meth = _methods[i];
                if (meth.IsGenericMethodDefinition)
                {
                    meth.GetToken(); // Doubles as "CreateMethod" for MethodBuilder -- analogous to CreateType()
                }

                MethodAttributes methodAttrs = meth.Attributes;

                // Any of these flags in the implemenation flags is set, we will not attach the IL method body
                if (((meth.GetMethodImplementationFlags() & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.PreserveSig | MethodImplAttributes.Unmanaged)) != MethodImplAttributes.IL) ||
                    ((methodAttrs & MethodAttributes.PinvokeImpl) != 0))
                {
                    continue;
                }

                byte[] localSig = meth.GetLocalSignature(out int sigLength);

                // Check that they haven't declared an abstract method on a non-abstract class
                if (((methodAttrs & MethodAttributes.Abstract) != 0) && ((_attributes & TypeAttributes.Abstract) == 0))
                {
                    throw new InvalidOperationException(SR.InvalidOperation_BadTypeAttributesNotAbstract);
                }

                byte[] body = meth.GetBody();

                // If this is an abstract method or an interface, we don't need to set the IL.
                if ((methodAttrs & MethodAttributes.Abstract) != 0)
                {
                    // We won't check on Interface because we can have class static initializer on interface.
                    // We will just let EE or validator to catch the problem.
                    if (body != null)
                    {
                        throw new InvalidOperationException(SR.Format(SR.InvalidOperation_BadMethodBody, meth.Name));
                    }
                }
                else if (body == null || body.Length == 0)
                {
                    // If it's not an abstract or an interface, set the IL.
                    if (meth._ilGenerator != null)
                    {
                        // we need to bake the method here.
                        meth.CreateMethodBodyHelper(meth.GetILGenerator());
                    }

                    body = meth.GetBody();

                    if ((body == null || body.Length == 0) && !meth._canBeRuntimeImpl)
                    {
                        throw new InvalidOperationException(
                            SR.Format(SR.InvalidOperation_BadEmptyMethodBody, meth.Name));
                    }
                }

                int maxStack = meth.GetMaxStack();

                ExceptionHandler[] exceptions = meth.GetExceptionHandlers();
                int[] tokenFixups = meth.GetTokenFixups();

                SetMethodIL(_module.GetNativeHandle(), meth.GetToken().Token, meth.InitLocals,
                    body, (body != null) ? body.Length : 0,
                    localSig, sigLength, maxStack,
                    exceptions, (exceptions != null) ? exceptions.Length : 0,
                    tokenFixups, (tokenFixups != null) ? tokenFixups.Length : 0);

                if (_module.ContainingAssemblyBuilder._assemblyData._access == AssemblyBuilderAccess.Run)
                {
                    // if we don't need the data structures to build the method any more
                    // throw them away.
                    meth.ReleaseBakedStructures();
                }
            }

            _hasBeenCreated = true;

            // Terminate the process.
            RuntimeType cls = null;
            TermCreateClass(_module.GetNativeHandle(), _typeToken.Token, JitHelpers.GetObjectHandleOnStack(ref cls));

            if (!_isHiddenGlobalType)
            {
                _bakedRuntimeType = cls;

                // if this type is a nested type, we need to invalidate the cached nested runtime type on the nesting type
                if (_declaringType != null && _declaringType._bakedRuntimeType != null)
                {
                    _declaringType._bakedRuntimeType.InvalidateCachedNestedType();
                }

                return cls;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Misc

        public int Size { get; private set; }

        public PackingSize PackingSize { get; private set; }

        public void SetParent(Type parent)
        {
            ThrowIfCreated();

            if (parent != null)
            {
                _module.CheckContext(parent);

                if (parent.IsInterface)
                {
                    throw new ArgumentException(SR.Argument_CannotSetParentToInterface);
                }

                _baseType = parent;
            }
            else
            {
                if ((_attributes & TypeAttributes.Interface) != TypeAttributes.Interface)
                {
                    _baseType = typeof(object);
                }
                else
                {
                    if ((_attributes & TypeAttributes.Abstract) == 0)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_BadInterfaceNotAbstract);
                    }

                    // there is no extends for interface class
                    _baseType = null;
                }
            }
        }

        public void AddInterfaceImplementation(Type interfaceType)
        {
            if (interfaceType == null)
            {
                throw new ArgumentNullException(nameof(interfaceType));
            }

            _module.CheckContext(interfaceType);
            ThrowIfCreated();

            TypeToken tkInterface = _module.GetTypeTokenInternal(interfaceType);
            AddInterfaceImpl(_module.GetNativeHandle(), _typeToken.Token, tkInterface.Token);

            _interfaces.Add(interfaceType);
        }

        public TypeToken TypeToken
        {
            get
            {
                if (IsGenericParameter)
                {
                    ThrowIfCreated();
                }

                return _typeToken;
            }
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

            DefineCustomAttribute(_module, _typeToken.Token, _module.GetConstructorToken(con).Token,
                binaryAttribute, false, false);
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            if (customBuilder == null)
            {
                throw new ArgumentNullException(nameof(customBuilder));
            }

            customBuilder.CreateCustomAttribute(_module, _typeToken.Token);
        }

        #endregion
    }
}
