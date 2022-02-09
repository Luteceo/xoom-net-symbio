// Copyright © 2012-2022 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Linq;

namespace Vlingo.Xoom.Symbio.Store.Journal
{
    /// <summary>
    /// The entries and possible snapshot of a full or partial stream of a given named stream.
    /// </summary>
    public class EntityStream
    {
        /// <summary>
        /// Construct a new Stream.
        /// </summary>
        /// <param name="streamName">the <code>string</code> name of this stream, which is generally a global unique identity
        /// of an associated entity/aggregate</param>
        /// <param name="streamVersion">The <code>int</code> version of the stream</param>
        /// <param name="entries">The <code>IEnumerable{BaseEntry}</code> of all entries in the named stream or some sub-stream</param>
        /// <param name="snapshot">the <see cref="State{T}"/> of a persisted state, or an empty <see cref="State{T}"/> if none</param>
        public EntityStream(string streamName, int streamVersion, IEnumerable<BaseEntry> entries, IState? snapshot)
        {
            StreamName = streamName;
            StreamVersion = streamVersion;
            Entries = entries;
            Snapshot = snapshot;
        }
        
        /// <summary>
        /// The most recent <see cref="State{T}"/> snapshot, if any.
        /// </summary>
        public IState? Snapshot { get; }
        
        /// <summary>
        /// The list of <see cref="BaseEntry"/> of the entries of the named stream, and possibly just a sub-stream.
        /// </summary>
        public IEnumerable<BaseEntry> Entries { get; }
        
        /// <summary>
        /// The string name of the stream, which is generally a global unique identity
        /// of an associated entity/aggregate.
        /// </summary>
        public string StreamName { get; }
        
        /// <summary>
        /// The version of the stream, which indicates the 1-based sequence of the
        /// last of all my entries listed herein. All entry streams start at version
        /// 1 and end with the total number of all its entries, e.g. Entries.Size.
        /// </summary>
        public int StreamVersion { get; }

        /// <summary>
        /// Gets whether or not I hold a non-empty snapshot.
        /// </summary>
        public bool HasSnapshot => Snapshot != null && !Snapshot.IsEmpty;

        /// <summary>
        /// Gets my size, which is the number of entries.
        /// </summary>
        public int Size => Entries.Count();

        public override string ToString() => $"EntityStream[streamName={StreamName} streamVersion={StreamVersion} entries={string.Join(", ", Entries.Select(e => e.ToString()))} snapshot={Snapshot}]";
    }
}