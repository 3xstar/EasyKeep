# Развёртывание базы данных EasyKeep

## Параметры

| Параметр | Значение |
| --- | --- |
| Название базы | `EasyKeep` (схема `dbo`) |
| SQL Server | Microsoft SQL Server 2025 Express (LocalDB `17.0.4025.3`), проверено также на SQL Server 2019+ |
| Экземпляр по умолчанию | `(localdb)\MSSQLLocalDB` |
| Аутентификация | Windows (Integrated Security), SQL-логины не используются |
| Инструмент выполнения скриптов | SSMS 21+ или `sqlcmd` из состава SQL Server Client SDK |
| Кодировка скриптов | UTF-8 с BOM (корректная кириллица в SSMS и `sqlcmd`) |
| Обязательные скрипты | `database/schema.sql` → `database/seed.sql` |

## Порядок выполнения

1. Подключитесь к нужному экземпляру SQL Server в SSMS или `sqlcmd`. Для LocalDB:
   ```powershell
   sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C
   ```
   Для именованного экземпляра замените сервер, например `localhost\SQLEXPRESS`.
2. Выполните `database/schema.sql` целиком. Скрипт сам создаёт базу `EasyKeep`, если её ещё нет, переключается на неё (`USE EasyKeep`) и создаёт 6 таблиц, ключи, ограничения и индексы. Отдельно создавать базу не требуется. Права пользователя: `db_owner` на LocalDB либо `dbcreator` + `db_owner` на учебном экземпляре.
3. Выполните `database/seed.sql`. Скрипт должен выполняться в той же базе `EasyKeep` (внутри есть `USE EasyKeep`) и наполняет её учебными данными в одной транзакции.
4. Проверьте результат запросами из раздела «Проверка результата».
5. Настройте строку подключения приложения по разделу «Настройка подключения» и запустите его согласно корневому [README](../README.md).

Команды для выполнения обоих скриптов одной строкой:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -i database/schema.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -i database/seed.sql
```

Скрипты не содержат абсолютных путей и секретов, поэтому переносимы на другой учебный компьютер.

## Проверка результата

Выполните в базе `EasyKeep`:

```sql
SELECT
    (SELECT COUNT(*) FROM dbo.categories)     AS categories,      -- 7
    (SELECT COUNT(*) FROM dbo.items)          AS items,           -- 19
    (SELECT COUNT(*) FROM dbo.warehouses)     AS warehouses,      -- 4
    (SELECT COUNT(*) FROM dbo.movements)      AS movements,       -- 28
    (SELECT COUNT(*) FROM dbo.movement_items) AS movement_items,  -- 129
    (SELECT COUNT(*) FROM dbo.stocks)         AS stocks;          -- 43

SELECT SUM(quantity) AS total_quantity,
       SUM(s.quantity * i.price) AS total_value
FROM dbo.stocks s
JOIN dbo.items i ON i.id = s.item_id;
-- 11909 единиц и 642123.50 ₽
```

Дополнительные контрольные точки:

```sql
-- Остаток ниже минимального порога: 36 < 40 (строка светофора «критично»).
SELECT s.quantity, i.min_stock
FROM dbo.stocks s
JOIN dbo.warehouses w ON w.id = s.warehouse_id
JOIN dbo.items i ON i.id = s.item_id
WHERE w.name = N'Торговый зал' AND i.sku = N'MILK-001';

