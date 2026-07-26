using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Net.Agora.Mac.PackageTests;

/// <summary>
/// Reads the public API out of a binding assembly using metadata only. The assembly targets
/// *-macos and references Microsoft.macOS, so it cannot be loaded into the test process; the
/// metadata reader lets these tests run on a plain net9.0 runner with no macOS workload.
/// Mirrors the Android repository's AssemblyApi.
/// </summary>
public sealed class AssemblyApi : IDisposable
{
    private readonly PEReader _peReader;
    private readonly MetadataReader _metadata;
    private IReadOnlyList<string>? _publicTypes;

    public AssemblyApi(Stream assembly)
    {
        _peReader = new PEReader(assembly);
        _metadata = _peReader.GetMetadataReader();
    }

    /// <summary>Namespace-qualified names of every public top-level type.</summary>
    public IReadOnlyList<string> PublicTypes => _publicTypes ??= _metadata.TypeDefinitions
        .Select(_metadata.GetTypeDefinition)
        .Where(type => (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
        .Select(FullNameOf)
        .ToList();

    public IReadOnlyList<string> MethodsOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetMethods()
            .Select(_metadata.GetMethodDefinition)
            .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
            .Select(method => _metadata.GetString(method.Name))
            .ToList();
    }

    public IReadOnlyList<string> PropertiesOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetProperties()
            .Select(_metadata.GetPropertyDefinition)
            .Select(property => _metadata.GetString(property.Name))
            .ToList();
    }

    /// <summary>
    /// Names of a type's events. Events carry no visibility of their own — it lives on the
    /// accessors — and everything the binding generator emits here is public, so the names alone
    /// are what is worth asserting.
    /// </summary>
    public IReadOnlyList<string> EventsOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetEvents()
            .Select(_metadata.GetEventDefinition)
            .Select(@event => _metadata.GetString(@event.Name))
            .ToList();
    }

    /// <summary>
    /// The parameter count of every public instance constructor of a type. An empty list means
    /// the type cannot be constructed directly at all; a list without 0 means it cannot be
    /// constructed without arguments.
    /// </summary>
    public IReadOnlyList<int> PublicConstructorArities(string typeFullName)
    {
        var type = FindType(typeFullName);
        var arities = new List<int>();

        foreach (var handle in type.GetMethods())
        {
            var method = _metadata.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public ||
                _metadata.GetString(method.Name) != ".ctor")
            {
                continue;
            }

            // The parameter count comes from the signature blob rather than GetParameters():
            // the Parameter table also carries a row for an attributed return value, which would
            // make a parameterless method look one-argument. Constructors are never generic, so
            // the count is the first integer after the header.
            var signature = _metadata.GetBlobReader(method.Signature);
            signature.ReadSignatureHeader();
            arities.Add(signature.ReadCompressedInteger());
        }

        return arities;
    }

    private TypeDefinition FindType(string typeFullName)
    {
        foreach (var handle in _metadata.TypeDefinitions)
        {
            var type = _metadata.GetTypeDefinition(handle);
            if (FullNameOf(type) == typeFullName)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"Type '{typeFullName}' is not defined in this assembly.");
    }

    private string FullNameOf(TypeDefinition type)
    {
        var name = _metadata.GetString(type.Name);
        var ns = _metadata.GetString(type.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public void Dispose() => _peReader.Dispose();
}
