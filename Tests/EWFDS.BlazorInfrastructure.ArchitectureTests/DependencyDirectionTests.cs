using System.Reflection;
using EWFDS.BlazorInfrastructure.Common.ErrorHandling;
using Xunit;

namespace EWFDS.BlazorInfrastructure.ArchitectureTests;

/// <summary>
/// Reflection-based architecture guardrails that enforce the dependency direction
/// documented in docs/Architecture-Separation.md §5 rule 2:
///
///   Blazor -> Common   and   Api -> Common
///   Common must NEVER depend on Blazor.* or Api.*
///   Blazor and Api must NOT depend on each other.
///
/// The check inspects the public/non-public signature surface of every type
/// (base type, interfaces, fields, properties, method parameters and returns,
/// generic arguments) and fails if a type in one area references a type in a
/// forbidden area.
/// </summary>
public class DependencyDirectionTests
{
    private const string RootNamespace = "EWFDS.BlazorInfrastructure";
    private const string CommonNamespace = RootNamespace + ".Common";
    private const string BlazorNamespace = RootNamespace + ".Blazor";
    private const string ApiNamespace = RootNamespace + ".Api";

    // Any type from the infrastructure assembly is a stable anchor for the assembly under test.
    private static readonly Assembly InfrastructureAssembly = typeof(IGlobalErrorHandler).Assembly;

    [Fact]
    public void Common_must_not_depend_on_Blazor()
    {
        var violations = FindViolations(
            sourceAreaNamespace: CommonNamespace,
            forbiddenAreaNamespace: BlazorNamespace);

        Assert.True(
            violations.Count == 0,
            "Common must never depend on Blazor.*:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Common_must_not_depend_on_Api()
    {
        var violations = FindViolations(
            sourceAreaNamespace: CommonNamespace,
            forbiddenAreaNamespace: ApiNamespace);

        Assert.True(
            violations.Count == 0,
            "Common must never depend on Api.*:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Blazor_must_not_depend_on_Api()
    {
        var violations = FindViolations(
            sourceAreaNamespace: BlazorNamespace,
            forbiddenAreaNamespace: ApiNamespace);

        Assert.True(
            violations.Count == 0,
            "Blazor must never depend on Api.*:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Api_must_not_depend_on_Blazor()
    {
        var violations = FindViolations(
            sourceAreaNamespace: ApiNamespace,
            forbiddenAreaNamespace: BlazorNamespace);

        Assert.True(
            violations.Count == 0,
            "Api must never depend on Blazor.*:\n" + string.Join("\n", violations));
    }

    private static List<string> FindViolations(string sourceAreaNamespace, string forbiddenAreaNamespace)
    {
        var violations = new List<string>();

        foreach (var type in GetTypesInArea(sourceAreaNamespace))
        {
            foreach (var referenced in GetReferencedTypes(type))
            {
                if (IsInArea(referenced, forbiddenAreaNamespace))
                {
                    violations.Add($"  {type.FullName} -> {referenced.FullName}");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<Type> GetTypesInArea(string areaNamespace)
    {
        Type[] types;
        try
        {
            types = InfrastructureAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        return types.Where(t => IsInArea(t, areaNamespace));
    }

    private static bool IsInArea(Type type, string areaNamespace)
    {
        var ns = ResolveElementType(type).Namespace;
        return ns is not null
            && (ns == areaNamespace || ns.StartsWith(areaNamespace + ".", StringComparison.Ordinal));
    }

    /// <summary>
    /// Collects the set of types that appear on a type's signature surface: base type,
    /// implemented interfaces, generic arguments, field/property types, and every method's
    /// parameter and return types (including constructors). This captures the dependencies
    /// that reflection can observe without reading IL bodies.
    /// </summary>
    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var referenced = new HashSet<Type>();

        void Add(Type? t)
        {
            if (t is null)
            {
                return;
            }

            var element = ResolveElementType(t);
            if (referenced.Add(element) && element.IsGenericType)
            {
                foreach (var arg in element.GetGenericArguments())
                {
                    Add(arg);
                }
            }
        }

        if (type.BaseType is not null)
        {
            Add(type.BaseType);
        }

        foreach (var iface in type.GetInterfaces())
        {
            Add(iface);
        }

        foreach (var field in type.GetFields(all))
        {
            Add(field.FieldType);
        }

        foreach (var property in type.GetProperties(all))
        {
            Add(property.PropertyType);
        }

        foreach (var method in type.GetMethods(all))
        {
            Add(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                Add(parameter.ParameterType);
            }
        }

        foreach (var ctor in type.GetConstructors(all))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                Add(parameter.ParameterType);
            }
        }

        return referenced;
    }

    /// <summary>
    /// Unwraps arrays, by-ref, pointer and constructed generic types down to the
    /// underlying element / generic-definition type so its namespace can be checked.
    /// </summary>
    private static Type ResolveElementType(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        if (type.IsConstructedGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        return type;
    }
}
