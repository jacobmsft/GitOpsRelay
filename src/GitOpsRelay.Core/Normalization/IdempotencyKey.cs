using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace GitOpsRelay.Core.Normalization;

public static class IdempotencyKey
{
    public static string Create(params string?[] components)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("v1"u8);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var component in components)
        {
            var bytes = component is null
                ? []
                : Encoding.UTF8.GetBytes(component.Normalize(NormalizationForm.FormC));
            BinaryPrimitives.WriteInt32BigEndian(length, component is null ? -1 : bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return $"v1-{Convert.ToHexStringLower(hash.GetHashAndReset())}";
    }
}
