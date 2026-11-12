namespace EasyKeep.Services;

/// <summary>Статус «Светофора дефицита».</summary>
public enum DeficitStatus
{
    // Порядок важен для сортировки: сначала самые проблемные позиции.
    Critical = 0,
    Warning = 1,
    Normal = 2,
    NonLiquid = 3
}
