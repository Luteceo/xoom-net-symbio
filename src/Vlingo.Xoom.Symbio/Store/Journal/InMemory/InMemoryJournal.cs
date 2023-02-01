// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using Vlingo.Xoom.Common;
using Vlingo.Xoom.Actors;
using Vlingo.Xoom.Symbio.Store.Dispatch;
using Vlingo.Xoom.Symbio.Store.Dispatch.Control;
using Vlingo.Xoom.Symbio.Store.Dispatch.InMemory;
using IDispatcher = Vlingo.Xoom.Symbio.Store.Dispatch.IDispatcher;

namespace Vlingo.Xoom.Symbio.Store.Journal.InMemory;

public class InMemoryJournal<T> : Journal<T>, IStoppable
{
    private readonly EntryAdapterProvider _entryAdapterProvider;
    private readonly StateAdapterProvider _stateAdapterProvider;
    private readonly List<IEntry> _journal;
    private readonly Dictionary<string, IJournalReader> _journalReaders;
    private readonly Dictionary<string, IStreamReader> _streamReaders;
    private readonly Dictionary<string, Dictionary<int, int>> _streamIndexes;
    private readonly Dictionary<string, IState> _snapshots;
    private readonly List<Dispatchable> _dispatchables;
    private readonly List<IDispatcher> _dispatchers;
    private readonly IDispatcherControl _dispatcherControl;

    public InMemoryJournal(IEnumerable<IDispatcher> dispatchers, World world, long checkConfirmationExpirationInterval = 1000L, long confirmationExpiration = 1000L)
    {
        _dispatchers = dispatchers.ToList();
        _entryAdapterProvider = EntryAdapterProvider.Instance(world);
        _stateAdapterProvider = StateAdapterProvider.Instance(world);
        _journal = new List<IEntry>();
        _journalReaders = new Dictionary<string, IJournalReader>(1);
        _streamReaders = new Dictionary<string, IStreamReader>(1);
        _streamIndexes = new Dictionary<string, Dictionary<int, int>>();
        _snapshots = new Dictionary<string, IState>();
        _dispatchables = new List<Dispatchable>();

        var dispatcherControlDelegate = new InMemoryDispatcherControlDelegate(_dispatchables);

        _dispatcherControl = world.Stage.ActorFor<IDispatcherControl>(
            () => new DispatcherControlActor(
                _dispatchers,
                dispatcherControlDelegate,
                checkConfirmationExpirationInterval,
                confirmationExpiration));
    }

    public InMemoryJournal(IDispatcher dispatcher, World world,
        long checkConfirmationExpirationInterval = 1000L, long confirmationExpiration = 1000L)
        : this (new []{dispatcher}, world, checkConfirmationExpirationInterval, confirmationExpiration)
    {
    }

    public override void Append<TSource>(string streamName, int streamVersion, TSource source, Metadata metadata, IAppendResultInterest interest, object @object)
    {
        var entry = _entryAdapterProvider.AsEntry(source, streamVersion, metadata);
        Insert(streamName, streamVersion, entry);
        Dispatch(streamName, streamVersion, new List<IEntry> { entry }, null);
        interest.AppendResultedIn(Success.Of<StorageException, Result>(Result.Success), streamName, streamVersion, source, Optional.Empty<object>(), @object);
    }

    public override void AppendWith<TSource, TSnapshotState>(string streamName, int streamVersion, TSource source, Metadata metadata, TSnapshotState snapshot, IAppendResultInterest interest, object @object)
    {
        var entry = _entryAdapterProvider.AsEntry(source, streamVersion, metadata);
        Insert(streamName, streamVersion, entry);
        IState? raw;
        Optional<TSnapshotState> snapshotResult;
        if (snapshot != null)
        {
            raw = _stateAdapterProvider.AsRaw<TSnapshotState, IState>(streamName, snapshot, streamVersion);
            _snapshots.Add(streamName, raw);
            snapshotResult = Optional.Of(snapshot);
        }
        else
        {
            raw = null;
            snapshotResult = Optional.Empty<TSnapshotState>();
        }

        Dispatch(streamName, streamVersion, new List<IEntry> { entry }, raw);
        interest.AppendResultedIn(Success.Of<StorageException, Result>(Result.Success), streamName, streamVersion, source, snapshotResult, @object);
    }

