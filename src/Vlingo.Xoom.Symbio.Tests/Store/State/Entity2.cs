// Copyright © 2012-2022 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

namespace Vlingo.Xoom.Symbio.Tests.Store.State;

public class Entity2
{
    public string Id { get; }
        
    public string Value { get; }

    public Entity2(string id, string value)
    {
        Id = id;
        Value = value;
    }
}