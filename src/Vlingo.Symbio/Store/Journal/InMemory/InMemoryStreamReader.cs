// Copyright © 2012-2020 Vaughn Vernon. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using Vlingo.Common;

namespace Vlingo.Symbio.Store.Journal.InMemory
{
    public class InMemoryStreamReader<TEntry> : IStreamReader<TEntry>
    {
        private List<BaseEntry<TEntry>> _journalView;
        private Dictionary<string, State<TEntry>> _snapshotsView;
        private Dictionary<string, Dictionary<int, int>> _streamIndexesView;
        private string _name;

        public InMemoryStreamReader(List<BaseEntry<TEntry>> journalView, Dictionary<string, Dictionary<int, int>> streamIndexesView, Dictionary<string, State<TEntry>> snapshotsView, string name)
        {
            _journalView = journalView;
            _streamIndexesView = streamIndexesView;
            _snapshotsView = snapshotsView;
            _name = name;
        }

        public ICompletes<Stream<TEntry>> StreamFor(string streamName) => StreamFor(streamName, 1);

        public ICompletes<Stream<TEntry>> StreamFor(string streamName, int fromStreamVersion)
        {
            var version = fromStreamVersion;
            if (_snapshotsView.TryGetValue(streamName, out var snapshot))
            {
                if (snapshot.DataVersion > version)
                {
                    version = snapshot.DataVersion;
                }
                else
                {
                    snapshot = null!; // reading from beyond snapshot
                }
            }
            
            var entries = new List<BaseEntry<TEntry>>();
            if (_streamIndexesView.TryGetValue(streamName, out var versionIndexes))
            {
                while (versionIndexes.TryGetValue(version, out var journalIndex)) {
                    var entry = _journalView[journalIndex];
                    entries.Add(entry);
                    ++version;
                }
            }
            return Completes.WithSuccess(new Stream<TEntry>(streamName, version - 1, entries, snapshot));
        }

        public string Name => _name;
    }
}