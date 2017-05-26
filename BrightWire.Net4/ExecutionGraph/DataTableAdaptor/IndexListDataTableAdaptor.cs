﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BrightWire.Models;

namespace BrightWire.ExecutionGraph.DataTableAdaptor
{
    class IndexListDataTableAdaptor : DataTableAdaptorBase<(IndexList, FloatVector)>, IIndexListEncoder
    {
        readonly int _inputSize;
        readonly int _outputSize;
        readonly IDataTableVectoriser _vectoriser;

        public IndexListDataTableAdaptor(ILinearAlgebraProvider lap, IDataTable dataTable, IDataTableVectoriser vectoriser)
            : base(lap, dataTable)
        {
            _vectoriser = vectoriser;
            var columnInfo = _vectoriser.Analysis.ColumnInfo.First() as IIndexColumnInfo;
            _inputSize = Convert.ToInt32(columnInfo.MaxIndex + 1);
            _outputSize = _vectoriser.OutputSize;

            // load the data
            dataTable.ForEach(row => _data.Add((row.GetField<IndexList>(0), _vectoriser.GetOutput(row))));
        }

        public override bool IsSequential => false;
        public override int InputSize => _inputSize;
        public override int OutputSize => _outputSize;
        public override int RowCount => _data.Count;

        public float[] Encode(IndexList indexList)
        {
            var ret = new float[_inputSize];
            foreach (var group in indexList.Index.GroupBy(d => d))
                ret[group.Key] = group.Count();
            return ret;
        }

        public override IMiniBatch Get(IExecutionContext executionContext, IReadOnlyList<int> rows)
        {
            var data = _GetRows(rows)
                .Select(r => (Encode(r.Item1), r.Item2.Data))
                .ToList()
            ;
            return _GetMiniBatch(rows, data);
        }

        public override IDataSource CloneWith(IDataTable dataTable)
        {
            return new IndexListDataTableAdaptor(_lap, dataTable, _vectoriser);
        }
    }
}