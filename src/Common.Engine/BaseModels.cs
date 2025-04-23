using Entities.DB.Entities;
using System.Text.Json.Serialization;

namespace Common.Engine;


public class EntityWithScore<T>
{
    public EntityWithScore(T entity, int score)
    {
        Entity = entity;
        Score = score;
    }
    public T Entity { get; set; }
    public int Score { get; set; }
}

public class BaseDTO
{
    public BaseDTO()
    {
    }
    public BaseDTO(AbstractEFEntity entity)
    {
        Id = entity.ID.ToString();
    }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public abstract class BaseDTOWithName : BaseDTO
{
    public BaseDTOWithName()
    {
    }
    public BaseDTOWithName(AbstractEFEntityWithName entity) : base(entity)
    {
        Name = entity.Name;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
