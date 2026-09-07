using System.Threading.Tasks;
using Amanhecer.Abstractions;

namespace Amanhecer.Tests;

public class AmanhecerPipelineContextAccessorTests
{
    [Test]
    public async Task When_NoContextSet_Should_ReturnNull()
    {
        var accessor = new AmanhecerPipelineContextAccessor();

        await Assert.That(accessor.PipelineContext).IsNull();
    }

    [Test]
    public async Task When_ContextSet_Should_ReturnSameInstance()
    {
        var accessor = new AmanhecerPipelineContextAccessor();
        var context = new AmanhecerContext();

        accessor.PipelineContext = context;

        await Assert.That(accessor.PipelineContext).IsSameReferenceAs(context);
    }

    [Test]
    public async Task When_ContextCleared_Should_ReturnNull()
    {
        var accessor = new AmanhecerPipelineContextAccessor();
        accessor.PipelineContext = new AmanhecerContext();

        accessor.PipelineContext = null;

        await Assert.That(accessor.PipelineContext).IsNull();
    }

    [Test]
    public async Task When_ContextSet_Should_FlowAcrossAwaitPoints()
    {
        var accessor = new AmanhecerPipelineContextAccessor();
        var context = new AmanhecerContext();
        accessor.PipelineContext = context;

        await Task.Yield();

        await Assert.That(accessor.PipelineContext).IsSameReferenceAs(context);
    }

    [Test]
    public async Task When_ContextSet_Should_FlowIntoChildAsyncFlow()
    {
        var accessor = new AmanhecerPipelineContextAccessor();
        var context = new AmanhecerContext();
        accessor.PipelineContext = context;

        var observed = await Task.Run(() => accessor.PipelineContext);

        await Assert.That(observed).IsSameReferenceAs(context);
    }

    [Test]
    public async Task When_ChildAsyncFlowChangesContext_Should_NotLeakToParentFlow()
    {
        var accessor = new AmanhecerPipelineContextAccessor();
        var parentContext = new AmanhecerContext();
        accessor.PipelineContext = parentContext;

        await Task.Run(() => accessor.PipelineContext = new AmanhecerContext());

        await Assert.That(accessor.PipelineContext).IsSameReferenceAs(parentContext);
    }
}
