﻿using BrightWire.ExecutionGraph.GradientDescent;
using System;
using System.Collections.Generic;
using System.Text;

namespace BrightWire.Descriptor.GradientDescent
{
    class RmsPropDescriptor : ICreateTemplateBasedGradientDescent
    {
        readonly float _decay;

        public RmsPropDescriptor(float decay = 0.9f)
        {
            _decay = decay;
        }

        public IGradientDescentOptimisation Create(IGradientDescentOptimisation prev, IMatrix template, IPropertySet propertySet)
        {
            var cache = propertySet.LinearAlgebraProvider.Create(template.RowCount, template.ColumnCount, 0f);
            return new RmsProp(_decay, cache, prev);
        }
    }
}