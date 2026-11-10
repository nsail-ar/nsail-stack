// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.SourceGenerator.Generation.Http.Models;

public record RouteModel
{
    public EquatableList<SegmentModel> Segments { get; set; } = new();
 
    public override string ToString()
    {
        if (Segments.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        for (var i = 0; i < Segments.Count; i++)
        {
            if (i > 0)
                sb.Append('/');

            var segment = Segments[i];
            if (segment.Type == SegmentType.Literal)
            {
                sb.Append(segment.Value);
            }
            else if (segment.Type == SegmentType.Parameter)
            {
                sb.Append("{HttpRequestMessageBuilder.Segment(message.");
                sb.Append(segment.Value);
                sb.Append(")}");
            }
        }

        return sb.ToString();
    }
}