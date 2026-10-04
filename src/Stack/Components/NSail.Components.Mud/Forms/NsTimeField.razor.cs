// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public partial class NsTimeField<TValue>
{
    TimeSpan? MudTime => ConvertToTimeSpan(Value);

    Task OnTimeChanged(TimeSpan? value)
    {
        return SetValue(ConvertFromTimeSpan(value));
    }

    static TimeSpan? ConvertToTimeSpan(TValue? value)
    {
        return value switch
        {
            null => null,
            TimeSpan timeSpan => timeSpan,
            TimeOnly timeOnly => timeOnly.ToTimeSpan(),
            _ => null
        };
    }

    static TValue? ConvertFromTimeSpan(TimeSpan? value)
    {
        if (value is null)
        {
            return default;
        }

        var target = typeof(TValue);

        if (target == typeof(TimeSpan))
        {
            return (TValue)(object)value.Value;
        }

        if (target == typeof(TimeSpan?))
        {
            return (TValue)(object)value;
        }

        if (target == typeof(TimeOnly))
        {
            return (TValue)(object)TimeOnly.FromTimeSpan(value.Value);
        }

        if (target == typeof(TimeOnly?))
        {
            return (TValue)(object)TimeOnly.FromTimeSpan(value.Value);
        }

        return default;
    }
}
