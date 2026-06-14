using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace FollowAlpha.LP.Architecture.Tests;

/// <summary>
/// Reads an assembly's metadata and reports references to forbidden members. Needed because some
/// purity rules (e.g. <c>DateTime.Now</c>) are property/method calls, not type dependencies, so
/// NetArchTest — which works on type-level dependencies — cannot see them.
/// </summary>
internal static class ForbiddenMemberScanner
{
    /// <summary>A forbidden member, identified by its declaring type's full name and member name.</summary>
    public readonly record struct ForbiddenMember(string DeclaringType, string Member)
    {
        public override string ToString() => $"{DeclaringType}.{Member}";
    }

    /// <summary>Returns the distinct forbidden members actually referenced by the assembly at <paramref name="assemblyPath"/>.</summary>
    public static ImmutableArray<string> Scan(string assemblyPath, IReadOnlyCollection<ForbiddenMember> forbidden)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var hits = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in reader.MemberReferences)
        {
            var memberRef = reader.GetMemberReference(handle);

            if (memberRef.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
            var ns = reader.GetString(typeRef.Namespace);
            var typeName = reader.GetString(typeRef.Name);
            var declaringType = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            var memberName = reader.GetString(memberRef.Name);

            foreach (var rule in forbidden)
            {
                if (declaringType == rule.DeclaringType && memberName == rule.Member)
                {
                    hits.Add(rule.ToString());
                }
            }
        }

        return hits.OrderBy(h => h, StringComparer.Ordinal).ToImmutableArray();
    }
}
