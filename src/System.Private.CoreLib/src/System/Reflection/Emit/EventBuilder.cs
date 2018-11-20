// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Emit
{
    /// <summary>
    /// A EventBuilder is always associated with a TypeBuilder. The TypeBuilder.DefineEvent
    /// method will return a new EventBuilder to a client.
    /// </summary>
    public sealed class EventBuilder
    {
        private readonly string _name;
        private EventToken _eventToken;
        private ModuleBuilder _module;
        private readonly EventAttributes _attributes;
        private TypeBuilder _type;

        internal EventBuilder(ModuleBuilder mod, string name, EventAttributes attr, TypeBuilder type, EventToken evToken)
        {
            _name = name;
            _module = mod;
            _attributes = attr;
            _eventToken = evToken;
            _type = type;
        }

        public EventToken GetEventToken() => _eventToken;

        private void SetMethodSemantics(MethodBuilder mdBuilder, MethodSemanticsAttributes semantics)
        {
            if (mdBuilder == null)
            {
                throw new ArgumentNullException(nameof(mdBuilder));
            }

            _type.ThrowIfCreated();
            TypeBuilder.DefineMethodSemantics(
                _module.GetNativeHandle(),
                _eventToken.Token,
                semantics,
                mdBuilder.GetToken().Token);
        }

        public void SetAddOnMethod(MethodBuilder mdBuilder)
        {
            SetMethodSemantics(mdBuilder, MethodSemanticsAttributes.AddOn);
        }

        public void SetRemoveOnMethod(MethodBuilder mdBuilder)
        {
            SetMethodSemantics(mdBuilder, MethodSemanticsAttributes.RemoveOn);
        }

        public void SetRaiseMethod(MethodBuilder mdBuilder)
        {
            SetMethodSemantics(mdBuilder, MethodSemanticsAttributes.Fire);
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

            _type.ThrowIfCreated();

            TypeBuilder.DefineCustomAttribute(
                _module,
                _eventToken.Token,
                _module.GetConstructorToken(con).Token,
                binaryAttribute,
                false, false);
        }

        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            if (customBuilder == null)
            {
                throw new ArgumentNullException(nameof(customBuilder));
            }

            _type.ThrowIfCreated();
            customBuilder.CreateCustomAttribute(_module, _eventToken.Token);
        }
    }
}
