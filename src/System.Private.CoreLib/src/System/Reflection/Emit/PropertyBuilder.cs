// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    /// <summary> 
    /// A PropertyBuilder is always associated with a TypeBuilder.  The TypeBuilder.DefineProperty
    /// method will return a new PropertyBuilder to a client.
    /// </summary>
    public sealed class PropertyBuilder : PropertyInfo
    {
        private string _name;
        private PropertyToken _propertyToken;
        private ModuleBuilder _noduleBuilder;
        private SignatureHelper _signature;
        private PropertyAttributes _attributes;
        private Type _returnType;
        private MethodInfo _getMethod;
        private MethodInfo _setMethod;
        private TypeBuilder _containingType;

        internal PropertyBuilder(
            ModuleBuilder mod,
            string name,
            SignatureHelper sig,
            PropertyAttributes attr,
            Type returnType
            PropertyToken prToken,
            TypeBuilder containingType)
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

            _name = name;
            _noduleBuilder = mod;
            _signature = sig;
            _attributes = attr;
            _returnType = returnType;
            _propertyToken = prToken;
            _containingType = containingType;
        }

        /// <summary>
        /// Set the default value of the Property
        /// </summary>
        public void SetConstant(object defaultValue)
        {
            _containingType.ThrowIfCreated();

            TypeBuilder.SetConstantValue(
                _noduleBuilder,
                _propertyToken.Token,
                _returnType,
                defaultValue);
        }

        public PropertyToken PropertyToken => _propertyToken;

        public override Module Module => _containingType.Module;

        private void SetMethodSemantics(MethodBuilder mdBuilder, MethodSemanticsAttributes semantics)
        {
            if (mdBuilder == null)
            {
                throw new ArgumentNullException(nameof(mdBuilder));
            }

            _containingType.ThrowIfCreated();
            TypeBuilder.DefineMethodSemantics(
                _noduleBuilder.GetNativeHandle(),
                _propertyToken.Token,
                semantics,
                mdBuilder.GetToken().Token);
        }

        public void SetGetMethod(MethodBuilder mdBuilder)
        {
            SetMethodSemantics(mdBuilder, MethodSemanticsAttributes.Getter);
            _getMethod = mdBuilder;
        }

        public void SetSetMethod(MethodBuilder mdBuilder)
        {
            SetMethodSemantics(mdBuilder, MethodSemanticsAttributes.Setter);
            _setMethod = mdBuilder;
        }

        public void AddOtherMethod(MethodBuilder mdBuilder)
        {
            SetMethodSemantics(mdBuilder, MethodSemanticsAttributes.Other);
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

            _containingType.ThrowIfCreated();
            TypeBuilder.DefineCustomAttribute(
                _noduleBuilder,
                _propertyToken.Token,
                _noduleBuilder.GetConstructorToken(con).Token,
                binaryAttribute,
                false, false);
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            if (customBuilder == null)
            {
                throw new ArgumentNullException(nameof(customBuilder));
            }

            _containingType.ThrowIfCreated();
            customBuilder.CreateCustomAttribute(_noduleBuilder, _propertyToken.Token);
        }

        public override object GetValue(object obj, object[] index)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override void SetValue(object obj, object value, object[] index)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override MethodInfo GetGetMethod(bool nonPublic)
        {
            if (nonPublic || _getMethod == null)
            {
                return _getMethod;
            }
            if ((_getMethod.Attributes & MethodAttributes.Public) == MethodAttributes.Public)
            {
                return _getMethod;
            }

            return null;
        }

        public override MethodInfo GetSetMethod(bool nonPublic)
        {
            if (nonPublic || _setMethod == null)
            {
                return _setMethod;
            }
            if ((_setMethod.Attributes & MethodAttributes.Public) == MethodAttributes.Public)
            {
                return _setMethod;
            }

            return null;
        }

        public override ParameterInfo[] GetIndexParameters()
        {
            throw new NotSupportedException(SR.NotSupported_DynamicModule);
        }

        public override Type PropertyType => _returnType;

        public override PropertyAttributes Attributes => _attributes;

        public override bool CanRead => _getMethod != null;

        public override bool CanWrite => _setMethod != null;

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

        public override string Name => _name;

        public override Type DeclaringType => _containingType;

        public override Type ReflectedType => _containingType;
    }
}
