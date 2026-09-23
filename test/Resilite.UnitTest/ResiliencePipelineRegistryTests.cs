using System;
using FluentAssertions;
using NSubstitute;

namespace Resilite.UnitTest;

public sealed class ResiliencePipelineRegistryTests
{
    [Fact]
    public void Constructor_WhenRegistrationsIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
            new ResiliencePipelineRegistry(null!);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenRegistrationsAreProvided_ShouldRegisterAllPipelines()
    {
        // Arrange
        var pipeline1 = Substitute.For<IResiliencePipeline>();
        var pipeline2 = Substitute.For<IResiliencePipeline>();

        var registrations = new[]
        {
            new ResiliencePipelineRegistration(
                "Pipeline1",
                pipeline1),

            new ResiliencePipelineRegistration(
                "Pipeline2",
                pipeline2)
        };

        // Act
        var sut =
            new ResiliencePipelineRegistry(registrations);

        // Assert
        sut.Get("Pipeline1")
            .Should()
            .BeSameAs(pipeline1);

        sut.Get("Pipeline2")
            .Should()
            .BeSameAs(pipeline2);
    }

    [Fact]
    public void Register_WhenRegistrationIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var sut = CreateRegistry();

        // Act
        Action act = () =>
            sut.Register(null!);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Register_WhenNameIsEmptyOrWhitespace_ShouldThrowArgumentException(
        string name)
    {
        // Arrange
        var sut = CreateRegistry();

        var pipeline =
            Substitute.For<IResiliencePipeline>();

        var registration =
            new ResiliencePipelineRegistration(
                name,
                pipeline);

        // Act
        Action act = () =>
            sut.Register(registration);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentException>();

        assertion.Which.ParamName
            .Should()
            .Be(nameof(registration.Name));
    }

    [Fact]
    public void Register_WhenPipelineIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var sut = CreateRegistry();

        var registration =
            new ResiliencePipelineRegistration(
                "Test",
                null!);

        // Act
        Action act = () =>
            sut.Register(registration);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_WhenRegistrationIsValid_ShouldRegisterPipeline()
    {
        // Arrange
        var sut = CreateRegistry();

        var pipeline =
            Substitute.For<IResiliencePipeline>();

        var registration =
            new ResiliencePipelineRegistration(
                "TestPipeline",
                pipeline);

        // Act
        sut.Register(registration);

        // Assert
        sut.Get("TestPipeline")
            .Should()
            .BeSameAs(pipeline);
    }

    [Fact]
    public void Register_WhenSameNameAlreadyExists_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline1 =
            Substitute.For<IResiliencePipeline>();

        var pipeline2 =
            Substitute.For<IResiliencePipeline>();

        var sut = new ResiliencePipelineRegistry(
            new[]
            {
                new ResiliencePipelineRegistration(
                    "TestPipeline",
                    pipeline1)
            });

        // Act
        Action act = () =>
            sut.Register(
                new ResiliencePipelineRegistration(
                    "TestPipeline",
                    pipeline2));

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*TestPipeline*already registered*");
    }

    [Fact]
    public void Register_WhenSameNameUsesDifferentCasing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline1 =
            Substitute.For<IResiliencePipeline>();

        var pipeline2 =
            Substitute.For<IResiliencePipeline>();

        var sut = new ResiliencePipelineRegistry(
            new[]
            {
                new ResiliencePipelineRegistration(
                    "PaymentApi",
                    pipeline1)
            });

        // Act
        Action act = () =>
            sut.Register(
                new ResiliencePipelineRegistration(
                    "paymentapi",
                    pipeline2));

        // Assert
        act.Should()
            .Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Get_WhenNameIsEmptyOrWhitespace_ShouldThrowArgumentException(
        string name)
    {
        // Arrange
        var sut = CreateRegistry();

        // Act
        Action act = () =>
            sut.Get(name);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentException>();

        assertion.Which.ParamName
            .Should()
            .Be("name");
    }

    [Fact]
    public void Get_WhenPipelineExists_ShouldReturnPipeline()
    {
        // Arrange
        var pipeline =
            Substitute.For<IResiliencePipeline>();

        var sut = new ResiliencePipelineRegistry(
            new[]
            {
                new ResiliencePipelineRegistration(
                    "PaymentApi",
                    pipeline)
            });

        // Act
        var result =
            sut.Get("PaymentApi");

        // Assert
        result.Should()
            .BeSameAs(pipeline);
    }

    [Theory]
    [InlineData("paymentapi")]
    [InlineData("PAYMENTAPI")]
    [InlineData("PaymentApi")]
    [InlineData("pAyMeNtApI")]
    public void Get_ShouldBeCaseInsensitive(
        string name)
    {
        // Arrange
        var pipeline =
            Substitute.For<IResiliencePipeline>();

        var sut = new ResiliencePipelineRegistry(
            new[]
            {
                new ResiliencePipelineRegistration(
                    "PaymentApi",
                    pipeline)
            });

        // Act
        var result = sut.Get(name);

        // Assert
        result.Should()
            .BeSameAs(pipeline);
    }

    [Fact]
    public void Get_WhenPipelineDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var sut = CreateRegistry();

        // Act
        Action act = () =>
            sut.Get("UnknownPipeline");

        // Assert
        act.Should()
            .Throw<KeyNotFoundException>()
            .WithMessage("*UnknownPipeline*");
    }

    [Fact]
    public async Task Register_WhenManyDifferentPipelinesAreRegisteredConcurrently_ShouldRegisterAll()
    {
        // Arrange
        var sut = CreateRegistry();

        const int count = 100;

        var pipelines = Enumerable
            .Range(0, count)
            .Select(index => new
            {
                Name = $"Pipeline-{index}",
                Pipeline =
                    Substitute.For<IResiliencePipeline>()
            })
            .ToArray();

        // Act
        await Task.WhenAll(
            pipelines.Select(item =>
                Task.Run(() =>
                    sut.Register(
                        new ResiliencePipelineRegistration(
                            item.Name,
                            item.Pipeline)))));

        // Assert
        foreach (var item in pipelines)
        {
            sut.Get(item.Name)
                .Should()
                .BeSameAs(item.Pipeline);
        }
    }

    [Fact]
    public async Task Register_WhenSameNameIsRegisteredConcurrently_ShouldAllowOnlyOneRegistration()
    {
        // Arrange
        var sut = CreateRegistry();

        const int count = 50;

        var successCount = 0;
        var duplicateCount = 0;

        // Act
        var tasks = Enumerable
            .Range(0, count)
            .Select(_ =>
                Task.Run(() =>
                {
                    try
                    {
                        sut.Register(
                            new ResiliencePipelineRegistration(
                                "SharedPipeline",
                                Substitute.For<IResiliencePipeline>()));

                        Interlocked.Increment(
                            ref successCount);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(
                            ref duplicateCount);
                    }
                }));

        await Task.WhenAll(tasks);

        // Assert
        successCount.Should().Be(1);

        duplicateCount.Should()
            .Be(count - 1);

        sut.Get("SharedPipeline")
            .Should()
            .NotBeNull();
    }

    private static ResiliencePipelineRegistry CreateRegistry()
    {
        return new ResiliencePipelineRegistry(
            Array.Empty<ResiliencePipelineRegistration>());
    }
}
