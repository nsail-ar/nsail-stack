// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

public sealed class FieldProbeModel
{
    public string? Text { get; set; }

    public string? Color { get; set; }

    public int? Number { get; set; }

    public decimal? Money { get; set; }

    public decimal? Percent { get; set; }

    public TimeSpan? Duration { get; set; }

    public DateTime? Date { get; set; }

    public TimeSpan? Time { get; set; }

    public DateTime? Moment { get; set; }

    public Guid? Choice { get; set; }

    public bool Flag { get; set; }

    public DateOnly DateOnlyValue { get; set; }

    public Guid RequiredChoice { get; set; }

    public static void FillInPlace(FieldProbeModel target)
    {
        var loaded = Preset();

        target.Text = loaded.Text;
        target.Color = loaded.Color;
        target.Number = loaded.Number;
        target.Money = loaded.Money;
        target.Percent = loaded.Percent;
        target.Duration = loaded.Duration;
        target.Date = loaded.Date;
        target.Time = loaded.Time;
        target.Moment = loaded.Moment;
        target.Choice = loaded.Choice;
        target.Flag = loaded.Flag;
        target.DateOnlyValue = loaded.DateOnlyValue;
        target.RequiredChoice = loaded.RequiredChoice;
    }

    public static FieldProbeModel Preset()
    {
        return new FieldProbeModel
        {
            Text = "already stored",
            Color = "#1976D2",
            Number = 7,
            Money = 1234.56m,
            Percent = 0.25m,
            Duration = TimeSpan.FromHours(3),
            Date = new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Unspecified),
            Time = new TimeSpan(14, 30, 0),
            Moment = new DateTime(2026, 4, 8, 14, 30, 0, DateTimeKind.Unspecified),
            Choice = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Flag = true,
            DateOnlyValue = new DateOnly(2026, 4, 8),
            RequiredChoice = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };
    }
}
