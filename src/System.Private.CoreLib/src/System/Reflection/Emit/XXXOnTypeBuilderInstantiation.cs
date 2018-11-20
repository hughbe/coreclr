// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Diagnostics;

namespace System.Reflection.Emit
{
    internal sealed class MethodOnTypeBuilderInstantiation : MethodInfo
    {
        internal static MethodInfo GetMethod(MethodInfo method, TypeBuilderInstantiation type)
        {
            return new MethodOnTypeBuilderInstantiation(method, type);
        }

        internal MethodInfo _method;
        private readonly TypeBuilderInstantiation _type;

        internal MethodOnTypeBuilderInstantiation(MethodInfo method, TypeBuilderInstantiation type)
        {
            Debug.Assert(method is MethodBuilder || method is RuntimeMethodInfo);

            _method = method;
            _type = type;
        }

        internal override Type[] GetParameterTypes() => _method.GetParameterTypes();

        public override MemberTypes MemberType => _method.MemberType;

        public override string Name => _method.Name;

        public override Type DeclaringType => _type;

        public override Type ReflectedType { get { return _type; } }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _method.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _method.GetCustomAttributes(attributeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _method.IsDefined(attributeType, inherit);
        }

        public override Module Module { get { return _method.Module; } }

        public override ParameterInfo[] GetParameters() => _method.GetParameters();

        public override MethodImplAttributes GetMethodImplementationFlags() => _method.GetMethodImplementationFlags();

        public override RuntimeMethodHandle MethodHandle => _method.MethodHandle;

        public override MethodAttributes Attributes => _method.Attributes;

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override CallingConventions CallingConvention => _method.CallingConvention;

        public override Type[] GetGenericArguments() => _method.GetGenericArguments();

        public override MethodInfo GetGenericMethodDefinition() => _method;

        public override bool IsGenericMethodDefinition => _method.IsGenericMethodDefinition;

        public override bool ContainsGenericParameters => _method.ContainsGenericParameters;

        public override MethodInfo MakeGenericMethod(params Type[] typeArgs)
        {
            if (!IsGenericMethodDefinition)
            {
                throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericMethodDefinition, this));
            }

            return MethodBuilderInstantiation.MakeGenericMethod(this, typeArgs);
        }

        public override bool IsGenericMethod => _method.IsGenericMethod;

        public override Type ReturnType => _method.ReturnType;

        public override ParameterInfo ReturnParameter => throw new NotSupportedException();

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotSupportedException();

        public override MethodInfo GetBaseDefinition() => throw new NotSupportedException();
    }

    internal sealed class ConstructorOnTypeBuilderInstantiation : ConstructorInfo
    {
        internal static ConstructorInfo GetConstructor(ConstructorInfo Constructor, TypeBuilderInstantiation type)
        {
            return new ConstructorOnTypeBuilderInstantiation(Constructor, type);
        }

        internal ConstructorInfo _ctor;
        private readonly TypeBuilderInstantiation _type;

        internal ConstructorOnTypeBuilderInstantiation(ConstructorInfo constructor, TypeBuilderInstantiation type)
        {
            Debug.Assert(constructor is ConstructorBuilder || constructor is RuntimeConstructorInfo);

            _ctor = constructor;
            _type = type;
        }

        internal override Type[] GetParameterTypes() => _ctor.GetParameterTypes();

        internal override Type GetReturnType() => DeclaringType;

        public override MemberTypes MemberType => _ctor.MemberType;

        public override string Name => _ctor.Name;

        public override Type DeclaringType => _type;

        public override Type ReflectedType => _type;

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _ctor.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _ctor.GetCustomAttributes(attributeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _ctor.IsDefined(attributeType, inherit);
        }

        internal int MetadataTokenInternal
        {
            get
            {
                if (_ctor is ConstructorBuilder cb)
                {
                    return cb.MetadataTokenInternal;
                }

                Debug.Assert(_ctor is RuntimeConstructorInfo);
                return _ctor.MetadataToken;
            }
        }

        public override Module Module => _ctor.Module;

        public override ParameterInfo[] GetParameters() => _ctor.GetParameters();

        public override MethodImplAttributes GetMethodImplementationFlags() => _ctor.GetMethodImplementationFlags();

        public override RuntimeMethodHandle MethodHandle => _ctor.MethodHandle;

        public override MethodAttributes Attributes => _ctor.Attributes;

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override CallingConventions CallingConvention => _ctor.CallingConvention;

        public override Type[] GetGenericArguments() => _ctor.GetGenericArguments();

        public override bool IsGenericMethodDefinition => false;

        public override bool ContainsGenericParameters => false;

        public override bool IsGenericMethod => false;

        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new InvalidOperationException();
        }
    }

    internal sealed class FieldOnTypeBuilderInstantiation : FieldInfo
    {
        internal static FieldInfo GetField(FieldInfo Field, TypeBuilderInstantiation type)
        {
            // There is a pre-existing race condition in this code with the side effect
            // that the second thread's value clobbers the first in the hashtable. This is 
            // an acceptable race condition since we make no guarantees that this will return the
            // same object.
            //
            // We're not entirely sure if this cache helps any specific scenarios, so 
            // long-term, one could investigate whether it's needed. In any case, this
            // method isn't expected to be on any critical paths for performance.
            if (type._hashtable.Contains(Field))
            {
                return type._hashtable[Field] as FieldInfo;
            }

            FieldInfo m = new FieldOnTypeBuilderInstantiation(Field, type);
            type._hashtable[Field] = m;
            return m;
        }

        private FieldInfo _field;
        private readonly TypeBuilderInstantiation _type;

        internal FieldOnTypeBuilderInstantiation(FieldInfo field, TypeBuilderInstantiation type)
        {
            Debug.Assert(field is FieldBuilder || field is RuntimeFieldInfo);

            _field = field;
            _type = type;
        }

        internal FieldInfo FieldInfo { get { return _field; } }

        public override MemberTypes MemberType => MemberTypes.Field;

        public override string Name => _field.Name;

        public override Type DeclaringType => _type;

        public override Type ReflectedType => _type;

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _field.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _field.GetCustomAttributes(attributeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _field.IsDefined(attributeType, inherit);
        }

        internal int MetadataTokenInternal
        {
            get
            {
                if (_field is FieldBuilder fb)
                {
                    return fb.MetadataTokenInternal;
                }

                Debug.Assert(_field is RuntimeFieldInfo);
                return _field.MetadataToken;
            }
        }

        public override Module Module => _field.Module;

        public override Type[] GetRequiredCustomModifiers() => _field.GetRequiredCustomModifiers();

        public override Type[] GetOptionalCustomModifiers() { return _field.GetOptionalCustomModifiers(); }

        public override void SetValueDirect(TypedReference obj, object value)
        {
            throw new NotImplementedException();
        }

        public override object GetValueDirect(TypedReference obj) => throw new NotImplementedException();

        public override RuntimeFieldHandle FieldHandle => throw new NotImplementedException();

        public override Type FieldType => throw new NotImplementedException();

        public override object GetValue(object obj) => throw new InvalidOperationException();

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            throw new InvalidOperationException();
        }

        public override FieldAttributes Attributes { get { return _field.Attributes; } }
    }
}
