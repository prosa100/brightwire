﻿using System;
using System.Collections.Generic;
using System.Text;

namespace BrightWire.ExecutionGraph.Layer
{
    class FeedForward : IComponent
    {
        protected readonly IVector _bias;
        protected readonly IMatrix _weight;
        protected readonly IGradientDescentOptimisation _updater;

        class Backpropagation : IBackpropagation
        {
            readonly FeedForward _layer;
            readonly IMatrix _input = null;

            public Backpropagation(FeedForward layer, IMatrix input)
            {
                _layer = layer;
                _input = input;
            }

            public void Dispose()
            {
                _input.Dispose();
            }

            public void Backward(IMatrix errorSignal, int channel, IBatchContext context, bool calculateOutput)
            {
                // work out the next error signal
                IMatrix ret = null;
                if (calculateOutput)
                    ret = errorSignal.TransposeAndMultiply(_layer._weight);

                // calculate the update to the weights
                var weightUpdate = _input.TransposeThisAndMultiply(errorSignal);

                // store the updates
                var learningContext = context.LearningContext;
                learningContext.Store(errorSignal, err => _UpdateBias(err, learningContext));
                learningContext.Store(weightUpdate, err => _layer.Update(err, learningContext));

                // log the backpropagation
                learningContext.Log("feed-forward-backpropagation", channel, _layer.GetHashCode(), errorSignal, ret, writer => {
                    if (learningContext.LogMatrixValues) {
                        writer.WriteStartElement("bias-update");
                        writer.WriteRaw(errorSignal.AsIndexable().AsXml);
                        writer.WriteEndElement();

                        writer.WriteStartElement("weight-update");
                        writer.WriteRaw(weightUpdate.AsIndexable().AsXml);
                        writer.WriteEndElement();
                    }
                });

                context.Backpropagate(ret, channel);
            }

            void _UpdateBias(IMatrix delta, ILearningContext context)
            {
                using (var columnSums = delta.ColumnSums())
                    _layer._bias.AddInPlace(columnSums, 1f / columnSums.Count, context.LearningRate);
            }
        }

        public FeedForward(IVector bias, IMatrix weight, IGradientDescentOptimisation updater)
        {
            _bias = bias;
            _weight = weight;
            _updater = updater;
        }

        public void Dispose()
        {
            _bias.Dispose();
            _weight.Dispose();
        }

        public void Update(IMatrix delta, ILearningContext context)
        {
            _updater.Update(_weight, delta, context);
        }

        public IMatrix Train(IMatrix input, int channel, IBatchContext context)
        {
            context.RegisterBackpropagation(new Backpropagation(this, input), channel);

            var output = Execute(input, channel, context);
            context.LearningContext.Log("feed-forward", channel, GetHashCode(), input, output);
            return output;
        }

        public IMatrix Execute(IMatrix input, int channel, IBatchContext context)
        {
            var ret = input.Multiply(_weight);
            ret.AddToEachRow(_bias);
            return ret;
        }
    }
}