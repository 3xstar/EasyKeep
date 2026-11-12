using System.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Data.Reports;
using Microsoft.Data.SqlClient;

namespace EasyKeep.Data;

/// <summary>
/// Чистый ADO.NET для проведения движений и аналитических отчётов.
/// Все изменения остатков выполняются в одной транзакции с блокировкой строк.
/// </summary>
public class AdoNetMovementRepository
{
    /// <summary>
    /// Проводит документ IN/OUT/TRANSFER: шапка, строки и остатки
    /// изменяются атомарно. При любой ошибке выполняется откат.
    /// </summary>
    public int CreateMovement(Movement movement)
    {
        ValidateMovement(movement);

        using SqlConnection connection = OpenConnection();
        using SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            int movementId = InsertMovementHeader(connection, transaction, movement);

            foreach (MovementItem line in movement.Items)
                ApplyMovementLine(connection, transaction, movementId, movement, line);

            transaction.Commit();
            movement.Id = movementId;
            return movementId;
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            // Не показываем пользователю технические детали SQL Server.
            if (ex is SqlException)
            {
                throw new InvalidOperationException(
                    "Не удалось провести документ. Проверьте товары, склады и остатки.", ex);
            }

            throw;
        }
    }

    /// <summary>Журнал движений с фильтрами по периоду, типу, складу и товару.</summary>
    public List<MovementJournalRow> GetJournal(
        DateTime from,
        DateTime to,
        string? type = null,
        int? warehouseId = null,
        string? search = null)
    {
        const string sql = """
            SELECT m.id,
                   m.type,
                   m.created_at,
                   fw.name AS from_warehouse,
                   tw.name AS to_warehouse,
                   m.comment,
                   COUNT(mi.id) AS items_count,
                   ISNULL(SUM(mi.quantity), 0) AS total_quantity
            FROM dbo.movements m
            LEFT JOIN dbo.warehouses fw ON fw.id = m.from_warehouse_id
            LEFT JOIN dbo.warehouses tw ON tw.id = m.to_warehouse_id
            JOIN dbo.movement_items mi ON mi.movement_id = m.id
            LEFT JOIN dbo.items i ON i.id = mi.item_id
            WHERE m.created_at >= @from
              AND m.created_at < @to
              AND (@type IS NULL OR m.type = @type)
              AND (@warehouseId IS NULL
                   OR m.from_warehouse_id = @warehouseId
                   OR m.to_warehouse_id = @warehouseId)
              AND (@search IS NULL OR EXISTS (
                    SELECT 1
                    FROM dbo.movement_items mi_search
                    JOIN dbo.items i_search ON i_search.id = mi_search.item_id
                    WHERE mi_search.movement_id = m.id
                      AND (i_search.name LIKE @search OR i_search.sku LIKE @search)
              ))
            GROUP BY m.id, m.type, m.created_at, fw.name, tw.name, m.comment
            ORDER BY m.created_at DESC, m.id DESC;
            """;

        using SqlConnection connection = OpenConnection();
        using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@from", from);
        command.Parameters.AddWithValue("@to", to);
        command.Parameters.AddWithValue("@type", (object?)NormalizeType(type) ?? DBNull.Value);
        command.Parameters.AddWithValue("@warehouseId", (object?)(warehouseId is > 0 ? warehouseId : null) ?? DBNull.Value);
        command.Parameters.AddWithValue("@search",
            string.IsNullOrWhiteSpace(search) ? DBNull.Value : $"%{search.Trim()}%");

        using SqlDataReader reader = command.ExecuteReader();
        List<MovementJournalRow> rows = new();

        while (reader.Read())
        {
            rows.Add(new MovementJournalRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7)));
        }

        return rows;
    }

    /// <summary>Сводка остатков, количества позиций и стоимости по складам.</summary>
    public List<WarehouseStockRow> GetStockReport()
    {
        const string sql = """
            SELECT w.name,
                   COUNT(s.id) AS items_count,
                   ISNULL(SUM(s.quantity), 0) AS total_quantity,
                   ISNULL(SUM(s.quantity * i.price), 0) AS total_value
            FROM dbo.warehouses w
            LEFT JOIN dbo.stocks s ON s.warehouse_id = w.id
            LEFT JOIN dbo.items i ON i.id = s.item_id
            GROUP BY w.name
            ORDER BY w.name;
            """;

        using SqlConnection connection = OpenConnection();
        using SqlCommand command = new(sql, connection);
        using SqlDataReader reader = command.ExecuteReader();
        List<WarehouseStockRow> rows = new();

        while (reader.Read())
        {
            rows.Add(new WarehouseStockRow(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetDecimal(3)));
        }

        return rows;
    }

    /// <summary>Топ товаров по расходу и перемещениям за период.</summary>
    public List<TopItemRow> GetTopItems(DateTime from, DateTime to, int limit = 10)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Количество строк должно быть больше нуля.");

        const string sql = """
            SELECT TOP (@limit)
                   i.sku,
                   i.name,
                   SUM(CASE WHEN m.type = N'OUT' THEN mi.quantity ELSE 0 END) AS out_quantity,
                   SUM(CASE WHEN m.type = N'TRANSFER' THEN mi.quantity ELSE 0 END) AS transfer_quantity,
                   COUNT(DISTINCT m.id) AS movements_count
            FROM dbo.items i
            JOIN dbo.movement_items mi ON mi.item_id = i.id
            JOIN dbo.movements m ON m.id = mi.movement_id
            WHERE m.created_at >= @from AND m.created_at < @to
            GROUP BY i.sku, i.name
            ORDER BY out_quantity DESC, transfer_quantity DESC, i.name;
            """;

        using SqlConnection connection = OpenConnection();
        using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@from", from);
        command.Parameters.AddWithValue("@to", to);

        using SqlDataReader reader = command.ExecuteReader();
        List<TopItemRow> rows = new();

        while (reader.Read())
        {
            rows.Add(new TopItemRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4)));
        }

        return rows;
    }

    /// <summary>История движений одного товара для панели «Рентген товара».</summary>
    public List<ItemMovementRow> GetItemHistory(int itemId, int limit = 50)
    {
        if (itemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemId), "Не указан товар.");

        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Количество строк должно быть больше нуля.");

        const string sql = """
            SELECT TOP (@limit)
                   m.id,
                   m.created_at,
                   m.type,
                   fw.name AS from_warehouse,
                   tw.name AS to_warehouse,
                   mi.quantity,
                   m.comment
            FROM dbo.movement_items mi
            JOIN dbo.movements m ON m.id = mi.movement_id
            LEFT JOIN dbo.warehouses fw ON fw.id = m.from_warehouse_id
            LEFT JOIN dbo.warehouses tw ON tw.id = m.to_warehouse_id
            WHERE mi.item_id = @itemId
            ORDER BY m.created_at DESC, m.id DESC;
            """;

        using SqlConnection connection = OpenConnection();
        using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@itemId", itemId);

        using SqlDataReader reader = command.ExecuteReader();
        List<ItemMovementRow> rows = new();

        while (reader.Read())
        {
            rows.Add(new ItemMovementRow(
                reader.GetInt32(0),
                reader.GetDateTime(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private static SqlConnection OpenConnection()
    {
        SqlConnection connection = new(AppConfig.GetConnectionString());
        connection.Open();
        return connection;
    }

    private static int InsertMovementHeader(
        SqlConnection connection,
        SqlTransaction transaction,
        Movement movement)
    {
        const string sql = """
            INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
            OUTPUT INSERTED.id
            VALUES (@type, @fromWarehouseId, @toWarehouseId, @createdAt, @comment);
            """;

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@type", movement.Type);
        command.Parameters.AddWithValue("@fromWarehouseId", (object?)movement.FromWarehouseId ?? DBNull.Value);
        command.Parameters.AddWithValue("@toWarehouseId", (object?)movement.ToWarehouseId ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", movement.CreatedAt);
        command.Parameters.AddWithValue("@comment", (object?)movement.Comment ?? DBNull.Value);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ApplyMovementLine(
        SqlConnection connection,
        SqlTransaction transaction,
        int movementId,
        Movement movement,
        MovementItem line)
    {
        switch (movement.Type)
        {
            case MovementTypes.In:
                IncreaseStock(connection, transaction, movement.ToWarehouseId!.Value, line);
                break;

            case MovementTypes.Out:
                DecreaseStock(connection, transaction, movement.FromWarehouseId!.Value, line);
                break;

            case MovementTypes.Transfer:
                // Сначала списываем у источника, затем добавляем получателю.
                DecreaseStock(connection, transaction, movement.FromWarehouseId!.Value, line);
                IncreaseStock(connection, transaction, movement.ToWarehouseId!.Value, line);
                break;
        }

        InsertMovementItem(connection, transaction, movementId, line);
    }

    private static void InsertMovementItem(
        SqlConnection connection,
        SqlTransaction transaction,
        int movementId,
        MovementItem line)
    {
        const string sql = """
            INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
            VALUES (@movementId, @itemId, @quantity);
            """;

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@movementId", movementId);
        command.Parameters.AddWithValue("@itemId", line.ItemId);
        command.Parameters.AddWithValue("@quantity", line.Quantity);
        command.ExecuteNonQuery();
    }

    /// <summary>Создаёт строку остатка с нулём, если товара ещё не было на складе.</summary>
    private static void EnsureStockRow(
        SqlConnection connection,
        SqlTransaction transaction,
        int warehouseId,
        int itemId)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.stocks WITH (UPDLOCK, ROWLOCK)
                WHERE warehouse_id = @warehouseId AND item_id = @itemId
            )
            BEGIN
                INSERT INTO dbo.stocks (warehouse_id, item_id, quantity)
                VALUES (@warehouseId, @itemId, 0);
            END;
            """;

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@warehouseId", warehouseId);
        command.Parameters.AddWithValue("@itemId", itemId);
        command.ExecuteNonQuery();
    }

    private static void DecreaseStock(
        SqlConnection connection,
        SqlTransaction transaction,
        int warehouseId,
        MovementItem line)
    {
        EnsureStockRow(connection, transaction, warehouseId, line.ItemId);

        // UPDLOCK не даёт другому пользователю изменить остаток до конца транзакции.
        const string selectSql = """
            SELECT i.name, s.quantity
            FROM dbo.stocks s WITH (UPDLOCK, ROWLOCK)
            JOIN dbo.items i ON i.id = s.item_id
            WHERE s.warehouse_id = @warehouseId AND s.item_id = @itemId;
            """;

        string itemName;
        int available;

        using (SqlCommand selectCommand = new(selectSql, connection, transaction))
        {
            selectCommand.Parameters.AddWithValue("@warehouseId", warehouseId);
            selectCommand.Parameters.AddWithValue("@itemId", line.ItemId);

            using SqlDataReader reader = selectCommand.ExecuteReader(CommandBehavior.SingleRow);
            if (!reader.Read())
                throw new InvalidOperationException("Не удалось прочитать остаток товара.");

            itemName = reader.GetString(0);
            available = reader.GetInt32(1);
        }

        // Понятная ошибка для «умной валидации»: интерфейс покажет доступный максимум.
        if (available < line.Quantity)
            throw new InsufficientStockException(itemName, available, line.Quantity);

        const string updateSql = """
            UPDATE dbo.stocks
            SET quantity = quantity - @quantity
            WHERE warehouse_id = @warehouseId AND item_id = @itemId;
            """;

        using SqlCommand updateCommand = new(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@quantity", line.Quantity);
        updateCommand.Parameters.AddWithValue("@warehouseId", warehouseId);
        updateCommand.Parameters.AddWithValue("@itemId", line.ItemId);
        updateCommand.ExecuteNonQuery();
    }

    private static void IncreaseStock(
        SqlConnection connection,
        SqlTransaction transaction,
        int warehouseId,
        MovementItem line)
    {
        EnsureStockRow(connection, transaction, warehouseId, line.ItemId);

        const string sql = """
            UPDATE dbo.stocks
            SET quantity = quantity + @quantity
            WHERE warehouse_id = @warehouseId AND item_id = @itemId;
            """;

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@quantity", line.Quantity);
        command.Parameters.AddWithValue("@warehouseId", warehouseId);
        command.Parameters.AddWithValue("@itemId", line.ItemId);
        command.ExecuteNonQuery();
    }

    private static void ValidateMovement(Movement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        if (movement.Items.Count == 0)
            throw new InvalidOperationException("Накладная не содержит товарных позиций.");

        ValidateWarehouses(movement);
        ValidateLines(movement.Items);

        if (movement.CreatedAt == default)
            movement.CreatedAt = DateTime.Now;

        if (movement.Comment?.Length > 500)
            throw new InvalidOperationException("Комментарий не должен превышать 500 символов.");
    }

    private static void ValidateWarehouses(Movement movement)
    {
        if (movement.Type == MovementTypes.In)
        {
            if (movement.FromWarehouseId is not null || movement.ToWarehouseId is null)
                throw new InvalidOperationException("Для прихода нужно указать только склад-получатель.");
            return;
        }

        if (movement.Type == MovementTypes.Out)
        {
            if (movement.FromWarehouseId is null || movement.ToWarehouseId is not null)
                throw new InvalidOperationException("Для расхода нужно указать только склад-отправитель.");
            return;
        }

        if (movement.Type == MovementTypes.Transfer)
        {
            if (movement.FromWarehouseId is null || movement.ToWarehouseId is null)
                throw new InvalidOperationException("Для перемещения нужно указать оба склада.");

            if (movement.FromWarehouseId == movement.ToWarehouseId)
                throw new InvalidOperationException("Склады отправления и получения должны различаться.");
            return;
        }

        throw new InvalidOperationException("Неизвестный тип движения.");
    }

    private static void ValidateLines(List<MovementItem> lines)
    {
        HashSet<int> itemIds = new();

        foreach (MovementItem line in lines)
        {
            if (line.ItemId <= 0)
                throw new InvalidOperationException("В строке документа не указан товар.");

            if (line.Quantity <= 0)
                throw new InvalidOperationException("Количество товара должно быть больше нуля.");

            if (!itemIds.Add(line.ItemId))
                throw new InvalidOperationException("Один и тот же товар указан в документе дважды.");
        }
    }

    private static string? NormalizeType(string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToUpperInvariant();
    }
}
