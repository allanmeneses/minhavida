using Circulos.Api.Reminders;

namespace Circulos.Api.Tests;

public sealed class RecurrenceTests
{
    [Fact]
    public void SemRecorrencia_SoDisparaNaData()
    {
        var rule = new RecurrenceRule { Frequency = "none" };
        var start = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(start, RecurrenceCalculator.NextOnOrAfter(rule, start, start, 0));
        Assert.Null(RecurrenceCalculator.NextOnOrAfter(rule, start, start.AddSeconds(1), 0));
    }

    [Fact]
    public void DiariaComIntervaloDois_SegueAGradeOriginal()
    {
        var rule = new RecurrenceRule { Frequency = "daily", Interval = 2 };
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start.AddDays(2).AddMinutes(1), 0);
        Assert.Equal(start.AddDays(4), next);
    }

    [Fact]
    public void SemanalACadaDuasSemanas_Avanca14Dias()
    {
        var rule = new RecurrenceRule { Frequency = "weekly", Interval = 2, DaysOfWeek = [(int)DayOfWeek.Monday] };
        var start = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start.AddDays(1), 0);
        Assert.NotNull(next);
        Assert.True(next!.Value > start);
        Assert.Equal(DayOfWeek.Monday, next.Value.DayOfWeek);
    }

    [Fact]
    public void MensalPorData_UsaODiaDoMes()
    {
        var rule = new RecurrenceRule { Frequency = "monthly", Interval = 1, MonthlyMode = "date", DayOfMonth = 31 };
        var start = new DateTime(2026, 1, 31, 10, 0, 0, DateTimeKind.Utc);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start.AddDays(1), 0);
        Assert.Equal(new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void MensalPorPosicao_TerceiraTerca()
    {
        var rule = new RecurrenceRule { Frequency = "monthly", Interval = 1, MonthlyMode = "position", WeekOfMonth = 3 };
        var start = new DateTime(2026, 1, 20, 11, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Tuesday, start.DayOfWeek);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start.AddDays(1), 0);
        Assert.NotNull(next);
        Assert.Equal(DayOfWeek.Tuesday, next!.Value.DayOfWeek);
        Assert.Equal(2, next.Value.Month);
    }

    [Fact]
    public void Anual_MesmoDiaDoAnoSeguinte()
    {
        var rule = new RecurrenceRule { Frequency = "yearly", Interval = 1 };
        var start = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start.AddDays(1), 0);
        Assert.Equal(new DateTime(2027, 3, 10, 8, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void TerminoPorNumero_ParaQuandoCompleta()
    {
        var rule = new RecurrenceRule { Frequency = "daily", Interval = 1, MaxOccurrences = 2 };
        var start = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        Assert.Null(RecurrenceCalculator.NextOnOrAfter(rule, start, start, 2));
    }

    [Fact]
    public void AtrasadoSemRecorrencia_NaoSegueRelevante()
    {
        var rule = new RecurrenceRule { Frequency = "none" };
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        Assert.False(RecurrenceCalculator.StillRelevant(rule, start, start, now));
    }
}
