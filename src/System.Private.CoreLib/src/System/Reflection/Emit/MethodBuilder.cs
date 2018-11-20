// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Reflection.Emit
{
    public sealed class MethodBuilder : MethodInfo
    {
        #region Private Data Members

        // Identity
        internal string _name;
        private MethodToken _methodToken;
        private ModuleBuilder _module;
        internal TypeBuilder _containingType;

        // IL
        // The location of all of the token fixups. Null means no fixups.
        private int[] _tokenFixupLocations;

        // Local signature if set explicitly via DefineBody. Null otherwise.
        private byte[] _localSignature;

        // Keep track debugging local information
        internal LocalSymInfo _localSymbolInfo;

        internal ILGenerator _ilGenerator;
        private byte[] _methodBytes;
        private ExceptionHandler[] _exceptionHandles;
        private const int DefaultMaxStack = 16;

        // Flags
        internal bool _isBaked;
        private readonly bool _isGlobalMethod;

        // Indicating if the method stack frame will be zero initialized or not.
        private bool _hasInitLocals;

        // Attributes
        private MethodAttributes _attributes;
        private readonly CallingConventions _callingConvention;
        private MethodImplAttributes _methodImplAttributes;
        private readonly List<SymCustomAttr> _symCustomAttrs;

        internal bool _canBeRuntimeImpl = false;
        private bool _isDllImport = false;

        // Parameters
        private SignatureHelper _signature;
        internal Type[] _parameterTypes;
        private Type _returnType;
        private Type[] _returnTypeRequiredCustomModifiers;
        private Type[] _returnTypeOptionalCustomModifiers;
        private Type[][] _parameterTypeRequiredCustomModifiers;
        private Type[][] _parameterTypeOptionalCustomModifiers;

        // Generics
        private GenericTypeParameterBuilder[] _genericArguments;
        private bool _isGenericMethodDefinition;

        #endregion

        #region Constructor

        internal MethodBuilder(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers,
            ModuleBuilder mod, TypeBuilder type, bool bIsGlobalMethod)
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
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }
            if (parameterTypes != null)
            {
                foreach (Type t in parameterTypes)
                {
                    if (t == null)
                    {
                        throw new ArgumentNullException(nameof(parameterTypes));
                    }
                }
            }

            _name = name;
            _module = mod;
            _containingType = type;
            _returnType = returnType;

            if ((attributes & MethodAttributes.Static) == 0)
            {
                // turn on the has this calling convention
                callingConvention = callingConvention | CallingConventions.HasThis;
            }
            else if ((attributes & MethodAttributes.Virtual) != 0)
            {
                // A method can't be both static and virtual
                throw new ArgumentException(SR.Arg_NoStaticVirtual);
            }

#if !FEATURE_DEFAULT_INTERFACES
            if ((attributes & MethodAttributes.SpecialName) != MethodAttributes.SpecialName)
            {
                if ((type.Attributes & TypeAttributes.Interface) == TypeAttributes.Interface)
                {
                    // methods on interface have to be abstract + virtual except special name methods such as type initializer
                    if ((attributes & (MethodAttributes.Abstract | MethodAttributes.Virtual)) !=
                        (MethodAttributes.Abstract | MethodAttributes.Virtual) &&
                        (attributes & MethodAttributes.Static) == 0)
                        throw new ArgumentException(SR.Argument_BadAttributeOnInterfaceMethod);
                }
            }
#endif

            _callingConvention = callingConvention;

            if (parameterTypes != null)
            {
                _parameterTypes = new Type[parameterTypes.Length];
                Array.Copy(parameterTypes, 0, _parameterTypes, 0, parameterTypes.Length);
            }
            else
            {
                _parameterTypes = null;
            }

            _returnTypeRequiredCustomModifiers = returnTypeRequiredCustomModifiers;
            _returnTypeOptionalCustomModifiers = returnTypeOptionalCustomModifiers;
            _parameterTypeRequiredCustomModifiers = parameterTypeRequiredCustomModifiers;
            _parameterTypeOptionalCustomModifiers = parameterTypeOptionalCustomModifiers;

            _attributes = attributes;
            _isGlobalMethod = bIsGlobalMethod;
            _isBaked = false;
            _hasInitLocals = true;

            _localSymbolInfo = new LocalSymInfo();
            _methodBytes = null;
            _ilGenerator = null;

            // Default is managed IL. Manged IL has bit flag 0x0020 set off
            _methodImplAttributes = MethodImplAttributes.IL;
        }

        #endregion

        #region Internal Members

        /// <summary>
        /// Sets the IL of this method.
        /// </summary>
        /// <param name="il">The ILGenerator the method queries to get all of the information it needs.</param>
        internal void CreateMethodBodyHelper(ILGenerator il)
        {
            if (il == null)
            {
                throw new ArgumentNullException(nameof(il));
            }

            _containingType.ThrowIfCreated();

            if (_isBaked)
            {
                throw new InvalidOperationException(SR.InvalidOperation_MethodHasBody);
            }

            if (il.m_methodBuilder != this && il.m_methodBuilder != null)
            {
                // You don't need to call DefineBody when you get your ILGenerator
                // through MethodBuilder::GetILGenerator.
                throw new InvalidOperationException(SR.InvalidOperation_BadILGeneratorUsage);
            }

            ThrowIfShouldNotHaveBody();

            if (il.m_ScopeTree.m_iOpenScopeCount != 0)
            {
                // There are still unclosed local scope
                throw new InvalidOperationException(SR.InvalidOperation_OpenLocalVariableScope);
            }

            _methodBytes = il.BakeByteArray();
            _tokenFixupLocations = il.GetTokenFixups();

            // Calculate all of the exceptions.
            __ExceptionInfo[] excp = il.GetExceptions();
            int numExceptions = CalculateNumberOfExceptions(excp);
            if (numExceptions > 0)
            {
                _exceptionHandles = new ExceptionHandler[numExceptions];

                for (int i = 0; i < excp.Length; i++)
                {
                    int[] filterAddrs = excp[i].GetFilterAddresses();
                    int[] catchAddrs = excp[i].GetCatchAddresses();
                    int[] catchEndAddrs = excp[i].GetCatchEndAddresses();
                    Type[] catchClass = excp[i].GetCatchClass();

                    int numCatch = excp[i].GetNumberOfCatches();
                    int start = excp[i].GetStartAddress();
                    int end = excp[i].GetEndAddress();
                    int[] type = excp[i].GetExceptionTypes();
                    int counter = 0;
                    for (int j = 0; j < numCatch; j++)
                    {
                        int tkExceptionClass = 0;
                        if (catchClass[j] != null)
                        {
                            tkExceptionClass = _module.GetTypeTokenInternal(catchClass[j]).Token;
                        }

                        switch (type[j])
                        {
                            case __ExceptionInfo.None:
                            case __ExceptionInfo.Fault:
                            case __ExceptionInfo.Filter:
                                _exceptionHandles[counter++] = new ExceptionHandler(start, end, filterAddrs[j], catchAddrs[j], catchEndAddrs[j], type[j], tkExceptionClass);
                                break;

                            case __ExceptionInfo.Finally:
                                _exceptionHandles[counter++] = new ExceptionHandler(start, excp[i].GetFinallyEndAddress(), filterAddrs[j], catchAddrs[j], catchEndAddrs[j], type[j], tkExceptionClass);
                                break;
                        }
                    }
                }
            }


            _isBaked = true;

            if (_module.GetSymWriter() != null)
            {
                // set the debugging information such as scope and line number
                // if it is in a debug module
                //
                SymbolToken tk = new SymbolToken(MetadataTokenInternal);
                ISymbolWriter symWriter = _module.GetSymWriter();

                // call OpenMethod to make this method the current method
                symWriter.OpenMethod(tk);

                // call OpenScope because OpenMethod no longer implicitly creating
                // the top-levelsmethod scope
                //
                symWriter.OpenScope(0);

                if (_symCustomAttrs != null)
                {
                    foreach (SymCustomAttr symCustomAttr in _symCustomAttrs)
                        _module.GetSymWriter().SetSymAttribute(
                        new SymbolToken(MetadataTokenInternal),
                            symCustomAttr.m_name,
                            symCustomAttr.m_data);
                }

                if (_localSymbolInfo != null)
                    _localSymbolInfo.EmitLocalSymInfo(symWriter);
                il.m_ScopeTree.EmitScopeTree(symWriter);
                il.m_LineNumberInfo.EmitLineNumberInfo(symWriter);
                symWriter.CloseScope(il.ILOffset);
                symWriter.CloseMethod();
            }
        }

        // This is only called from TypeBuilder.CreateType after the method has been created
        internal void ReleaseBakedStructures()
        {
            if (!_isBaked)
            {
                // We don't need to do anything here if we didn't baked the method body
                return;
            }

            _methodBytes = null;
            _localSymbolInfo = null;
            _tokenFixupLocations = null;
            _localSignature = null;
            _exceptionHandles = null;
        }

        internal override Type[] GetParameterTypes()
        {
            return _parameterTypes ?? (_parameterTypes = Array.Empty<Type>());
        }

        internal static Type GetMethodBaseReturnType(MethodBase method)
        {
            if (method is MethodInfo mi)
            {
                return mi.ReturnType;
            }
            else if (method is ConstructorInfo ci)
            {
                return ci.GetReturnType();
            }

            Debug.Fail("We should never get here!");
            return null;
        }

        internal void SetToken(MethodToken token) => _methodToken = token;

        /// <summary>
        /// Returns the il bytes of this method.
        /// This il is not valid until somebody has called BakeByteArray
        /// </summary>
        /// <returns></returns>
        internal byte[] GetBody() => _methodBytes;

        internal int[] GetTokenFixups() => _tokenFixupLocations;

        internal SignatureHelper GetMethodSignature()
        {
            if (_parameterTypes == null)
            {
                _parameterTypes = Array.Empty<Type>();
            }

            _signature = SignatureHelper.GetMethodSigHelper(_module, _callingConvention, _genericArguments != null ? _genericArguments.Length : 0,
                _returnType ?? typeof(void), _returnTypeRequiredCustomModifiers, _returnTypeOptionalCustomModifiers,
                _parameterTypes, _parameterTypeRequiredCustomModifiers, _parameterTypeOptionalCustomModifiers);

            return _signature;
        }

        /// <summary>
        /// Returns a buffer whose initial signatureLength bytes contain encoded local signature.
        /// </summary>
        internal byte[] GetLocalSignature(out int signatureLength)
        {
            if (_localSignature != null)
            {
                signatureLength = _localSignature.Length;
                return _localSignature;
            }

            if (_ilGenerator != null)
            {
                if (_ilGenerator.m_localCount != 0)
                {
                    // If user is using ILGenerator::DeclareLocal, then get local signaturefrom there.
                    return _ilGenerator.m_localSignature.InternalGetSignature(out signatureLength);
                }
            }

            return SignatureHelper.GetLocalVarSigHelper(_module).InternalGetSignature(out signatureLength);
        }

        internal int GetMaxStack()
        {
            if (_ilGenerator == null)
            {
                // this is the case when client provide an array of IL byte stream rather than going through ILGenerator.
                return DefaultMaxStack;
            }

            return _ilGenerator.GetMaxStackSize() + ExceptionHandlerCount;
        }

        internal ExceptionHandler[] GetExceptionHandlers() => _exceptionHandles;

        internal int ExceptionHandlerCount => _exceptionHandles != null ? _exceptionHandles.Length : 0;

        internal int CalculateNumberOfExceptions(__ExceptionInfo[] excp)
        {
            if (excp == null)
            {
                return 0;
            }

            int num = 0;
            for (int i = 0; i < excp.Length; i++)
            {
                num += excp[i].GetNumberOfCatches();
            }

            return num;
        }

        internal bool IsTypeCreated() => _containingType != null && _containingType.IsCreated();

        internal TypeBuilder GetTypeBuilder() => _containingType;

        internal ModuleBuilder GetModuleBuilder() => _module;

        #endregion

        #region Object Overrides

        public override bool Equals(object obj)
        {
            if (!(obj is MethodBuilder))
            {
                return false;
            }
            if (!_name.Equals(((MethodBuilder)obj)._name))
            {
                return false;
            }

            if (_attributes != ((MethodBuilder)obj)._attributes)
            {
                return false;
            }

            SignatureHelper thatSig = ((MethodBuilder)obj).GetMethodSignature();
            return thatSig.Equals(GetMethodSignature());
        }

        public override int GetHashCode() => _name.GetHashCode();

        public override string ToString()
        {
            var sb = new StringBuilder(1000);
            sb.Append("Name: " + _name + " " + Environment.NewLine);
            sb.Append("Attributes: " + (int)_attributes + Environment.NewLine);
            sb.Append("Method Signature: " + GetMethodSignature() + Environment.NewLine);
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        #endregion

        #region MemberInfo Overrides

        public override string Name => _name;

        internal int MetadataTokenInternal => GetToken().Token;

        public override Module Module => _containingType.Module;

        public override Type DeclaringType => _containingType._isHiddenGlobalType ? null : _containingType;

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => null;

        public override Type ReflectedType => DeclaringType;

        #endregion

        #region MethodBase Overrides

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override MethodImplAttributes GetMethodImplementationFlags() => _methodImplAttributes;

        public override MethodAttributes Attributes => _attributes;

        public override CallingConventions CallingConvention => _callingConvention;

        public override RuntimeMethodHandle MethodHandle
        {
            get => throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override bool IsSecurityCritical => true;

        public override bool IsSecuritySafeCritical => false;

        public override bool IsSecurityTransparent => false;

        #endregion

        #region MethodInfo Overrides

        public override MethodInfo GetBaseDefinition() => this;

        public override Type ReturnType => _returnType;

        public override ParameterInfo[] GetParameters()
        {
            if (!_isBaked || _containingType == null || _containingType.BakedRuntimeType == null)
            {
                throw new NotSupportedException(SR.InvalidOperation_TypeNotCreated);
            }

            MethodInfo rmi = _containingType.GetMethod(_name, _parameterTypes);
            return rmi.GetParameters();
        }

        public override ParameterInfo ReturnParameter
        {
            get
            {
                if (!_isBaked || _containingType == null || _containingType.BakedRuntimeType == null)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_TypeNotCreated);
                }

                MethodInfo rmi = _containingType.GetMethod(_name, _parameterTypes);
                return rmi.ReturnParameter;
            }
        }

        #endregion

        #region ICustomAttributeProvider Implementation

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

        #endregion

        #region Generic Members

        public override bool IsGenericMethodDefinition => _isGenericMethodDefinition;

        public override bool ContainsGenericParameters => throw new NotSupportedException();

        public override MethodInfo GetGenericMethodDefinition()
        {
            if (!IsGenericMethod)
            {
                throw new InvalidOperationException();
            }

            return this;
        }

        public override bool IsGenericMethod => _genericArguments != null;

        public override Type[] GetGenericArguments() => _genericArguments;

        public override MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
            return MethodBuilderInstantiation.MakeGenericMethod(this, typeArguments);
        }

        public GenericTypeParameterBuilder[] DefineGenericParameters(params string[] names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }
            if (names.Length == 0)
            {
                throw new ArgumentException(SR.Arg_EmptyArray, nameof(names));
            }
            if (_genericArguments != null)
            {
                throw new InvalidOperationException(SR.InvalidOperation_GenericParametersAlreadySet);
            }
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null)
                {
                    throw new ArgumentNullException(nameof(names));
                }
            }

            if (_methodToken.Token != 0)
            {
                throw new InvalidOperationException(SR.InvalidOperation_MethodBuilderBaked);
            }

            _isGenericMethodDefinition = true;
            _genericArguments = new GenericTypeParameterBuilder[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                _genericArguments[i] = new GenericTypeParameterBuilder(new TypeBuilder(names[i], i, this));
            }

            return _genericArguments;
        }

        internal void ThrowIfGeneric()
        {
            if (IsGenericMethod && !IsGenericMethodDefinition)
            {
                throw new InvalidOperationException();
            }
        }

        #endregion

        #region Public Members

        public MethodToken GetToken()
        {
            // We used to always "tokenize" a MethodBuilder when it is constructed. After change list 709498
            // we only "tokenize" a method when requested. But the order in which the methods are tokenized
            // didn't change: the same order the MethodBuilders are constructed. The recursion introduced
            // will overflow the stack when there are many methods on the same type (10000 in my experiment).
            // The change also introduced race conditions. Before the code change GetToken is called from
            // the MethodBuilder .ctor which is protected by lock(ModuleBuilder.SyncRoot). Now it
            // could be called more than once on the the same method introducing duplicate (invalid) tokens.
            // I don't fully understand this change. So I will keep the logic and only fix the recursion and 
            // the race condition.

            if (_methodToken.Token != 0)
            {
                return _methodToken;
            }

            MethodBuilder currentMethod = null;
            MethodToken currentToken = new MethodToken(0);
            int i;

            // We need to lock here to prevent a method from being "tokenized" twice.
            // We don't need to synchronize this with Type.DefineMethod because it only appends newly
            // constructed MethodBuilders to the end of m_listMethods
            lock (_containingType._methods)
            {
                if (_methodToken.Token != 0)
                {
                    return _methodToken;
                }

                // If m_tkMethod is still 0 when we obtain the lock, m_lastTokenizedMethod must be smaller
                // than the index of the current method.
                for (i = _containingType._lastTokenizedMethod + 1; i < _containingType._methods.Count; ++i)
                {
                    currentMethod = _containingType._methods[i];
                    currentToken = currentMethod.GetTokenNoLock();

                    if (currentMethod == this)
                        break;
                }

                _containingType._lastTokenizedMethod = i;
            }

            Debug.Assert(currentMethod == this, "We should have found this method in m_containingType.m_listMethods");
            Debug.Assert(currentToken.Token != 0, "The token should not be 0");

            return currentToken;
        }

        private MethodToken GetTokenNoLock()
        {
            Debug.Assert(_methodToken.Token == 0, "m_tkMethod should not have been initialized");

            byte[] sigBytes = GetMethodSignature().InternalGetSignature(out int sigLength);

            int token = TypeBuilder.DefineMethod(_module.GetNativeHandle(), _containingType.MetadataTokenInternal, _name, sigBytes, sigLength, Attributes);
            _methodToken = new MethodToken(token);

            if (_genericArguments != null)
            {
                foreach (GenericTypeParameterBuilder tb in _genericArguments)
                {
                    if (!tb._type.IsCreated())
                    {
                        tb._type.CreateType();
                    }
                }
            }

            TypeBuilder.SetMethodImpl(_module.GetNativeHandle(), token, _methodImplAttributes);
            return _methodToken;
        }

        public void SetParameters(params Type[] parameterTypes)
        {
            _module.CheckContext(parameterTypes);

            SetSignature(null, null, null, parameterTypes, null, null);
        }

        public void SetReturnType(Type returnType)
        {
            _module.CheckContext(returnType);

            SetSignature(returnType, null, null, null, null, null);
        }

        public void SetSignature(
            Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers,
            Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        {
            // We should throw InvalidOperation_MethodBuilderBaked here if the method signature has been baked.
            // But we cannot because that would be a breaking change from V2.
            if (_methodToken.Token != 0)
                return;

            _module.CheckContext(returnType);
            _module.CheckContext(returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers, parameterTypes);
            _module.CheckContext(parameterTypeRequiredCustomModifiers);
            _module.CheckContext(parameterTypeOptionalCustomModifiers);

            ThrowIfGeneric();

            if (returnType != null)
            {
                _returnType = returnType;
            }

            if (parameterTypes != null)
            {
                _parameterTypes = new Type[parameterTypes.Length];
                Array.Copy(parameterTypes, 0, _parameterTypes, 0, parameterTypes.Length);
            }

            _returnTypeRequiredCustomModifiers = returnTypeRequiredCustomModifiers;
            _returnTypeOptionalCustomModifiers = returnTypeOptionalCustomModifiers;
            _parameterTypeRequiredCustomModifiers = parameterTypeRequiredCustomModifiers;
            _parameterTypeOptionalCustomModifiers = parameterTypeOptionalCustomModifiers;
        }

        public ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string strParamName)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_ParamSequence);
            }

            ThrowIfGeneric();
            _containingType.ThrowIfCreated();

            if (position > 0 && (_parameterTypes == null || position > _parameterTypes.Length))
            {
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_ParamSequence);
            }

            attributes = attributes & ~ParameterAttributes.ReservedMask;
            return new ParameterBuilder(this, position, attributes, strParamName);
        }

        private struct SymCustomAttr
        {
            public string m_name;
            public byte[] m_data;
        }

        public void SetImplementationFlags(MethodImplAttributes attributes)
        {
            ThrowIfGeneric();

            _containingType.ThrowIfCreated();

            _methodImplAttributes = attributes;

            _canBeRuntimeImpl = true;

            TypeBuilder.SetMethodImpl(_module.GetNativeHandle(), MetadataTokenInternal, attributes);
        }

        public ILGenerator GetILGenerator()
        {
            ThrowIfGeneric();
            ThrowIfShouldNotHaveBody();

            return _ilGenerator ?? (_ilGenerator = new ILGenerator(this));
        }

        public ILGenerator GetILGenerator(int size)
        {
            ThrowIfGeneric();
            ThrowIfShouldNotHaveBody();

            return _ilGenerator ?? (_ilGenerator = new ILGenerator(this, size));
        }

        private void ThrowIfShouldNotHaveBody()
        {
            if ((_methodImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL ||
                (_methodImplAttributes & MethodImplAttributes.Unmanaged) != 0 ||
                (_attributes & MethodAttributes.PinvokeImpl) != 0 ||
                _isDllImport)
            {
                // cannot attach method body if methodimpl is marked not marked as managed IL
                throw new InvalidOperationException(SR.InvalidOperation_ShouldNotHaveMethodBody);
            }
        }

        /// <summary>
        /// Property is set to true if user wishes to have zero initialized stack frame for this
        /// method. Default to false.
        /// </summary>
        public bool InitLocals
        {
            get
            {
                ThrowIfGeneric();
                return _hasInitLocals;
            }
            set
            {
                ThrowIfGeneric();
                _hasInitLocals = value;
            }
        }

        public Module GetModule() => GetModuleBuilder();

        public string Signature => GetMethodSignature().ToString();

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

            ThrowIfGeneric();

            TypeBuilder.DefineCustomAttribute(_module, MetadataTokenInternal,
                _module.GetConstructorToken(con).Token,
                binaryAttribute,
                false, false);

            if (IsKnownCA(con))
            {
                ParseCA(con, binaryAttribute);
            }
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            if (customBuilder == null)
            {
                throw new ArgumentNullException(nameof(customBuilder));
            }

            ThrowIfGeneric();

            customBuilder.CreateCustomAttribute(_module, MetadataTokenInternal);

            if (IsKnownCA(customBuilder._con))
            {
                ParseCA(customBuilder._con, customBuilder._blob);
            }
        }

        // This method should return true for any and every ca that requires more work
        // than just setting the ca
        private bool IsKnownCA(ConstructorInfo con)
        {
            Type caType = con.DeclaringType;
            if (caType == typeof(MethodImplAttribute))
            {
                return true;
            }
            else if (caType == typeof(DllImportAttribute))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ParseCA(ConstructorInfo con, byte[] blob)
        {
            Type caType = con.DeclaringType;
            if (caType == typeof(MethodImplAttribute))
            {
                // dig through the blob looking for the MethodImplAttributes flag
                // that must be in the MethodCodeType field

                // for now we simply set a flag that relaxes the check when saving and
                // allows this method to have no body when any kind of MethodImplAttribute is present
                _canBeRuntimeImpl = true;
            }
            else if (caType == typeof(DllImportAttribute))
            {
                _canBeRuntimeImpl = true;
                _isDllImport = true;
            }
        }

        #endregion
    }

    internal class LocalSymInfo
    {
        // This class tracks the local variable's debugging information 
        // and namespace information with a given active lexical scope.

        #region Internal Data Members
        internal string[] m_strName;
        internal byte[][] m_ubSignature;
        internal int[] m_iLocalSlot;
        internal int[] m_iStartOffset;
        internal int[] m_iEndOffset;
        internal int m_iLocalSymCount;         // how many entries in the arrays are occupied
        internal string[] m_namespace;
        internal int m_iNameSpaceCount;
        internal const int InitialSize = 16;
        #endregion

        #region Constructor
        internal LocalSymInfo()
        {
            // initialize data variables
            m_iLocalSymCount = 0;
            m_iNameSpaceCount = 0;
        }
        #endregion

        #region Private Members
        private void EnsureCapacityNamespace()
        {
            if (m_iNameSpaceCount == 0)
            {
                m_namespace = new string[InitialSize];
            }
            else if (m_iNameSpaceCount == m_namespace.Length)
            {
                string[] strTemp = new string[checked(m_iNameSpaceCount * 2)];
                Array.Copy(m_namespace, 0, strTemp, 0, m_iNameSpaceCount);
                m_namespace = strTemp;
            }
        }

        private void EnsureCapacity()
        {
            if (m_iLocalSymCount == 0)
            {
                // First time. Allocate the arrays.
                m_strName = new string[InitialSize];
                m_ubSignature = new byte[InitialSize][];
                m_iLocalSlot = new int[InitialSize];
                m_iStartOffset = new int[InitialSize];
                m_iEndOffset = new int[InitialSize];
            }
            else if (m_iLocalSymCount == m_strName.Length)
            {
                // the arrays are full. Enlarge the arrays
                // why aren't we just using lists here?
                int newSize = checked(m_iLocalSymCount * 2);
                int[] temp = new int[newSize];
                Array.Copy(m_iLocalSlot, 0, temp, 0, m_iLocalSymCount);
                m_iLocalSlot = temp;

                temp = new int[newSize];
                Array.Copy(m_iStartOffset, 0, temp, 0, m_iLocalSymCount);
                m_iStartOffset = temp;

                temp = new int[newSize];
                Array.Copy(m_iEndOffset, 0, temp, 0, m_iLocalSymCount);
                m_iEndOffset = temp;

                string[] strTemp = new string[newSize];
                Array.Copy(m_strName, 0, strTemp, 0, m_iLocalSymCount);
                m_strName = strTemp;

                byte[][] ubTemp = new byte[newSize][];
                Array.Copy(m_ubSignature, 0, ubTemp, 0, m_iLocalSymCount);
                m_ubSignature = ubTemp;
            }
        }

        #endregion

        #region Internal Members
        internal void AddLocalSymInfo(string strName, byte[] signature, int slot, int startOffset, int endOffset)
        {
            // make sure that arrays are large enough to hold addition info
            EnsureCapacity();
            m_iStartOffset[m_iLocalSymCount] = startOffset;
            m_iEndOffset[m_iLocalSymCount] = endOffset;
            m_iLocalSlot[m_iLocalSymCount] = slot;
            m_strName[m_iLocalSymCount] = strName;
            m_ubSignature[m_iLocalSymCount] = signature;
            checked { m_iLocalSymCount++; }
        }

        internal void AddUsingNamespace(string strNamespace)
        {
            EnsureCapacityNamespace();
            m_namespace[m_iNameSpaceCount] = strNamespace;
            checked { m_iNameSpaceCount++; }
        }

        internal virtual void EmitLocalSymInfo(ISymbolWriter symWriter)
        {
            int i;

            for (i = 0; i < m_iLocalSymCount; i++)
            {
                symWriter.DefineLocalVariable(
                            m_strName[i],
                            FieldAttributes.PrivateScope,
                            m_ubSignature[i],
                            SymAddressKind.ILOffset,
                            m_iLocalSlot[i],
                            0,          // addr2 is not used yet
                            0,          // addr3 is not used
                            m_iStartOffset[i],
                            m_iEndOffset[i]);
            }
            for (i = 0; i < m_iNameSpaceCount; i++)
            {
                symWriter.UsingNamespace(m_namespace[i]);
            }
        }

        #endregion
    }

    /// <summary>
    /// Describes exception handler in a method body.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ExceptionHandler : IEquatable<ExceptionHandler>
    {
        // Keep in sync with unmanged structure. 
        internal readonly int m_exceptionClass;
        internal readonly int m_tryStartOffset;
        internal readonly int m_tryEndOffset;
        internal readonly int m_filterOffset;
        internal readonly int m_handlerStartOffset;
        internal readonly int m_handlerEndOffset;
        internal readonly ExceptionHandlingClauseOptions m_kind;

        #region Constructors

        internal ExceptionHandler(int tryStartOffset, int tryEndOffset, int filterOffset, int handlerStartOffset, int handlerEndOffset,
            int kind, int exceptionTypeToken)
        {
            Debug.Assert(tryStartOffset >= 0);
            Debug.Assert(tryEndOffset >= 0);
            Debug.Assert(filterOffset >= 0);
            Debug.Assert(handlerStartOffset >= 0);
            Debug.Assert(handlerEndOffset >= 0);
            Debug.Assert(IsValidKind((ExceptionHandlingClauseOptions)kind));
            Debug.Assert(kind != (int)ExceptionHandlingClauseOptions.Clause || (exceptionTypeToken & 0x00FFFFFF) != 0);

            m_tryStartOffset = tryStartOffset;
            m_tryEndOffset = tryEndOffset;
            m_filterOffset = filterOffset;
            m_handlerStartOffset = handlerStartOffset;
            m_handlerEndOffset = handlerEndOffset;
            m_kind = (ExceptionHandlingClauseOptions)kind;
            m_exceptionClass = exceptionTypeToken;
        }

        private static bool IsValidKind(ExceptionHandlingClauseOptions kind)
        {
            switch (kind)
            {
                case ExceptionHandlingClauseOptions.Clause:
                case ExceptionHandlingClauseOptions.Filter:
                case ExceptionHandlingClauseOptions.Finally:
                case ExceptionHandlingClauseOptions.Fault:
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region Equality

        public override int GetHashCode()
        {
            return m_exceptionClass ^ m_tryStartOffset ^ m_tryEndOffset ^ m_filterOffset ^ m_handlerStartOffset ^ m_handlerEndOffset ^ (int)m_kind;
        }

        public override bool Equals(object obj)
        {
            return obj is ExceptionHandler && Equals((ExceptionHandler)obj);
        }

        public bool Equals(ExceptionHandler other)
        {
            return
                other.m_exceptionClass == m_exceptionClass &&
                other.m_tryStartOffset == m_tryStartOffset &&
                other.m_tryEndOffset == m_tryEndOffset &&
                other.m_filterOffset == m_filterOffset &&
                other.m_handlerStartOffset == m_handlerStartOffset &&
                other.m_handlerEndOffset == m_handlerEndOffset &&
                other.m_kind == m_kind;
        }

        public static bool operator ==(ExceptionHandler left, ExceptionHandler right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ExceptionHandler left, ExceptionHandler right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}










