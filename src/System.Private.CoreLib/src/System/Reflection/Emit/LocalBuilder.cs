// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Emit
{
    public sealed class LocalBuilder : LocalVariableInfo
    {
        private readonly int _index;
        private readonly Type _type;
        private readonly MethodInfo _methodBuilder;
        private readonly bool _isPinned;

        internal LocalBuilder(int localIndex, Type localType, MethodInfo methodBuilder)
            : this(localIndex, localType, methodBuilder, false)
        {
        }

        internal LocalBuilder(int localIndex, Type localType, MethodInfo methodBuilder, bool isPinned)
        {
            _isPinned = isPinned;
            _index = localIndex;
            _type = localType;
            _methodBuilder = methodBuilder;
        }

        internal int GetLocalIndex() => _index;

        internal MethodInfo GetMethodBuilder() => _methodBuilder;

        public override bool IsPinned => _isPinned;

        public override Type LocalType => _type;

        public override int LocalIndex => _index;

        public void SetLocalSymInfo(string name)
        {
            SetLocalSymInfo(name, 0, 0);
        }

        public void SetLocalSymInfo(string name, int startOffset, int endOffset)
        {
            MethodBuilder methodBuilder = _methodBuilder as MethodBuilder;
            if (methodBuilder == null)
            {
                // it's a light code gen entity
                throw new NotSupportedException();
            }

            ModuleBuilder dynMod = (ModuleBuilder)methodBuilder.Module;
            if (methodBuilder.IsTypeCreated())
            {
                // cannot change method after its containing type has been created
                throw new InvalidOperationException(SR.InvalidOperation_TypeHasBeenCreated);
            }

            // set the name and range of offset for the local
            if (dynMod.GetSymWriter() == null)
            {
                // cannot set local name if not debug module
                throw new InvalidOperationException(SR.InvalidOperation_NotADebugModule);
            }

            SignatureHelper sigHelp = SignatureHelper.GetFieldSigHelper(dynMod);
            sigHelp.AddArgument(_type);
            byte[] signature = sigHelp.InternalGetSignature(out int sigLength);

            // The symbol store doesn't want the calling convention on the
            // front of the signature, but InternalGetSignature returns
            // the callinging convention. So we strip it off. This is a
            // bit unfortunate, since it means that we need to allocate
            // yet another array of bytes...  
            byte[] mungedSig = new byte[sigLength - 1];
            Buffer.BlockCopy(signature, 1, mungedSig, 0, sigLength - 1);

            int index = methodBuilder.GetILGenerator().m_ScopeTree.GetCurrentActiveScopeIndex();
            if (index == -1)
            {
                // top level scope information is kept with methodBuilder
                methodBuilder._localSymbolInfo.AddLocalSymInfo(
                     name,
                     mungedSig,
                     _index,
                     startOffset,
                     endOffset);
            }
            else
            {
                methodBuilder.GetILGenerator().m_ScopeTree.AddLocalSymInfoToCurrentScope(
                     name,
                     mungedSig,
                     _index,
                     startOffset,
                     endOffset);
            }
        }
    }
}
