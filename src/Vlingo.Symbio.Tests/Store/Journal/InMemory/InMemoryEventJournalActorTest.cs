// Copyright © 2012-2020 Vaughn Vernon. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using Vlingo.Actors;
using Vlingo.Actors.TestKit;
using Vlingo.Common.Serialization;
using Vlingo.Symbio.Store;
using Vlingo.Symbio.Store.Journal;
using Vlingo.Symbio.Store.Journal.InMemory;
using Vlingo.Symbio.Tests.Store.Dispatch;
using Vlingo.Symbio.Tests.Store.State;
using Xunit;
using Xunit.Abstractions;

namespace Vlingo.Symbio.Tests.Store.Journal.InMemory
{
    public class InMemoryEventJournalActorTest : IDisposable
    {
        private object _object = new object();
        private IJournal<TextEntry> _journal;
        private readonly World _world;
        private readonly MockDispatcher<TextEntry, SnapshotState> _dispatcher;

        [Fact]
        public void TestThatJournalAppendsOneEvent()
        {
            var interest = new MockAppendResultInterest<Test1Source, SnapshotState>();
            _dispatcher.AfterCompleting(1);
            interest.AfterCompleting(1);

            var source = new Test1Source();
            var streamName = "123";
            var streamVersion = 1;
            _journal.Append<Test1Source, SnapshotState>(streamName, streamVersion, source, interest, _object);

            Assert.Equal(1, interest.ReceivedAppendsSize);

            var entries = interest.Entries;
            var journalData = entries.First();
            Assert.NotNull(journalData);
            Assert.Equal(streamName, journalData.StreamName);
            Assert.Equal(streamVersion, journalData.StreamVersion);
            Assert.Equal(Result.Success, journalData.Result);
            Assert.False(journalData.Snapshot.IsPresent);

            var sourceList = journalData.Sources;
            Assert.Single(sourceList);
            Assert.Equal(source, sourceList.First());

            Assert.Equal(1, _dispatcher.DispatchedCount());
            var dispatched = _dispatcher.GetDispatched()[0];

            Assert.NotEqual(new DateTimeOffset(),  dispatched.CreatedOn);
            Assert.False(dispatched.State.IsPresent);
            Assert.NotNull(dispatched.Id);
            var dispatchedEntries = dispatched.Entries;
            Assert.Single(dispatchedEntries);
        }

        public InMemoryEventJournalActorTest(ITestOutputHelper output)
        {
            var converter = new Converter(output);
            Console.SetOut(converter);
            
            _world = World.StartWithDefaults("test-journal");
            _dispatcher = new MockDispatcher<TextEntry, SnapshotState>(new MockConfirmDispatchedResultInterest());
            
            _journal = Journal<TextEntry>.Using<InMemoryJournalActor<TextEntry, SnapshotState>, SnapshotState>(_world.Stage, _dispatcher);
            EntryAdapterProvider.Instance(_world).RegisterAdapter(new Test1SourceAdapter());
            EntryAdapterProvider.Instance(_world).RegisterAdapter(new Test2SourceAdapter());
            StateAdapterProvider.Instance(_world).RegisterAdapter(new SnapshotStateAdapter());
        }
        
        public void Dispose() => _world.Terminate();
    }
    
    public class Test1Source : Source<string>
    {
        private int _one = 1;

        public int One => _one;
    }

    public class Test2Source : Source<string>
    {
        private int _two = 2;

        public int Two => _two;
    }
    
    internal class Test1SourceAdapter : EntryAdapter<Test1Source, TextEntry>
    {
        public override Test1Source FromEntry(TextEntry entry) => JsonSerialization.Deserialized<Test1Source>(entry.EntryData);
        
        public override TextEntry ToEntry(Test1Source source, Metadata metadata)
        {
            var serialization = JsonSerialization.Serialized(source);
            return new TextEntry(typeof(Test1Source), 1, serialization, metadata);
        }

        public override TextEntry ToEntry(Test1Source source, string id, Metadata metadata)
        {
            var serialization = JsonSerialization.Serialized(source);
            return new TextEntry(id, typeof(Test1Source), 1, serialization, metadata);
        }
    }
    
    internal class Test2SourceAdapter : EntryAdapter<Test2Source, TextEntry>
    {
        public override Test2Source FromEntry(TextEntry entry) => JsonSerialization.Deserialized<Test2Source>(entry.EntryData);

        public override TextEntry ToEntry(Test2Source source, Metadata metadata)
        {
            var serialization = JsonSerialization.Serialized(source);
            return new TextEntry(typeof(Test2Source), 1, serialization, metadata);
        }

        public override TextEntry ToEntry(Test2Source source, string id, Metadata metadata)
        {
            var serialization = JsonSerialization.Serialized(source);
            return new TextEntry(id, typeof(Test2Source), 1, serialization, metadata);
        }
    }

    internal class TestResults
    {
        private AccessSafely _access;
        internal List<BaseEntry<string>> Entries { get; } = new List<BaseEntry<string>>();
        
        internal AccessSafely AfterCompleting(int times)
        {
            _access = AccessSafely.AfterCompleting(times)
                .WritingWith<List<BaseEntry<string>>>("addAll", values => Entries.AddRange(values))
                .ReadingWith<int, BaseEntry<string>>("entry", index => Entries[index])
                .ReadingWith<int, string>("entryId", index => Entries[index].Id)
                .ReadingWith("size", () => Entries.Count);

            return _access;
        }
    }
}