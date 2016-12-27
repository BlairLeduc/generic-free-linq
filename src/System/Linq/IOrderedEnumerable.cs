// Copyright © 2017 Blair Leduc 
// This source is subject to the MIT License.
// See https://opensource.org/licenses/MIT
// All other rights reserved.

using System;
using Microsoft.SPOT;
using System.Collections;

namespace System.Linq
{
    public interface IOrderedEnumerable : IEnumerable
    {
        IOrderedEnumerable CreateOrderedEnumerable(Func2 keySelector, IComparer comparer, bool descending);
    }
}
