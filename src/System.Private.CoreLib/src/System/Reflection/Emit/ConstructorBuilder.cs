// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    public sealed class ConstructorBuilder : ConstructorInfo
    {
        private readonly MethodBuilder _methodBuilder;
        internal bool _isDefaultConstructor;

        #region Constructor

        internal ConstructorBuilder(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type[] parameterTypes, Type[][] requiredCustomModifiers, Type[][] optionalCustomModifiers, ModuleBuilder mod, TypeBuilder type)
        {
            _methodBuilder = new MethodBuilder(name, attributes, callingConvention, null, null, null,
                parameterTypes, requiredCustomModifiers, optionalCustomModifiers, mod, type, false);

            type._methods.Add(_methodBuilder);

            byte[] sigBytes = _methodBuilder.GetMethodSignature().InternalGetSignature(out int sigLength);
            MethodToken token = _methodBuilder.GetToken();
        }

        internal ConstructorBuilder(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type[] parameterTypes, ModuleBuilder mod, TypeBuilder type) :
            this(name, attributes, callingConvention, parameterTypes, null, null, mod, type)
        {
        }

        #endregion

        #region Internal

        internal override Type[] GetParameterTypes() => _methodBuilder.GetParameterTypes();

        private TypeBuilder GetTypeBuilder() => _methodBuilder.GetTypeBuilder();

        #endregion

        #region Object Overrides

        public override string ToString() => _methodBuilder.ToString();

        #endregion

        #region MemberInfo Overrides

        internal int MetadataTokenInternal => _methodBuilder.MetadataTokenInternal;

        public override Module Module => _methodBuilder.Module;

        public override Type ReflectedType => _methodBuilder.ReflectedType;

        public override Type DeclaringType => _methodBuilder.DeclaringType;

        public override string Name => _methodBuilder.Name;

        #endregion

        #region MethodBase Overrides

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override ParameterInfo[] GetParameters()
        {
            ConstructorInfo rci = GetTypeBuilder().GetConstructor(_methodBuilder._parameterTypes);
            return rci.GetParameters();
        }

        public override MethodAttributes Attributes => _methodBuilder.Attributes;

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return _methodBuilder.GetMethodImplementationFlags();
        }

        public override RuntimeMethodHandle MethodHandle => _methodBuilder.MethodHandle;

        #endregion

        #region ConstructorInfo Overrides

        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        #endregion

        #region ICustomAttributeProvider Implementation

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _methodBuilder.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _methodBuilder.GetCustomAttributes(attributeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _methodBuilder.IsDefined(attributeType, inherit);
        }

        #endregion

        #region Public Members

        public MethodToken GetToken() => _methodBuilder.GetToken();

        public ParameterBuilder DefineParameter(int iSequence, ParameterAttributes attributes, string strParamName)
        {
            // MD will assert if we try to set the reserved bits explicitly
            attributes = attributes & ~ParameterAttributes.ReservedMask;
            return _methodBuilder.DefineParameter(iSequence, attributes, strParamName);
        }

        public ILGenerator GetILGenerator()
        {
            if (_isDefaultConstructor)
            {
                throw new InvalidOperationException(SR.InvalidOperation_DefaultConstructorILGen);
            }

            return _methodBuilder.GetILGenerator();
        }

        public ILGenerator GetILGenerator(int streamSize)
        {
            if (_isDefaultConstructor)
            {
                throw new InvalidOperationException(SR.InvalidOperation_DefaultConstructorILGen);
            }

            return _methodBuilder.GetILGenerator(streamSize);
        }

        public override CallingConventions CallingConvention
        {
            get => DeclaringType.IsGenericType ? CallingConventions.HasThis : CallingConventions.Standard;
        }

        public Module GetModule() => _methodBuilder.GetModule();

        // This always returns null. Is that what we want?
        internal override Type GetReturnType() => _methodBuilder.ReturnType;

        public string Signature => _methodBuilder.Signature;

        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
            _methodBuilder.SetCustomAttribute(con, binaryAttribute);
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            _methodBuilder.SetCustomAttribute(customBuilder);
        }

        public void SetImplementationFlags(MethodImplAttributes attributes)
        {
            _methodBuilder.SetImplementationFlags(attributes);
        }

        public bool InitLocals
        {
            get => _methodBuilder.InitLocals;
            set => _methodBuilder.InitLocals = value;
        }

        #endregion
    }
}
