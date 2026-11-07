using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Makoto.Generators.Model;

internal static class SymbolFormats
{
    /// <summary>global::-qualified, keeping nullable annotations.</summary>
    public static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static string ToQualifiedString(this ITypeSymbol type) => type.ToDisplayString(Qualified);

    /// <summary>Renders a constraint clause, e.g. <c>where T : global::System.IComparable&lt;T&gt;</c>, or null when unconstrained.</summary>
    public static string? ToConstraintClause(this ITypeParameterSymbol parameter)
    {
        var constraints = new List<string>();
        if (parameter.HasReferenceTypeConstraint)
        {
            constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
        }

        if (parameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (parameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }

        if (parameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        constraints.AddRange(parameter.ConstraintTypes.Select(ToQualifiedString));

        if (parameter.HasConstructorConstraint && !parameter.HasValueTypeConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count == 0 ? null : $"where {parameter.Name} : {string.Join(", ", constraints)}";
    }
}
