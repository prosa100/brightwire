﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrightWire.ExecutionGraph.Node
{
    abstract class BackpropagationBase : IBackpropagation
    {
        public abstract void Backward(IGraphData errorSignal, IContext context, IReadOnlyList<INode> parents);

        #region Disposal
        ~BackpropagationBase()
        {
            _Dispose(false);
        }
        public void Dispose()
        {
            _Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void _Dispose(bool isDisposing) { }
        #endregion
    }
}