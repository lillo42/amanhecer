using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Exceptions;

namespace Amanhecer.Abstractions.Tests.Exceptions;

public class AmanhecerExceptionsTests
{
    [Test]
    public async Task AmanhecerException_Should_BeAnException()
    {
        await Assert.That(typeof(Exception).IsAssignableFrom(typeof(AmanhecerException))).IsTrue();
        await Assert.That(typeof(AmanhecerException).IsAbstract).IsTrue();
    }

    [Test]
    public async Task InvalidMessageException_DefaultConstructor_Should_HaveDefaultMessageAndNoInner()
    {
        var exception = new InvalidMessageException();

        await Assert.That(exception.Message)
            .IsEqualTo("The message could not be mapped to the expected request type.");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception is AmanhecerException).IsTrue();
    }

    [Test]
    public async Task InvalidMessageException_Should_StoreInnerException()
    {
        var inner = new FormatException("bad payload");

        var exception = new InvalidMessageException(inner);

        await Assert.That(ReferenceEquals(exception.InnerException, inner)).IsTrue();
    }

    [Test]
    public async Task InvalidMessageException_Should_UseCustomMessage()
    {
        var exception = new InvalidMessageException("custom message");

        await Assert.That(exception.Message).IsEqualTo("custom message");
    }

    [Test]
    public async Task MultiPipelineFoundException_Should_StoreRoutingKeyAndBuildMessage()
    {
        var exception = new MultiPipelineFoundException("orders");

        await Assert.That(exception.RoutingKey).IsEqualTo("orders");
        await Assert.That(exception.Message).IsEqualTo("Multi-pipeline found for routingKey: orders");
        await Assert.That(exception is AmanhecerException).IsTrue();
    }

    [Test]
    public async Task PipelineNotFoundException_Should_StoreRoutingKeyAndBuildMessage()
    {
        var exception = new PipelineNotFoundException("orders");

        await Assert.That(exception.RoutingKey).IsEqualTo("orders");
        await Assert.That(exception.Message).IsEqualTo("Pipeline not found for routingKey: orders");
        await Assert.That(exception is AmanhecerException).IsTrue();
    }
}
