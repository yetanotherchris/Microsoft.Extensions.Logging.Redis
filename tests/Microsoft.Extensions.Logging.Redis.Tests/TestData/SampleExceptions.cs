using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Logging.Redis.Tests.TestData;

public static class SampleExceptions
{
    public static Exception Simple() => new ArgumentException("Sample argument error");

    public static Exception WithInnerExceptions()
    {
        try
        {
            try
            {
                throw new InvalidOperationException("Inner most");
            }
            catch (Exception inner)
            {
                throw new InvalidOperationException("Middle layer", inner);
            }
        }
        catch (Exception inner)
        {
            throw new ApplicationException("Outer layer", inner);
        }
    }

    public static Exception Aggregate()
    {
        var inner = new List<Exception>
        {
            new InvalidOperationException("First"),
            new InvalidOperationException("Second"),
            new InvalidOperationException("Third"),
        };

        return new AggregateException("Aggregate", inner);
    }
}