-- Документы по типам: 2 IN, 10 TRANSFER, 16 OUT.
SELECT type, COUNT(*) AS documents
FROM dbo.movements
GROUP BY type
ORDER BY type;
```

Если значения совпали, база развёрнута корректно. В приложении признак успешного подключения — заполненный раздел «Остатки» (4 склада и 43 строки остатков) и работающий отчёт с итогом 11 909 единиц.

## Повторный запуск

- `schema.sql` безопасен для повторного запуска: каждая таблица создаётся только при отсутствии (`IF OBJECT_ID(...) IS NULL`), существующая база и данные не удаляются.
- `seed.sql` также безопасен: перед загрузкой проверяется наличие товара `COF-001`; если данные уже загружены, выводится сообщение «Тестовые данные EasyKeep уже загружены» и вставка пропускается.
- Автоматического удаления базы или очистки таблиц нет. Чтобы вернуть **чистое учебное состояние**, удалите данные только в этой учебной базе в порядке, обратном внешним ключам, и выполните `seed.sql` заново:

  ```sql
  USE EasyKeep;
  DELETE FROM dbo.movement_items;
  DELETE FROM dbo.movements;
  DELETE FROM dbo.stocks;
  DELETE FROM dbo.items;
  DELETE FROM dbo.categories;
  DELETE FROM dbo.warehouses;
  DBCC CHECKIDENT ('dbo.movement_items', RESEED, 0);
  DBCC CHECKIDENT ('dbo.movements', RESEED, 0);
  DBCC CHECKIDENT ('dbo.stocks', RESEED, 0);
  DBCC CHECKIDENT ('dbo.items', RESEED, 0);
  DBCC CHECKIDENT ('dbo.categories', RESEED, 0);
  DBCC CHECKIDENT ('dbo.warehouses', RESEED, 0);
  ```

  После этого снова выполните `database/seed.sql`. Операция допустима только для локальной учебной базы; на рабочих данных её выполнять нельзя.

## Тестовые данные

`seed.sql` загружает вымышленные данные кофейни и распределяет их так, чтобы были воспроизводимы все функции и тест-кейсы из [docs/testing.md](../docs/testing.md):

| Набор | Состав и назначение |
| --- | --- |
| Категории (7) | Кофе, Чай, Молочные продукты, Сиропы, Упаковка, Выпечка, Расходники. Нужны для фильтра каталога и JOIN-запросов. |
| Товары (19) | `COF-001…SUP-004` с уникальными SKU, единицами измерения (`кг`, `л`, `шт.`, `упак.`), ценами и `min_stock`. Достаточно для CRUD, поиска, дубля артикула и светофора. |
| Склады (4) | «Основной склад», «Торговый зал», «Кофейня на Арбате», «Склад брака». Демонстрируют мультискладской учёт и перемещения. |
| Движения (28) | 2 прихода, 10 перемещений и 16 расходов за последние 30 дней. Формируют историю для прогноза, журнала и отчёта. |
| Строки движений (129) | Многопозиционные накладные; проверяют JOIN, агрегацию и топ товаров. |
| Остатки (43) | Рассчитываются из движений тем же скриптом, поэтому история и `stocks` согласованы; отрицательных остатков нет. |

Специально подготовленные крайние случаи:

- «Торговый зал» / `MILK-001` = **36** при `min_stock = 40` — красный статус по правилу BR-01/BR-04.
- «Торговый зал» / `COF-002` = **2** — критичный прогноз при ненулевом расходе.
- «Торговый зал» / `COF-001` = **25** — жёлтая зона «внимание».
- «Основной склад» / `SUP-004` = **0** — нулевой остаток для проверки списания.
- `TEA-001`, `TEA-002` — есть остаток, но нет расхода за 30 дней (неликвид, ⚪).
- «Склад брака» — отдельная точка для перемещения и утилизации брака.
- «Торговый зал» / `MILK-001` = 36 позволяет воспроизвести негативный тест: списание 50 единиц отклоняется с откатом, а 36 — проходит.

## Настройка подключения

Приложение читает строку подключения в таком порядке:

1. `src/EasyKeep/appsettings.Local.json` — локальный файл с реальными параметрами, исключён из Git через `.gitignore`.
2. `src/EasyKeep/appsettings.json` — общая конфигурация без секретов (по умолчанию строка пустая).
3. Если строка не найдена или пуста, `AppConfig` использует фоллбэк:
   `Server=(localdb)\MSSQLLocalDB;Database=EasyKeep;Integrated Security=True;TrustServerCertificate=True`.

Пример локального файла находится в `src/EasyKeep/appsettings.Local.example.json`:

```json
{
  "ConnectionStrings": {
    "EasyKeep": "Server=(localdb)\\MSSQLLocalDB;Database=EasyKeep;Integrated Security=True;TrustServerCertificate=True"
  }
}
```

На другом компьютере скопируйте пример в `appsettings.Local.json` и замените `Server` на свой экземпляр (например `localhost\\SQLEXPRESS`). Пароли в репозиторий не добавляются; при ошибке подключения приложение показывает общее сообщение без деталей строки подключения. Необходимые права: чтение и запись в базу `EasyKeep` (`db_datareader`, `db_datawriter`), для первого развёртывания — `db_owner`.
