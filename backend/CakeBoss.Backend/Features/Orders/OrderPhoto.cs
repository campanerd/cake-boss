using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Orders;

/// <summary>Foto de referência anexada ao pedido. O arquivo fica no storage; aqui só a referência.</summary>
public class OrderPhoto
{
    private OrderPhoto() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }

    /// <summary>Caminho do objeto no bucket (privado) do storage.</summary>
    public string StorageKey { get; private set; } = "";

    public string OriginalName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long SizeBytes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static OrderPhoto Create(string storageKey, string originalName, string contentType, long sizeBytes, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("Foto sem referência de armazenamento.");
        if (sizeBytes <= 0)
            throw new DomainException("Foto vazia.");

        return new OrderPhoto
        {
            Id = Guid.CreateVersion7(),
            StorageKey = storageKey,
            OriginalName = TextCleaner.Clean(Path.GetFileName(originalName), 200) ?? "photo",
            ContentType = TextCleaner.Clean(contentType, 100) ?? "application/octet-stream",
            SizeBytes = sizeBytes,
            CreatedAt = nowUtc
        };
    }
}
