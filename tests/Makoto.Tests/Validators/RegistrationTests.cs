using Microsoft.Extensions.DependencyInjection;

namespace Makoto.Tests.Validators;

public class RegistrationTests
{
    private static ServiceProvider Provider() => new ServiceCollection().AddMakotoValidators().BuildServiceProvider();

    [Fact]
    public void Interface_resolves_to_the_generated_validator()
    {
        using var provider = Provider();

        var validator = provider.GetRequiredService<IValidator<Signup>>();

        Assert.IsType<SignupValidator>(validator);
        Assert.False(validator.Validate(new Signup { Email = "nope" }).IsValid);
    }

    [Fact]
    public void Concrete_type_resolves()
    {
        using var provider = Provider();

        Assert.NotNull(provider.GetRequiredService<AddressValidator>());
    }

    [Fact]
    public void Struct_models_register()
    {
        using var provider = Provider();

        Assert.IsType<CoordinatesValidator>(provider.GetRequiredService<IValidator<Coordinates>>());
    }

    [Fact]
    public void Validators_are_singletons()
    {
        using var provider = Provider();

        Assert.Same(provider.GetRequiredService<IValidator<Signup>>(), provider.GetRequiredService<IValidator<Signup>>());
    }

    [Fact]
    public void An_existing_registration_is_kept()
    {
        var custom = new AlwaysValid();
        var services = new ServiceCollection().AddSingleton<IValidator<Signup>>(custom).AddMakotoValidators();

        using var provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<IValidator<Signup>>());
    }

    [Fact]
    public void Calling_it_twice_adds_nothing()
    {
        var services = new ServiceCollection().AddMakotoValidators();
        var count = services.Count;

        services.AddMakotoValidators();

        Assert.Equal(count, services.Count);
    }

    private sealed class AlwaysValid : IValidator<Signup>
    {
        public ValidationResult Validate(Signup instance) => default;

        public bool IsValid(Signup instance) => true;
    }
}
