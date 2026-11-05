namespace Storefront.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void StampCreated(DateTimeOffset now)
    {
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void StampUpdated(DateTimeOffset now) => UpdatedAt = now;
}
