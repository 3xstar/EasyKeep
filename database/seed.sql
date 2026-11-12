-- Междисциплинарная проектная работа № 2.
-- Название системы: EasyKeep (умный складской учёт).
-- Автор: [ФИО, группа].
--
-- Назначение: учебные данные для демонстрации всех функций EasyKeep.
-- Выполняется после database/schema.sql в базе EasyKeep.
--
-- Что содержит:
--   1. Справочники: 7 категорий, 19 товаров, 4 склада.
--   2. Движения за последние 30 дней: приход, расход, перемещения.
--   3. Остатки рассчитываются из движений, поэтому история и stocks согласованы.
--   4. Негативные/крайние сценарии: остаток ниже min_stock, нулевой остаток,
--      товар без расхода за 30 дней и товар с нулевым расходом (неликвид).
--
-- Повторный запуск безопасен: если данные уже загружены, скрипт ничего не меняет.

SET NOCOUNT ON;
GO

USE EasyKeep;
GO

IF EXISTS (SELECT 1 FROM dbo.items WHERE sku = N'COF-001')
BEGIN
    PRINT N'Тестовые данные EasyKeep уже загружены. Повторная вставка пропущена.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Категории.
    INSERT INTO dbo.categories (name)
    VALUES (N'Кофе'), (N'Чай'), (N'Молочные продукты'), (N'Сиропы'),
           (N'Упаковка'), (N'Выпечка'), (N'Расходники');

    -- 2. Товары. min_stock задаёт порог «Светофора дефицита».
    INSERT INTO dbo.items (sku, name, category_id, unit, price, min_stock)
    SELECT v.sku, v.name, c.id, v.unit, v.price, v.min_stock
    FROM (VALUES
        (N'COF-001', N'Кофе эспрессо, зерно 1 кг',       N'Кофе',              N'кг',   1200.00, 20),
        (N'COF-002', N'Кофе арабика, зерно 1 кг',        N'Кофе',              N'кг',   1500.00, 15),
        (N'COF-003', N'Кофе колумбия, зерно 1 кг',       N'Кофе',              N'кг',   1800.00, 10),
        (N'TEA-001', N'Чай чёрный, 500 г',               N'Чай',               N'шт.',   600.00, 10),
        (N'TEA-002', N'Чай зелёный, 500 г',              N'Чай',               N'шт.',   750.00, 10),
        (N'MILK-001', N'Молоко 3,2%, 1 л',               N'Молочные продукты', N'шт.',    85.00, 40),
        (N'MILK-002', N'Сливки 10%, 1 л',                N'Молочные продукты', N'шт.',   190.00, 20),
        (N'SYR-001', N'Сироп карамельный, 1 л',          N'Сиропы',            N'шт.',   450.00,  5),
        (N'SYR-002', N'Сироп ванильный, 1 л',            N'Сиропы',            N'шт.',   480.00,  5),
        (N'PACK-001', N'Стакан бумажный 300 мл',         N'Упаковка',          N'шт.',     6.50, 500),
        (N'PACK-002', N'Крышка для стакана',             N'Упаковка',          N'шт.',     1.20, 300),
        (N'PACK-003', N'Крафт-пакет',                    N'Упаковка',          N'шт.',     9.00, 100),
        (N'PACK-004', N'Салфетки бумажные',              N'Упаковка',          N'шт.',     0.80, 200),
        (N'BAKE-001', N'Круассан классический',          N'Выпечка',           N'шт.',    65.00, 50),
        (N'BAKE-002', N'Маффин шоколадный',              N'Выпечка',           N'шт.',    80.00, 20),
        (N'SUP-001', N'Салфетки влажные',                N'Расходники',        N'упак.', 120.00, 10),
        (N'SUP-002', N'Перчатки нитриловые',             N'Расходники',        N'упак.', 450.00,  5),
        (N'SUP-003', N'Моющее средство',                 N'Расходники',        N'шт.',   320.00,  3),
        (N'SUP-004', N'Маска одноразовая',               N'Расходники',        N'упак.',  15.00, 10)
    ) AS v (sku, name, category_name, unit, price, min_stock)
    JOIN dbo.categories c ON c.name = v.category_name;

    -- 3. Склады: центральный, торговая точка, кофейня и склад брака.
    INSERT INTO dbo.warehouses (name, address)
    VALUES
        (N'Основной склад',  N'ул. Складская, 1'),
        (N'Торговый зал',    N'ул. Центральная, 15'),
        (N'Кофейня на Арбате', N'ул. Арбат, 20'),
        (N'Склад брака',     N'ул. Складская, 1');

    DECLARE @main   INT = (SELECT id FROM dbo.warehouses WHERE name = N'Основной склад');
    DECLARE @shop   INT = (SELECT id FROM dbo.warehouses WHERE name = N'Торговый зал');
    DECLARE @coffee INT = (SELECT id FROM dbo.warehouses WHERE name = N'Кофейня на Арбате');
    DECLARE @defect INT = (SELECT id FROM dbo.warehouses WHERE name = N'Склад брака');
    DECLARE @today  DATE = CAST(GETDATE() AS DATE);
    DECLARE @movement_id INT;

    -- 4. Приход №1: стартовое наполнение основного склада 30 дней назад.
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'IN', NULL, @main, DATEADD(DAY, -30, @today), N'Поставка №1: стартовое наполнение');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 200), (N'COF-002', 120), (N'COF-003', 80),
        (N'TEA-001', 60), (N'TEA-002', 50),
        (N'MILK-001', 250), (N'MILK-002', 150),
        (N'SYR-001', 40), (N'SYR-002', 30),
        (N'PACK-001', 6000), (N'PACK-002', 3000), (N'PACK-003', 800), (N'PACK-004', 2000),
        (N'BAKE-001', 300), (N'BAKE-002', 250),
        (N'SUP-001', 100), (N'SUP-002', 50), (N'SUP-003', 40), (N'SUP-004', 100)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 5. Приход №2: частичное пополнение в середине периода.
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'IN', NULL, @main, DATEADD(DAY, -10, @today), N'Поставка №2: пополнение молочной продукции и выпечки');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'MILK-001', 100), (N'MILK-002', 50),
        (N'BAKE-001', 150), (N'BAKE-002', 120)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 6. Перемещения на торговый зал.
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @shop, DATEADD(DAY, -29, @today), N'Перемещение в торговый зал №1');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 40), (N'MILK-001', 40), (N'PACK-001', 500),
        (N'PACK-002', 500), (N'BAKE-001', 60), (N'BAKE-002', 40)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @shop, DATEADD(DAY, -22, @today), N'Перемещение в торговый зал №2');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 30), (N'MILK-001', 30), (N'SYR-001', 10),
        (N'PACK-001', 500), (N'BAKE-001', 50)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @shop, DATEADD(DAY, -15, @today), N'Перемещение в торговый зал №3');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 30), (N'MILK-001', 30), (N'TEA-001', 10),
        (N'PACK-001', 500), (N'PACK-002', 500), (N'PACK-003', 200)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @shop, DATEADD(DAY, -8, @today), N'Перемещение в торговый зал №4');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 20), (N'MILK-001', 25), (N'SYR-002', 10),
        (N'PACK-001', 500), (N'PACK-004', 400), (N'BAKE-002', 40)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @shop, DATEADD(DAY, -2, @today), N'Перемещение в торговый зал №5');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 20), (N'MILK-001', 20), (N'PACK-001', 400),
        (N'PACK-002', 400), (N'BAKE-001', 40)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 7. Перемещения в кофейню на Арбате.
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @coffee, DATEADD(DAY, -27, @today), N'Перемещение в кофейню №1');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 30), (N'MILK-002', 30), (N'SYR-001', 10),
        (N'PACK-001', 400), (N'PACK-003', 100)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @coffee, DATEADD(DAY, -20, @today), N'Перемещение в кофейню №2');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-003', 20), (N'MILK-002', 25), (N'SYR-002', 10),
        (N'PACK-001', 400), (N'PACK-002', 400)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @coffee, DATEADD(DAY, -13, @today), N'Перемещение в кофейню №3');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 25), (N'MILK-002', 20), (N'PACK-001', 400), (N'BAKE-002', 30)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @coffee, DATEADD(DAY, -6, @today), N'Перемещение в кофейню №4');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-003', 20), (N'MILK-002', 20), (N'SYR-001', 10),
        (N'PACK-001', 300), (N'PACK-004', 300)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @coffee, DATEADD(DAY, -1, @today), N'Перемещение в кофейню №5');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 20), (N'MILK-002', 15), (N'PACK-001', 300), (N'PACK-002', 300)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 8. Перемещение брака на отдельный склад (для демонстрации TRANSFER).
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'TRANSFER', @main, @defect, DATEADD(DAY, -5, @today), N'Перемещение брака на склад брака');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES (N'COF-003', 5), (N'PACK-001', 50)) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 9. Расход в торговом зале (продажи и списания).
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -25, @today), N'Продажи в торговом зале №1');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 15), (N'MILK-001', 12), (N'BAKE-001', 20),
        (N'PACK-001', 120), (N'PACK-002', 120)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -23, @today), N'Продажи в торговом зале №2');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 12), (N'MILK-001', 10), (N'BAKE-002', 15), (N'PACK-001', 100)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -19, @today), N'Продажи в торговом зале №3');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 14), (N'MILK-001', 12), (N'BAKE-001', 18), (N'PACK-001', 110)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -17, @today), N'Продажи в торговом зале №4');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 13), (N'MILK-001', 11), (N'BAKE-002', 12), (N'PACK-001', 105)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -12, @today), N'Продажи в торговом зале №5');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 15), (N'MILK-001', 13), (N'BAKE-001', 20), (N'PACK-001', 120)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -10, @today), N'Продажи в торговом зале №6');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 8), (N'MILK-001', 10), (N'BAKE-002', 10), (N'PACK-001', 95)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -7, @today), N'Продажи в торговом зале №7');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 12), (N'MILK-001', 12), (N'BAKE-001', 15), (N'PACK-001', 100)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -4, @today), N'Продажи в торговом зале №8');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 10), (N'MILK-001', 14), (N'BAKE-002', 12), (N'PACK-001', 130)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @shop, NULL, DATEADD(DAY, -1, @today), N'Продажи в торговом зале №9');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-001', 14), (N'MILK-001', 15), (N'BAKE-001', 18), (N'PACK-001', 140)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 10. Расход в кофейне.
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @coffee, NULL, DATEADD(DAY, -24, @today), N'Продажи в кофейне №1');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 12), (N'MILK-002', 10), (N'PACK-001', 100)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @coffee, NULL, DATEADD(DAY, -18, @today), N'Продажи в кофейне №2');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-003', 8), (N'MILK-002', 9), (N'PACK-001', 95), (N'BAKE-002', 8)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @coffee, NULL, DATEADD(DAY, -11, @today), N'Продажи в кофейне №3');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-002', 11), (N'MILK-002', 11), (N'PACK-001', 110)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @coffee, NULL, DATEADD(DAY, -3, @today), N'Продажи в кофейне №4');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES
        (N'COF-003', 9), (N'MILK-002', 12), (N'PACK-001', 120), (N'BAKE-002', 9)
    ) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 11. Списание расходников на основные нужды (демонстрация нулевого остатка).
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @main, NULL, DATEADD(DAY, -4, @today), N'Списание масок на хозяйственные нужды');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES (N'SUP-004', 100)) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 12. Утилизация брака (OUT со склада брака).
    INSERT INTO dbo.movements (type, from_warehouse_id, to_warehouse_id, created_at, comment)
    VALUES (N'OUT', @defect, NULL, DATEADD(DAY, -2, @today), N'Утилизация брака');

    SET @movement_id = SCOPE_IDENTITY();
    INSERT INTO dbo.movement_items (movement_id, item_id, quantity)
    SELECT @movement_id, i.id, v.quantity
    FROM (VALUES (N'COF-003', 5)) AS v (sku, quantity)
    JOIN dbo.items i ON i.sku = v.sku;

    -- 13. Рассчитываем остатки из движений: история и текущее состояние согласованы.
    ;WITH stock_deltas AS
    (
        SELECT m.to_warehouse_id AS warehouse_id, mi.item_id, mi.quantity AS delta
        FROM dbo.movements m
        JOIN dbo.movement_items mi ON mi.movement_id = m.id
        WHERE m.type IN (N'IN', N'TRANSFER')

        UNION ALL

        SELECT m.from_warehouse_id AS warehouse_id, mi.item_id, -mi.quantity AS delta
        FROM dbo.movements m
        JOIN dbo.movement_items mi ON mi.movement_id = m.id
        WHERE m.type IN (N'OUT', N'TRANSFER')
    )
    INSERT INTO dbo.stocks (warehouse_id, item_id, quantity)
    SELECT warehouse_id, item_id, SUM(delta)
    FROM stock_deltas
    GROUP BY warehouse_id, item_id;

    COMMIT TRANSACTION;

    PRINT N'Тестовые данные EasyKeep успешно загружены: 4 склада, 7 категорий, 19 товаров, 30 дней истории.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    PRINT N'Ошибка загрузки тестовых данных: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
