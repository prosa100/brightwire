﻿using BrightWire.Connectionist.Training.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BrightWire.Models;
using System.Xml;

namespace BrightWire.Connectionist.Training.Batch
{
    class BidirectionalBatchTrainer : RecurrentBatchTrainerBase, INeuralNetworkBidirectionalBatchTrainer
    {
        readonly int _padding;
        readonly bool _collectTrainingError;
        readonly IReadOnlyList<INeuralNetworkBidirectionalLayer> _layer;

        public BidirectionalBatchTrainer(ILinearAlgebraProvider lap, IReadOnlyList<INeuralNetworkBidirectionalLayer> layer, bool calculateTrainingError, int padding, bool stochastic) 
            : base(lap, stochastic)
        {
            _collectTrainingError = calculateTrainingError;
            _padding = padding;
            _layer = layer;
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing) {
                foreach (var item in _layer)
                    item.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IReadOnlyList<INeuralNetworkBidirectionalLayer> Layer { get { return _layer; } }

        public BidirectionalNetwork NetworkInfo
        {
            get
            {
                return new BidirectionalNetwork {
                    Padding = _padding,
                    Layer = _layer.Select(l => l.LayerInfo).ToArray(),
                };
            }

            set
            {
                for (int i = 0, len = value.Layer.Length, len2 = _layer.Count; i < len && i < len2; i++)
                    _layer[i].LayerInfo = value.Layer[i];
            }
        }

        public float CalculateCost(ISequentialTrainingDataProvider data, float[] forwardMemory, float[] backwardMemory, IRecurrentTrainingContext context)
        {
            return Execute(data, forwardMemory, backwardMemory, context)
                .SelectMany(r => r)
                .Select(r => context.TrainingContext.ErrorMetric.Compute(r.Output, r.Target))
                .Average()
            ;
        }

        public IReadOnlyList<RecurrentExecutionResults[]> Execute(ISequentialTrainingDataProvider trainingData, float[] forwardMemory, float[] backwardMemory, IRecurrentTrainingContext context)
        {
            List<RecurrentExecutionResults> temp;
            var sequenceOutput = new Dictionary<int, List<RecurrentExecutionResults>>();
            var batchSize = context.TrainingContext.MiniBatchSize;

            foreach (var miniBatch in _GetMiniBatches(trainingData, false, batchSize)) {
                _lap.PushLayer();
                var sequenceLength = miniBatch.SequenceLength;
                context.ExecuteBidirectional(context.TrainingContext, miniBatch, _layer, forwardMemory, backwardMemory, _padding, null, (memoryOutput, output) => {
                    // store the output
                    for (var k = 0; k < sequenceLength; k++) {
                        if (!sequenceOutput.TryGetValue(k, out temp))
                            sequenceOutput.Add(k, temp = new List<RecurrentExecutionResults>());
                        var ret = output[k].AsIndexable().Rows.Zip(miniBatch.GetExpectedOutput(output, k).AsIndexable().Rows, (a, e) => Tuple.Create(a, e));
                        temp.AddRange(ret.Zip(memoryOutput[k], (t, d) => new RecurrentExecutionResults(t.Item1, t.Item2, d)));
                    }
                });

                // cleanup
                context.TrainingContext.EndBatch();
                _lap.PopLayer();
                miniBatch.Dispose();
            }
            return sequenceOutput.OrderBy(kv => kv.Key).Select(kv => kv.Value.ToArray()).ToList();
        }

        public Tuple<float[], float[]> Train(ISequentialTrainingDataProvider trainingData, float[] forwardMemory, float[] backwardMemory, int numEpochs, IRecurrentTrainingContext context)
        {
            var trainingContext = context.TrainingContext;
            var logger = trainingContext.Logger;
            for (int i = 0; i < numEpochs && context.TrainingContext.ShouldContinue; i++) {
                trainingContext.StartEpoch(trainingData.Count);
                var batchErrorList = new List<double>();

                foreach (var miniBatch in _GetMiniBatches(trainingData, _stochastic, trainingContext.MiniBatchSize)) {
                    _lap.PushLayer();
                    var sequenceLength = miniBatch.SequenceLength;
                    var updateStack = new Stack<Tuple<Stack<Tuple<INeuralNetworkRecurrentBackpropagation, INeuralNetworkRecurrentBackpropagation>>, IMatrix, IMatrix, ISequentialMiniBatch, int>>();
                    context.ExecuteBidirectional(context.TrainingContext, miniBatch, _layer, forwardMemory, backwardMemory, _padding, updateStack, null);

                    // backpropagate, accumulating errors across the sequence
                    using (var updateAccumulator = new UpdateAccumulator(trainingContext)) {
                        while (updateStack.Any()) {
                            var update = updateStack.Pop();
                            var isT0 = !updateStack.Any();
                            var actionStack = update.Item1;

                            // calculate error
                            var expectedOutput = update.Item2;
                            var curr = new List<IMatrix>();
                            curr.Add(expectedOutput.Subtract(update.Item3));

                            // get a measure of the training error
                            if (_collectTrainingError) {
                                foreach (var item in curr)
                                    batchErrorList.Add(item.AsIndexable().Values.Select(v => Math.Pow(v, 2)).Average() / 2);
                            }

                            #region logging
                            if (logger != null) {
                                logger.WriteStartElement("initial-error");
                                foreach (var item in curr)
                                    item.WriteTo(logger);
                                logger.WriteEndElement();
                            }
                            #endregion

                            // backpropagate
                            while (actionStack.Any()) {
                                var backpropagationAction = actionStack.Pop();
                                if(backpropagationAction.Item1 != null && backpropagationAction.Item2 != null && curr.Count == 1) {
                                    using (var m = curr[0]) {
                                        var split = m.SplitRows(forwardMemory.Length);
                                        curr[0] = split.Item1;
                                        curr.Add(split.Item2);
                                    }
                                    #region logging
                                    if (logger != null) {
                                        logger.WriteStartElement("post-split");
                                        foreach (var item in curr)
                                            item.WriteTo(logger);
                                        logger.WriteEndElement();
                                    }
                                    #endregion
                                }
                                if (backpropagationAction.Item1 != null) {
                                    using(var m = curr[0])
                                        curr[0] = backpropagationAction.Item1.Execute(m, trainingContext, actionStack.Any() || isT0, updateAccumulator);
                                }
                                if (backpropagationAction.Item2 != null) {
                                    using(var m = curr[1])
                                        curr[1] = backpropagationAction.Item2.Execute(m, trainingContext, actionStack.Any() || isT0, updateAccumulator);
                                }
                                #region logging
                                if (logger != null) {
                                    logger.WriteStartElement("error");
                                    foreach (var item in curr)
                                        item.WriteTo(logger);
                                    logger.WriteEndElement();
                                }
                                #endregion
                            }

                            // apply any filters
                            foreach (var filter in _filter) {
                                foreach(var item in curr)
                                    filter.AfterBackPropagation(update.Item4, update.Item5, item);
                            }

                            // adjust the initial memory against the error signal
                            if (isT0) {
                                using (var columnSums0 = curr[0].ColumnSums())
                                using (var columnSums1 = curr[1].ColumnSums()) {
                                    var initialDelta = columnSums0.AsIndexable();
                                    for (var j = 0; j < forwardMemory.Length; j++)
                                        forwardMemory[j] += initialDelta[j] * trainingContext.TrainingRate;

                                    initialDelta = columnSums1.AsIndexable();
                                    for (var j = 0; j < backwardMemory.Length; j++)
                                        backwardMemory[j] += initialDelta[j] * trainingContext.TrainingRate;
                                }
                            }
                        }
                    }

                    // cleanup
                    trainingContext.EndBatch();
                    _lap.PopLayer();
                    miniBatch.Dispose();
                }
                trainingContext.EndRecurrentEpoch(_collectTrainingError ? batchErrorList.Average() : 0, context);
            }
            return Tuple.Create(forwardMemory, backwardMemory);
        }
    }
}
