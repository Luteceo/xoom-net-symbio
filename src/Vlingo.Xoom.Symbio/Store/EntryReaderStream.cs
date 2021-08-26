// Copyright © 2012-2021 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using Reactive.Streams;
using Vlingo.Xoom.Actors;
using Vlingo.Xoom.Streams;

namespace Vlingo.Xoom.Symbio.Store
{
    public class EntryReaderStream : IStream
    {
        private readonly EntryAdapterProvider _entryAdapterProvider;
        private long _flowElementsRate;
        private readonly IEntryReader _entryReader;
        private ISource<EntryBundle>? _entryReaderSource;
        private IPublisher<EntryBundle>? _publisher;
        private readonly Stage _stage;
        private EntryStreamSubscriber<EntryBundle>? _subscriber;

        public EntryReaderStream(Stage stage, IEntryReader entryReader, EntryAdapterProvider entryAdapterProvider)
        {
            _stage = stage;
            _entryReader = entryReader;
            _entryAdapterProvider = entryAdapterProvider;
        }
        
        public void Request(long flowElementsRate)
        {
            _flowElementsRate = flowElementsRate;

            _subscriber?.SubscriptionHook?.Request(_flowElementsRate);
        }

        public void FlowInto<T>(Sink<T> sink) => FlowInto(sink, Stream.DefaultFlowRate, Stream.DefaultProbeInterval);

        public void FlowInto<T>(Sink<T> sink, long flowElementsRate) => FlowInto(sink, flowElementsRate, Stream.DefaultProbeInterval);

        public void FlowInto<T>(Sink<T> sink, long flowElementsRate, int probeInterval)
        {
            _flowElementsRate = flowElementsRate;

            var configuration =
                PublisherConfiguration.With(
                    probeInterval,
                    Streams.Streams.DefaultMaxThrottle,
                    Streams.Streams.DefaultBufferSize,
                    Streams.Streams.OverflowPolicy.DropCurrent);

            _entryReaderSource = _stage.ActorFor<ISource<EntryBundle>>(() => new EntryReaderSource(_entryReader, _entryAdapterProvider, flowElementsRate));

            _publisher = _stage.ActorFor<IPublisher<EntryBundle>>(() => new StreamPublisher<EntryBundle>(_entryReaderSource, configuration));

            var subscriber = _stage.ActorFor<ISubscriber<T>>(() => new EntryStreamSubscriber<T>(sink, flowElementsRate));

            _publisher.Subscribe((ISubscriber<EntryBundle>) subscriber);
        }

        public void Stop() => _subscriber?.SubscriptionHook?.Cancel();
    }
}