    public override void AppendAll<TSource>(string streamName, int fromStreamVersion, IEnumerable<ISource> sources, Metadata metadata, IAppendResultInterest interest, object @object)
    {
        var sourcesForEntries = sources.ToList();
        var entries = _entryAdapterProvider.AsEntries(sourcesForEntries, fromStreamVersion, metadata);
        var dispatchableEntries = entries.ToList();
        Insert(streamName, fromStreamVersion, dispatchableEntries);

        Dispatch(streamName, fromStreamVersion, dispatchableEntries, null);
        interest.AppendAllResultedIn(Success.Of<StorageException, Result>(Result.Success), streamName, fromStreamVersion, sourcesForEntries, Optional.Empty<object>(), @object);
    }

    public override void AppendAllWith<TSource, TSnapshotState>(string streamName, int fromStreamVersion, IEnumerable<ISource> sources,
        Metadata metadata, TSnapshotState snapshot, IAppendResultInterest interest, object @object)
    {
        var sourcesForEntries = sources.ToList();
        var entries = _entryAdapterProvider.AsEntries(sourcesForEntries, fromStreamVersion, metadata);
        var dispatchableEntries = entries.ToList();
        Insert(streamName, fromStreamVersion, dispatchableEntries);
        IState? raw;
        Optional<TSnapshotState> snapshotResult;
        if (snapshot != null)
        {
            raw = _stateAdapterProvider.AsRaw<TSnapshotState, IState>(streamName, snapshot, fromStreamVersion);
            _snapshots.Add(streamName, raw);
            snapshotResult = Optional.Of(snapshot);
        }
        else
        {
            raw = null;
            snapshotResult = Optional.Empty<TSnapshotState>();
        }

        Dispatch(streamName, fromStreamVersion, dispatchableEntries, raw);
        interest.AppendAllResultedIn(Success.Of<StorageException, Result>(Result.Success), streamName, fromStreamVersion, sourcesForEntries, snapshotResult, @object);
    }

    public override ICompletes<IJournalReader?> JournalReader(string name)
    {
        IJournalReader? reader = null;
        if (!_journalReaders.ContainsKey(name))
        {
            reader = new InMemoryJournalReader(_journal, name);
            _journalReaders.Add(name, reader);
        }
            
        return Completes.WithSuccess(reader);
    }

    public override ICompletes<IStreamReader?> StreamReader(string name)
    {
        IStreamReader? reader = null;
        if (!_journalReaders.ContainsKey(name))
        {
            var castedDictionary = new Dictionary<string, State<T>>();
            foreach (var snapshotPair in _snapshots)
            {
                castedDictionary.Add(snapshotPair.Key, (State<T>)snapshotPair.Value);
            }
            reader = new InMemoryStreamReader<T>(_journal.Cast<BaseEntry>().ToList(), _streamIndexes, castedDictionary, name);
            _streamReaders.Add(name, reader);
        }
        return Completes.WithSuccess(reader);
    }

    public void Conclude()
    {
    }

    public void Stop() => _dispatcherControl.Stop();

    public bool IsStopped { get; } = false;
        
    private void Insert(string streamName, int streamVersion, IEntry entry)
    {
        var entryIndex = _journal.Count;
        var id = $"{entryIndex + 1}";
        if (entry is BaseEntry baseEntry)
        {
            baseEntry.SetId(id);
        }
        _journal.Add(entry);

        var versionIndexes = _streamIndexes.ComputeIfAbsent(streamName, k => new Dictionary<int, int>());
        versionIndexes.Add(streamVersion, entryIndex);
    }

    private void Insert(string streamName, int fromStreamVersion, IEnumerable<IEntry> entries)
    {
        int index = 0;
        foreach (var entry in entries)
        {
            Insert(streamName, fromStreamVersion + index, entry);
            ++index;
        }
    }

    private void Dispatch(string streamName, int streamVersion, IEnumerable<IEntry> entries, IState? snapshot)
    {
        var dispatchableEntries = entries as IEntry[] ?? entries.ToArray();
        var id = GetDispatchId(streamName, streamVersion, dispatchableEntries);
        var dispatchable = new Dispatchable(id,  DateTimeOffset.Now, snapshot, dispatchableEntries);
        _dispatchables.Add(dispatchable);
        foreach (var dispatcher in _dispatchers)
        {
            dispatcher.Dispatch(dispatchable);
        }
    }

    private static string GetDispatchId(string streamName, int streamVersion, IEnumerable<IEntry> entries) => $"{streamName}:{streamVersion}:{string.Join(":", entries.Select(e => e.Id))}";
}