// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection.Emit
{
    internal sealed class MethodBuilderInstantiation : MethodInfo
    {
        internal MethodInfo _method;
        private Type[] _inst;

        internal static MethodInfo MakeGenericMethod(MethodInfo method, Type[] inst)
        {
            if (!method.IsGenericMethodDefinition)
            {
                throw new InvalidOperationException();
            }

            return new MethodBuilderInstantiation(method, inst);
        }

        internal MethodBuilderInstantiation(MethodInfo method, Type[] inst)
        {
            _method = method;
            _inst = inst;
        }

        internal override Type[] GetParameterTypes() => _method.GetParameterTypes();

        public override MemberTypes MemberType => _method.MemberType;

        public override string Name => _method.Name;

        public override Type DeclaringType => _method.DeclaringType;

        public override Type ReflectedType => _method.ReflectedType;

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _method.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _method.GetCustomAttributes(attributeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit) { return _method.IsDefined(attributeType, inherit); }

        public override Module Module { get { return _method.Module; } }

        public override ParameterInfo[] GetParameters() => throw new NotSupportedException();

        public override MethodImplAttributes GetMethodImplementationFlags() => _method.GetMethodImplementationFlags();

        public override RuntimeMethodHandle MethodHandle => throw new NotSupportedException(SR.NotSupported_DynamicModule);

        public override MethodAttributes Attributes => _method.Attributes;

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override CallingConventions CallingConvention => _method.CallingConvention;

        public override Type[] GetGenericArguments() => _inst;

        public override MethodInfo GetGenericMethodDefinition() => _method;

        public override bool IsGenericMethodDefinition => false;
        public override bool ContainsGenericParameters
        {
            get
            {
                for (int i = 0; i < _inst.Length; i++)
                {
                    if (_inst[i].ContainsGenericParameters)
                    {
                        return true;
                    }
                }

                return DeclaringType != null && DeclaringType.ContainsGenericParameters;
            }
        }

        public override MethodInfo MakeGenericMethod(params Type[] arguments)
        {
            throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericMethodDefinition, this));
        }

        public override bool IsGenericMethod { get { return true; } }

        public override Type ReturnType => _method.ReturnType;

        public override ParameterInfo ReturnParameter => throw new NotSupportedException();

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotSupportedException();

        public override MethodInfo GetBaseDefinition() => throw new NotSupportedException();
    }
}
