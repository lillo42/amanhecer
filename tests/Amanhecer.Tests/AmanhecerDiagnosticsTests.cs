using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading.Tasks;

namespace Amanhecer.Tests;

public class AmanhecerDiagnosticsTests
{
    [Test]
    public async Task When_ReadingInstrumentationName_Should_BeAmanhecer()
    {
        await Assert.That(AmanhecerDiagnostics.InstrumentationName).IsEqualTo("Amanhecer");
    }

    [Test]
    public async Task When_ReadingActivitySource_Should_UseInstrumentationNameAndAssemblyVersion()
    {
        var expectedVersion = ExpectedVersion();

        await Assert.That(AmanhecerDiagnostics.ActivitySource.Name)
            .IsEqualTo(AmanhecerDiagnostics.InstrumentationName);
        await Assert.That(AmanhecerDiagnostics.ActivitySource.Version).IsEqualTo(expectedVersion);
    }

    [Test]
    public async Task When_ReadingMeter_Should_UseInstrumentationNameAndAssemblyVersion()
    {
        var expectedVersion = ExpectedVersion();

        await Assert.That(AmanhecerDiagnostics.Meter.Name)
            .IsEqualTo(AmanhecerDiagnostics.InstrumentationName);
        await Assert.That(AmanhecerDiagnostics.Meter.Version).IsEqualTo(expectedVersion);
    }

    [Test]
    public async Task When_ListenerSubscribedByInstrumentationName_Should_CaptureActivities()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmanhecerDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = AmanhecerDiagnostics.ActivitySource.StartActivity("tests.activity");

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.Source).IsSameReferenceAs(AmanhecerDiagnostics.ActivitySource);
        await Assert.That(activity.OperationName).IsEqualTo("tests.activity");
    }

    [Test]
    public async Task When_ListenerSubscribedByInstrumentationName_Should_CaptureInstruments()
    {
        const string instrumentName = "tests.diagnostics.counter";
        Instrument? published = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter == AmanhecerDiagnostics.Meter && instrument.Name == instrumentName)
            {
                published = instrument;
            }
        };
        listener.Start();

        var counter = AmanhecerDiagnostics.Meter.CreateCounter<int>(instrumentName);

        await Assert.That(published).IsNotNull();
        await Assert.That(published!.Meter.Name).IsEqualTo(AmanhecerDiagnostics.InstrumentationName);
    }

    private static string ExpectedVersion()
    {
        return typeof(AmanhecerDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";
    }
}
