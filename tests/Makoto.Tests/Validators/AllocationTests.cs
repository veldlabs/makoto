namespace Makoto.Tests.Validators;

// IsEmail is a Regex, and a Regex whose cached runner is in use on another thread allocates a new one. Other tests
// validating at the same moment would show up here, though the generated code has nothing to do with it.
[CollectionDefinition(nameof(AllocationTests), DisableParallelization = true)]
public sealed class AllocationCollection;

[Collection(nameof(AllocationTests))]
public class AllocationTests
{
    private const int Iterations = 1_000;

    private static long Measure(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static Signup Sample()
    {
        // No IEnumerable here: walking an interface allocates its enumerator, which the design accepts.
        var signup = ValidatorTests.Valid();
        signup.Aliases = null;
        return signup;
    }

    [Fact]
    public void Validate_allocates_nothing_on_success()
    {
        IValidator<Signup> validator = new SignupValidator();
        var signup = Sample();
        var validate = () => { validator.Validate(signup); };

        Measure(validate);

        Assert.Equal(0, Measure(validate));
    }

    [Fact]
    public void IsValid_allocates_nothing_on_success()
    {
        IValidator<Signup> validator = new SignupValidator();
        var signup = Sample();
        var isValid = () => { validator.IsValid(signup); };

        Measure(isValid);

        Assert.Equal(0, Measure(isValid));
    }

    [Fact]
    public void The_measure_sees_the_failure_path_allocate()
    {
        IValidator<Signup> validator = new SignupValidator();
        var signup = Sample();
        signup.Email = "nope";
        var validate = () => { validator.Validate(signup); };

        Measure(validate);

        Assert.True(Measure(validate) > 0);
    }
}
