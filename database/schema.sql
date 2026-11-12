-- Междисциплинарная проектная работа № 2.
-- Название системы: EasyKeep (умный складской учёт).
-- Автор: [ФИО, группа].
--
-- Назначение: создание схемы базы данных EasyKeep из 6 связанных таблиц.
-- СУБД: Microsoft SQL Server 2019+.
--
-- Порядок выполнения:
--   1. Открыть скрипт в SSMS и подключиться к нужному экземпляру SQL Server.
--   2. Выполнить скрипт целиком (F5), затем выполнить database/seed.sql.
--
-- Повторный запуск безопасен: существующая база и таблицы не удаляются,
-- создаются только отсутствующие объекты. Скрипт не содержит секретов
-- и абсолютных путей, поэтому его можно запускать на другом компьютере.

SET NOCOUNT ON;
GO

-- Создаём базу только при её отсутствии (данные не удаляются).
IF DB_ID(N'EasyKeep') IS NULL
BEGIN
    CREATE DATABASE EasyKeep;
END;
GO

USE EasyKeep;
GO

-- 1. Категории товаров.
IF OBJECT_ID(N'dbo.categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.categories
    (
        id   INT           IDENTITY(1, 1) NOT NULL,
        name NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_categories PRIMARY KEY (id),
        CONSTRAINT UQ_categories_name UNIQUE (name)
    );
END;
GO

-- 2. Номенклатура (товары) с артикулом и неснижаемым остатком.
IF OBJECT_ID(N'dbo.items', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.items
    (
        id          INT            IDENTITY(1, 1) NOT NULL,
        sku         NVARCHAR(50)   NOT NULL,
        name        NVARCHAR(200)  NOT NULL,
        category_id INT            NOT NULL,
        unit        NVARCHAR(20)   NOT NULL,
        price       DECIMAL(18, 2) NOT NULL CONSTRAINT DF_items_price DEFAULT 0,
        min_stock   INT            NOT NULL CONSTRAINT DF_items_min_stock DEFAULT 0,
        CONSTRAINT PK_items PRIMARY KEY (id),
        CONSTRAINT UQ_items_sku UNIQUE (sku),
        CONSTRAINT FK_items_categories FOREIGN KEY (category_id) REFERENCES dbo.categories (id),
        CONSTRAINT CK_items_price CHECK (price >= 0),
        CONSTRAINT CK_items_min_stock CHECK (min_stock >= 0)
    );

    -- Поиск и сортировка каталога по названию.
    CREATE INDEX IX_items_name ON dbo.items (name);
END;
GO

-- 3. Склады и торговые точки.
IF OBJECT_ID(N'dbo.warehouses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.warehouses
    (
        id      INT           IDENTITY(1, 1) NOT NULL,
        name    NVARCHAR(100) NOT NULL,
        address NVARCHAR(300) NULL,
        CONSTRAINT PK_warehouses PRIMARY KEY (id),
        CONSTRAINT UQ_warehouses_name UNIQUE (name)
    );
END;
GO

-- 4. Текущие остатки в разрезе «склад — товар».
-- Одна строка на пару склад/товар; отрицательный остаток запрещён на уровне БД.
IF OBJECT_ID(N'dbo.stocks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.stocks
    (
        id           INT IDENTITY(1, 1) NOT NULL,
        warehouse_id INT NOT NULL,
        item_id      INT NOT NULL,
        quantity     INT NOT NULL CONSTRAINT DF_stocks_quantity DEFAULT 0,
        CONSTRAINT PK_stocks PRIMARY KEY (id),
        CONSTRAINT UQ_stocks_warehouse_item UNIQUE (warehouse_id, item_id),
        CONSTRAINT FK_stocks_warehouses FOREIGN KEY (warehouse_id) REFERENCES dbo.warehouses (id),
        CONSTRAINT FK_stocks_items FOREIGN KEY (item_id) REFERENCES dbo.items (id),
        CONSTRAINT CK_stocks_quantity CHECK (quantity >= 0)
    );

    -- Ускоряет JOIN и расчёт остатков по товару (в «Светофоре» и «Рентгене»).
    CREATE INDEX IX_stocks_item_id ON dbo.stocks (item_id);
END;
GO

-- 5. Шапки документов движения: приход, расход, перемещение.
IF OBJECT_ID(N'dbo.movements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.movements
    (
        id                INT           IDENTITY(1, 1) NOT NULL,
        type              NVARCHAR(10)  NOT NULL,
        from_warehouse_id INT           NULL,
        to_warehouse_id   INT           NULL,
        created_at        DATETIME2(0)  NOT NULL CONSTRAINT DF_movements_created_at DEFAULT SYSDATETIME(),
        comment           NVARCHAR(500) NULL,
        CONSTRAINT PK_movements PRIMARY KEY (id),
        CONSTRAINT FK_movements_from_warehouse FOREIGN KEY (from_warehouse_id) REFERENCES dbo.warehouses (id),
        CONSTRAINT FK_movements_to_warehouse FOREIGN KEY (to_warehouse_id) REFERENCES dbo.warehouses (id),
        CONSTRAINT CK_movements_type CHECK (type IN (N'IN', N'OUT', N'TRANSFER')),

        -- Для каждого типа документа разрешены только осмысленные склады:
        -- IN — только получатель, OUT — только отправитель,
        -- TRANSFER — оба склада и они должны различаться.
        CONSTRAINT CK_movements_warehouses CHECK
        (
            (type = N'IN' AND from_warehouse_id IS NULL AND to_warehouse_id IS NOT NULL)
            OR (type = N'OUT' AND from_warehouse_id IS NOT NULL AND to_warehouse_id IS NULL)
            OR (type = N'TRANSFER' AND from_warehouse_id IS NOT NULL AND to_warehouse_id IS NOT NULL
                AND from_warehouse_id <> to_warehouse_id)
        )
    );

    -- Журнал операций и отчёты сортируются по дате.
    CREATE INDEX IX_movements_created_at ON dbo.movements (created_at DESC);
    CREATE INDEX IX_movements_type ON dbo.movements (type);
END;
GO

-- 6. Табличная часть документов движения.
-- Одна позиция товара в документе может встречаться только один раз.
IF OBJECT_ID(N'dbo.movement_items', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.movement_items
    (
        id          INT IDENTITY(1, 1) NOT NULL,
        movement_id INT NOT NULL,
        item_id     INT NOT NULL,
        quantity    INT NOT NULL,
        CONSTRAINT PK_movement_items PRIMARY KEY (id),
        CONSTRAINT UQ_movement_items_movement_item UNIQUE (movement_id, item_id),
        CONSTRAINT FK_movement_items_movements FOREIGN KEY (movement_id)
            REFERENCES dbo.movements (id) ON DELETE CASCADE,
        CONSTRAINT FK_movement_items_items FOREIGN KEY (item_id) REFERENCES dbo.items (id),
        CONSTRAINT CK_movement_items_quantity CHECK (quantity > 0)
    );

    -- История движений конкретного товара для панели «Рентген товара».
    CREATE INDEX IX_movement_items_item_id ON dbo.movement_items (item_id);
END;
GO

PRINT N'Схема EasyKeep создана (или уже существовала). Следующий шаг: database/seed.sql.';
GO
