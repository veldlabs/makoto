namespace Makoto;

/// <summary>A single failed rule. Generated validators share one immutable instance per rule.</summary>
public sealed class ValidationFailure(string propertyName, string errorCode, string message)
{
    /// <summary>The path of the failing value, e.g. <c>Items[3].Name</c>.</summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>The rule's code.</summary>
    public string ErrorCode { get; } = errorCode;

    /// <summary>The rule's message.</summary>
    public string Message { get; } = message;

    /// <inheritdoc />
    public override string ToString() => $"{PropertyName}: {Message}";

    internal ValidationFailure WithPath(string path) =>
        new(PropertyName.Length == 0 ? path : $"{path}.{PropertyName}", ErrorCode, Message);

    internal ValidationFailure WithElementPath(string path, int index)
    {
        // A stack buffer, because the default handler rents from ArrayPool for every path.
        Span<char> buffer = stackalloc char[128];
        var full = PropertyName.Length == 0
            ? string.Create(null, buffer, $"{path}[{index}]")
            : string.Create(null, buffer, $"{path}[{index}].{PropertyName}");
        return new ValidationFailure(full, ErrorCode, Message);
    }
}

/// <summary>The outcome of a validation. <c>default</c> is a success, and a success allocates nothing.</summary>
public readonly struct ValidationResult
{
    private readonly ValidationFailure[]? _failures;
    private readonly int _count;

    internal ValidationResult(ValidationFailure[] failures, int count)
    {
        _failures = failures;
        _count = count;
    }

    /// <summary>True when no rule failed.</summary>
    public bool IsValid => _count == 0;

    /// <summary>The failed rules, in declaration order.</summary>
    public ReadOnlySpan<ValidationFailure> Failures => new(_failures, 0, _count);

    /// <summary>Throws a <see cref="ValidationException"/> when any rule failed.</summary>
    public void ThrowIfInvalid()
    {
        if (_count != 0)
        {
            throw new ValidationException(Failures.ToArray());
        }
    }
}

/// <summary>Thrown by <see cref="ValidationResult.ThrowIfInvalid"/>.</summary>
public sealed class ValidationException(IReadOnlyList<ValidationFailure> failures)
    : Exception($"Validation failed: {string.Join("; ", failures)}")
{
    /// <summary>The failed rules.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; } = failures;
}

/// <summary>Accumulates failures for generated code. The array is only allocated on the first failure.</summary>
public ref struct FailureCollector
{
    private ValidationFailure[]? _items;
    private int _count;

    /// <summary>The number of failures so far.</summary>
    public readonly int Count => _count;

    /// <summary>Adds a failure.</summary>
    public void Add(ValidationFailure failure)
    {
        if (_items is null || _count == _items.Length)
        {
            Array.Resize(ref _items, _count == 0 ? 4 : _count * 2);
        }

        _items[_count++] = failure;
    }

    /// <summary>Prepends <paramref name="path"/> to everything added since <paramref name="start"/>.</summary>
    public readonly void PrefixFrom(int start, string path)
    {
        for (var i = start; i < _count; i++)
        {
            _items![i] = _items[i].WithPath(path);
        }
    }

    /// <summary>Prepends <c>path[index]</c> to everything added since <paramref name="start"/>.</summary>
    public readonly void PrefixFrom(int start, string path, int index)
    {
        for (var i = start; i < _count; i++)
        {
            _items![i] = _items[i].WithElementPath(path, index);
        }
    }

    /// <summary>Builds the result.</summary>
    public readonly ValidationResult ToResult() => _count == 0 ? default : new ValidationResult(_items!, _count);
}
