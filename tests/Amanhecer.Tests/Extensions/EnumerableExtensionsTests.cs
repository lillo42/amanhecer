using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace Amanhecer.Tests.Extensions;

public class EnumerableExtensionsTests
{
    private const string ReflectionMessage =
        "Uses reflection to reach the internal EnumerableExtensions type.";

    private const string EquivalencyMessage =
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.";

    [Test]
    [RequiresUnreferencedCode(EquivalencyMessage + " " + ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_Should_ReturnSourceFollowedByOther()
    {
        var result = AppendRange([1, 2], [3, 4]).ToList();

        await Assert.That(result)
            .IsEquivalentTo([1, 2, 3, 4], CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(EquivalencyMessage + " " + ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_WithEmptySource_Should_ReturnOther()
    {
        var result = AppendRange([], [1, 2]).ToList();

        await Assert.That(result)
            .IsEquivalentTo([1, 2], CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(EquivalencyMessage + " " + ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_WithEmptyOther_Should_ReturnSource()
    {
        var result = AppendRange([1, 2], []).ToList();

        await Assert.That(result)
            .IsEquivalentTo([1, 2], CollectionOrdering.Matching);
    }

    [Test]
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_WithBothEmpty_Should_ReturnEmpty()
    {
        var result = AppendRange([], []).ToList();

        await Assert.That(result).Count().IsEqualTo(0);
    }

    [Test]
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_WithNullSource_Should_ThrowArgumentNullException()
    {
        await Assert.That(() => AppendRange(null!, []))
            .ThrowsExactly<System.ArgumentNullException>()
            .WithParameterName("source");
    }

    [Test]
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_WithNullOther_Should_ThrowArgumentNullException()
    {
        await Assert.That(() => AppendRange([], null!))
            .ThrowsExactly<System.ArgumentNullException>()
            .WithParameterName("other");
    }

    [Test]
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_Should_NotEnumerateSourceUntilIterated()
    {
        var enumerated = false;

        IEnumerable<int> Source()
        {
            enumerated = true;
            yield return 1;
        }

        var result = AppendRange(Source(), [2]);

        await Assert.That(enumerated).IsFalse();

        var items = result.ToList();

        await Assert.That(enumerated).IsTrue();
        await Assert.That(items).Count().IsEqualTo(2);
    }

    [Test]
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public async Task When_AppendRange_Should_AllowMultipleEnumerations()
    {
        var result = AppendRange([1, 2], [3]);

        var first = result.ToList();
        var second = result.ToList();

        await Assert.That(first).Count().IsEqualTo(3);
        await Assert.That(second).Count().IsEqualTo(3);
        await Assert.That(second[0]).IsEqualTo(first[0]);
        await Assert.That(second[2]).IsEqualTo(first[2]);
    }

    // EnumerableExtensions is internal and no InternalsVisibleTo is granted to the test
    // assembly, so AppendRange is exercised through reflection.
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    private static IEnumerable<int> AppendRange(IEnumerable<int> source, IEnumerable<int> other)
    {
        try
        {
            return (IEnumerable<int>)GetAppendRangeMethod().Invoke(null, [source, other])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(exception.InnerException);
            throw;
        }
    }

    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    private static MethodInfo GetAppendRangeMethod()
    {
        return typeof(AmanhecerPipeline).Assembly
            .GetType("Amanhecer.Extensions.EnumerableExtensions", throwOnError: true)!
            .GetMethod("AppendRange", BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int));
    }
}
