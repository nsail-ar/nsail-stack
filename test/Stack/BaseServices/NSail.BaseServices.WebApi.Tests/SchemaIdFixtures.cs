// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Optical.Prescriptions.Models
{
    // The homonym the story is about, in the two areas that declare it for real: an app's
    // prescription and the kit's are different messages, and the short name belongs to each.
    public sealed class CreatePrescriptionBody
    {
        public string Text { get; set; } = string.Empty;
    }
}

namespace NSail.Medical.Prescriptions.Models
{
    public sealed class CreatePrescriptionBody
    {
        public string Text { get; set; } = string.Empty;

        public sealed class Line
        {
            public string Text { get; set; } = string.Empty;
        }
    }

    public sealed class Line
    {
        public string Text { get; set; } = string.Empty;
    }
}

namespace NSail.Types
{
    public sealed class Page<T>
    {
        public IReadOnlyList<T> Items { get; set; } = [];
    }

    public sealed class Pair<TFirst, TSecond>
    {
        public TFirst? First { get; set; }

        public TSecond? Second { get; set; }
    }
}
