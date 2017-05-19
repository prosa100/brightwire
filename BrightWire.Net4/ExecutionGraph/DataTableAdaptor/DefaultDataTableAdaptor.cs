﻿using BrightWire.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrightWire.ExecutionGraph.DataTableAdaptor
{
    class DefaultDataTableAdaptor : DataTableAdaptorBase
    {
        readonly IDataTableVectoriser _vectoriser;

        public DefaultDataTableAdaptor(ILinearAlgebraProvider lap, IDataTable dataTable, IDataTableVectoriser vectoriser = null)
            : base(lap, dataTable)
        {
            _vectoriser = vectoriser ?? dataTable.GetVectoriser(true);
        }

        public override IDataSource CloneWith(IDataTable dataTable)
        {
            return new DefaultDataTableAdaptor(_lap, dataTable, _vectoriser);
        }

        public override int InputSize => _vectoriser.InputSize;
        public override int OutputSize => _vectoriser.OutputSize;
        public override bool IsSequential => false;

        public override IMiniBatch Get(IReadOnlyList<int> rows)
        {
            var data = _GetRows(rows)
                .Select(r => (_vectoriser.GetInput(r).Data, _vectoriser.GetOutput(r).Data))
                .ToList()
            ;
            return _GetMiniBatch(rows, data);
        }

        //public string GetOutputLabel(int columnIndex, int vectorIndex)
        //{
        //    return _vectoriser.GetOutputLabel(columnIndex, vectorIndex);
        //}
    }
}