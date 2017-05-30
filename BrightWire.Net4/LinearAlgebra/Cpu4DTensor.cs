﻿using MathNet.Numerics.LinearAlgebra.Single;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrightWire.LinearAlgebra
{
    internal class Cpu4DTensor : I4DTensor
    {
        readonly Cpu3DTensor[] _data;
        readonly int _rows, _columns, _depth;

        public Cpu4DTensor(int rows, int columns, int depth, int count)
        {
            _rows = rows;
            _columns = columns;
            _depth = depth;
            _data = Enumerable.Range(0, count).Select(i => new Cpu3DTensor(rows, columns, depth)).ToArray();
        }

        public Cpu4DTensor(IReadOnlyList<I3DTensor> tensorList)
        {
            var first = tensorList.First();
            Debug.Assert(tensorList.All(m => m.RowCount == first.RowCount && m.ColumnCount == first.ColumnCount && m.Depth == first.Depth));
            _rows = first.RowCount;
            _columns = first.ColumnCount;
            _depth = first.Depth;
            _data = tensorList.Cast<Cpu3DTensor>().ToArray();
        }

        public int RowCount => _rows;

        public int ColumnCount => _columns;

        public int Depth => _depth;

        public int Count => _data.Length;

        public void Dispose()
        {
            // nop
        }

        public I3DTensor GetTensorAt(int index)
        {
            return _data[index];
        }

        public IReadOnlyList<IIndexable3DTensor> AsIndexable() => _data;

        public I4DTensor AddPadding(int padding)
        {
            var ret = new List<I3DTensor>();
            foreach (var item in _data)
                ret.Add(item.AddPadding(padding));
            return new Cpu4DTensor(ret);
        }

        public I4DTensor RemovePadding(int padding)
        {
            var ret = new List<I3DTensor>();
            foreach (var item in _data)
                ret.Add(item.RemovePadding(padding));
            return new Cpu4DTensor(ret);
        }

        public (I4DTensor Result, IReadOnlyList<IReadOnlyList<(object X, object Y)>> Index) MaxPool(int filterWidth, int filterHeight, int stride, bool calculateIndex)
        {
            List<IReadOnlyList<(object X, object Y)>> indexList = calculateIndex ? new List<IReadOnlyList<(object X, object Y)>>() : null;
            var ret = new List<I3DTensor>();
            for (var i = 0; i < Count; i++) {
                var result = GetTensorAt(i).MaxPool(filterWidth, filterHeight, stride, calculateIndex);
                if (calculateIndex)
                    indexList.Add(result.Index);
                ret.Add(result.Result);
            }
            return (new Cpu4DTensor(ret), indexList);
        }

        public I4DTensor ReverseMaxPool(int rows, int columns, IReadOnlyList<IReadOnlyList<(object X, object Y)>> indexList)
        {
            var ret = new List<I3DTensor>();
            for (var i = 0; i < Count; i++) {
                var result = GetTensorAt(i).ReverseMaxPool(rows, columns, indexList != null ? indexList[i] : null);
                ret.Add(result);
            }
            return new Cpu4DTensor(ret);
        }

        public I3DTensor Im2Col(int filterWidth, int filterHeight, int stride)
        {
            var ret = new List<IMatrix>();
            for (var i = 0; i < Count; i++) {
                var result = GetTensorAt(i).Im2Col(filterWidth, filterHeight, stride);
                ret.Add(result);
            }
            return new Cpu3DTensor(ret);
        }

        public I3DTensor ReverseIm2Col(IReadOnlyList<IReadOnlyList<IVector>> filter, int inputHeight, int inputWidth, int inputDepth, int padding, int filterHeight, int filterWidth, int stride)
        {
            var ret = new List<IMatrix>();
            for (var i = 0; i < Count; i++) {
                var result = GetTensorAt(i).ReverseIm2Col(filter, inputHeight, inputWidth, inputDepth,padding, filterHeight, filterWidth, stride);
                ret.Add(result);
            }
            return new Cpu3DTensor(ret);
        }
    }
}