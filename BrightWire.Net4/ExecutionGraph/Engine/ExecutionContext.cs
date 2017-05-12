﻿using BrightWire.ExecutionGraph.Helper;
using System;
using System.Collections.Generic;
using System.Text;

namespace BrightWire.ExecutionGraph.Engine
{
    class ExecutionContext : IExecutionContext
    {
        readonly List<IGraphOperation> _operationList = new List<IGraphOperation>();
        readonly Dictionary<string, IMatrix> _memory = new Dictionary<string, IMatrix>();
        readonly ILinearAlgebraProvider _lap;
        IGraphData _data;

        public ExecutionContext(ILinearAlgebraProvider lap)
        {
            _lap = lap;
        }

        public ILinearAlgebraProvider LinearAlgebraProvider => _lap;
        public IGraphData Data
        {
            get { return _data; }
            set { _data = value; }
        }

        public void Add(IReadOnlyList<IGraphOperation> operations) => _operationList.AddRange(operations);
        public void Add(IGraphOperation operation) => _operationList.Add(operation);

        public IMatrix GetMemory(string index)
        {
            return _memory[index];
        }

        public IGraphOperation GetNextOperation()
        {
            for (int i = 0, len = _operationList.Count; i < len; i++) {
                var operation = _operationList[i];
                _operationList.RemoveAt(i);
                return operation;
            }
            return null;
        }

        public void SetMemory(string index, IMatrix memory)
        {
            _memory[index] = memory;
        }
    }
}