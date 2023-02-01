// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using Vlingo.Xoom.Common;

namespace Vlingo.Xoom.Symbio.Store.Journal;

/// <summary>
/// The reader for a specific named <see cref="EntityStream"/> within the <see cref="IJournal{T}"/>, which is provided by
/// its <code>StreamReader()</code>. This reader can read all existing <see cref="IEntry{T}"/> instances of a given
/// <see cref="EntityStream"/>, or from a specific <see cref="EntityStream"/> version. If snapshots are used for the
/// given <see cref="EntityStream"/>, the result may include a snapshot <see cref="State{T}"/> instance if one is available
/// within the given <see cref="EntityStream"/> version span <code>fromStreamVersion</code> to the stream end).
/// </summary>
public interface IStreamReader
{
    /// <summary>
    /// Eventually answers the full <see cref="EntityStream"/> of the stream with <paramref name="streamName"/>; or if
    /// a <see cref="State{T}"/> snapshot is available, only the snapshot and any <see cref="IEntry{T}"/> instances
    /// that exist since the snapshot.
    /// </summary>
    /// <param name="streamName">The string name of the <see cref="EntityStream"/> to answer</param>
    /// <returns>The <see cref="ICompletes{T}"/> of <see cref="EntityStream"/> of the full stream, or a snapshot with remaining stream</returns>
    ICompletes<EntityStream> StreamFor(string streamName);
        
    /// <summary>
    /// Eventually answers the <see cref="EntityStream"/> of the stream with <paramref name="streamName"/>, starting with
    /// <paramref name="fromStreamVersion"/> until the end of the stream. Any existing snapshot within this
    /// sub-stream is ignored. This method enables reading the entire stream without snapshot
    /// optimizations (e.g. <code>reader.StreamFor(id, 1)</code> reads the entire stream of <paramref name="streamName"/>.
    /// </summary>
    /// <param name="streamName">The string name of the <see cref="EntityStream"/> to answer</param>
    /// <param name="fromStreamVersion">The <code>int</code> version from which to begin reading, inclusive</param>
    /// <returns>The <see cref="ICompletes{T}"/> of <see cref="EntityStream"/> of all full stream</returns>
    ICompletes<EntityStream> StreamFor(string streamName, int fromStreamVersion);
        
    /// <summary>
    /// Gets the name of the reader.
    /// </summary>
    string Name { get; }
}

public static class StreamReaderExtensions
{
    /// <summary>
    /// The stream version of the first entry in every stream.
    /// </summary>
    public static int FirstStreamVersion<T>(this IStreamReader streamReader) => 1;
}