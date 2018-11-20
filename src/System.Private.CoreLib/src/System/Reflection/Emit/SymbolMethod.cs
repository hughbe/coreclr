// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    internal sealed class SymbolMethod : MethodInfo
    {
        private readonly ModuleBuilder _module;
        private readonly Type _containingType;
        private readonly string _name;
        private readonly CallingConventions _callingConvention;
        private readonly Type _returnType;
        private MethodToken _methodToken;
        private readonly Type[] _parameterTypes;
        private readonly SignatureHelper _signature;

        internal SymbolMethod(ModuleBuilder mod, MethodToken token, Type arrayClass, string methodName,
            CallingConventions callingConvention, Type returnType, Type[] parameterTypes)
        {
            // This is a kind of MethodInfo to represent methods for array type of unbaked type

            // Another way to look at this class is as a glorified MethodToken wrapper. At the time of this comment
            // this class is only constructed inside ModuleBuilder.GetArrayMethod and the only interesting thing 
            // passed into it is this MethodToken. The MethodToken was forged using a TypeSpec for an Array type and
            // the name of the method on Array. 
            // As none of the methods on Array have CustomModifiers their is no need to pass those around in here.
            _methodToken = token;

            // The ParameterTypes are also a bit interesting in that they may be unbaked TypeBuilders.
            _returnType = returnType;
            if (parameterTypes != null)
            {
                _parameterTypes = new Type[parameterTypes.Length];
                Array.Copy(parameterTypes, 0, _parameterTypes, 0, parameterTypes.Length);
            }
            else
            {
                _parameterTypes = Array.Empty<Type>();
            }

            _module = mod;
            _containingType = arrayClass;
            _name = methodName;
            _callingConvention = callingConvention;

            _signature = SignatureHelper.GetMethodSigHelper(
                mod, callingConvention, returnType, null, null, parameterTypes, null, null);
        }

        internal override Type[] GetParameterTypes() => _parameterTypes;

        internal MethodToken GetToken(ModuleBuilder mod)
        {
            return mod.GetArrayMethodToken(_containingType, _name, _callingConvention, _returnType, _parameterTypes);
        }
        
        public override Module Module
        {
            get { return _module; }
        }

        public override Type ReflectedType => _containingType as Type;

        public override string Name => _name;

        public override Type DeclaringType => _containingType;

        public override ParameterInfo[] GetParameters()
        {
            throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override MethodAttributes Attributes
        {
            get => throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override CallingConventions CallingConvention => _callingConvention;

        public override RuntimeMethodHandle MethodHandle
        {
            get => throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override Type ReturnType => _returnType;

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => null;

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override MethodInfo GetBaseDefinition() => this;

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotSupportedException(SR.NotSupported_SymbolMethod);
        }

        public Module GetModule() => _module;

        public MethodToken GetToken() => _methodToken;
    }
}
