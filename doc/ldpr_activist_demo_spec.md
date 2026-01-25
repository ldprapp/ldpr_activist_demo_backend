# ldpr_activist_demo — спецификация демо-бэкенда (готово для разработки)

Данный документ фиксирует требования к демо-проекту **ldpr_activist_demo**: хранение пользователей (Identity/User Storage), справочники регионов/городов, задания и подтверждения выполнения заданий.  
В документе используется ограждение блоков кода **тройными тильдами** `~~~` (а не тройными обратными кавычками), чтобы не ломать Markdown при вложенных примерах и генерации. 

---

## 0) Технологии и допущения

- **C# + ASP.NET Web API**
- **EF Core (code-first)**
- **PostgreSQL** — основное хранилище данных (пользователи, справочники, задания, submissions).
- **Redis** — OTP-хранилище (одноразовые коды) с TTL (автоочистка истёкших кодов).
- **Docker** — запуск инфраструктуры и API.

### 0.1. Авторизация (демо-уровень)

- JWT/токены **не используются**.
- Для административных/чувствительных операций клиент передаёт:
  - `ActorUserId: Guid`
  - `ActorPassword: string`
- Сервер делает проверки:
  1) пользователь существует;
  2) сохранённый `PasswordHash` совпадает (после проверки через хэшер паролей);
  3) проверка прав: админ / создатель задания / доверенное лицо.

**Важно про пароль:** во всех запросах пароль передаётся **обычной строкой** (не хэш). Сервер сам вычисляет и проверяет хэш (в БД хранится только `PasswordHash`), а передача должна происходить по HTTPS/TLS.

> Это демо-логика. В проде будет заменено на нормальную аутентификацию/авторизацию.

---

## 1) Справочники (PostgreSQL)

### 1.1. Регионы РФ

Таблица: `regions`

Поля:
- `Id: int` — PK, identity
- `Name: text` — уникальное название субъекта РФ

Ограничения:
- `UNIQUE (Name)`

### 1.2. Города

Таблица: `cities`

Поля:
- `Id: int` — PK, identity
- `RegionId: int` — FK → `regions.Id`
- `Name: text` — название города

Ограничения:
- `FK (RegionId) -> regions(Id)`
- `UNIQUE (RegionId, Name)` — одинаковые имена городов допускаются в разных регионах

---

## 2) Пользователи (PostgreSQL)

### 2.1. Модель `users`

Таблица: `users`

Поля:
- `Id: uuid` — PK
- ФИО (3 отдельных поля):
  - `LastName: text` (not null)
  - `FirstName: text` (not null)
  - `MiddleName: text` (null)
- `Gender: text` или `int` (enum) — на усмотрение
- `PhoneNumber: text` (not null, unique)
- `PasswordHash: text` (not null) — пароль хранится **в хэшированном виде**
- `BirthDate: date` (not null)
- Локация пользователя:
  - `RegionId: int` (FK → `regions.Id`, not null)
  - `CityId: int` (FK → `cities.Id`, not null)
  - Инвариант: `cities.RegionId == users.RegionId` (валидировать при create/update)
- Флаги:
  - `IsAdmin: boolean` (not null)
  - `IsPhoneConfirmed: boolean` (not null)
- Баллы:
  - `Points: int` (not null, default 0)
  - Инвариант: `Points >= 0`

Ограничения/индексы:
- `UNIQUE (PhoneNumber)`
- `FK (RegionId) -> regions(Id)`
- `FK (CityId) -> cities(Id)`
- рекомендуется: `CHECK (Points >= 0)`

### 2.2. Значения при создании
- `IsAdmin = false`
- `IsPhoneConfirmed = false`
- `Points = 0`

### 2.3. Публичный профиль пользователя
Во всех публичных ответах пользователя **нельзя** возвращать:
- `PasswordHash`
- `IsAdmin`

---

## 3) OTP подтверждение телефона (Redis)

### 3.1. Назначение
OTP используется для подтверждения номера телефона при регистрации (и в будущем может быть использован при смене телефона).

### 3.2. Хранение в Redis
Ключ:
- `otp:phone:{phoneNumberNormalized}` → значение `{otpCode}`

TTL:
- настраиваемый, например 300 секунд (5 минут).

Рекомендуемые настройки:
- `Otp__TtlSeconds` (например `300`)
- `Otp__Length` (например `6`)

> Благодаря TTL Redis автоматически удаляет просроченные OTP.

---

## 4) Задания (PostgreSQL)

### 4.1. Модель `tasks`

Таблица: `tasks`

Поля:
- `Id: uuid` — PK
- `AuthorUserId: uuid` — GUID админа-создателя (FK → `users.Id`)
- Контент:
  - `Title: text` (not null)
  - `Description: text` (not null)
  - `RequirementsText: text` (not null)
  - `CoverImageUrl: text` (null) — URL/путь (для демо)
  - `ExecutionLocation: text` (null) — локация для выполнения (текст)
- Баллы:
  - `RewardPoints: int` (not null, `>= 0`)
- Даты:
  - `PublishedAt: timestamptz` (not null)
  - `DeadlineAt: timestamptz` (not null)
- Статус:
  - `Status: int` enum (not null), например:
    - `Open = 0`
    - `InProgress = 1`
    - `Closed = 2`
- Локация задания:
  - `RegionId: int` (FK → `regions.Id`, **обязателен**)
  - `CityId: int` (FK → `cities.Id`, **nullable**)
  - Инвариант: если `CityId != null`, то `cities.RegionId == tasks.RegionId`

Ограничения/индексы:
- `FK (AuthorUserId) -> users(Id)`
- `FK (RegionId) -> regions(Id)`
- `FK (CityId) -> cities(Id)`
- рекомендуется: `CHECK (RewardPoints >= 0)`
- рекомендуется индекс для ленты: `(RegionId, CityId, Status, DeadlineAt)`

### 4.2. Доверенные лица (админы)
Таблица связей: `task_trusted_admins`

Поля:
- `TaskId: uuid` — FK → `tasks.Id`
- `AdminUserId: uuid` — FK → `users.Id`

PK:
- `(TaskId, AdminUserId)`

Инвариант:
- `AdminUserId` должен ссылаться на пользователя с `IsAdmin = true` (валидировать в сервисе).

---

## 5) Подтверждение выполнения (PostgreSQL)

### 5.1. Модель `task_submissions`

Таблица: `task_submissions`

Поля:
- `Id: uuid` — PK
- `TaskId: uuid` — FK → `tasks.Id` (not null)
- `UserId: uuid` — FK → `users.Id` (not null)
- `SubmittedAt: timestamptz` (not null)
- Подтверждение:
  - `ConfirmedByAdminId: uuid` — FK → `users.Id` (null)
  - `ConfirmedAt: timestamptz` (null)
- Опциональные материалы:
  - `PhotosJson: jsonb` (null) — массив строк (URL/пути)
  - `ProofText: text` (null)

Ограничения/индексы:
- `UNIQUE (TaskId, UserId)` — одна submission на пару (task, user)

---

## 6) Правила доступа (демо)

### 6.1. Проверка “актор — админ”
Выполняется по `ActorUserId + ActorPassword`:
1) найти пользователя `users.Id == ActorUserId`
2) проверить пароль: вычислить хэш/верифицировать через хэшер и сравнить с сохранённым `PasswordHash`
3) `IsAdmin == true`

### 6.2. Проверка “создатель или доверенное лицо”
Для `taskId`:
- `tasks.AuthorUserId == ActorUserId` **или**
- существует запись в `task_trusted_admins (TaskId, ActorUserId)`

И актор должен быть админом.

---

## 7) API — общие соглашения

Базовый префикс: `/api/v1`  
Формат: JSON

Коды ответов (рекомендуемые):
- `200 OK`, `201 Created`, `204 No Content`
- `400 Bad Request`
- `401 Unauthorized` — неверные учётные данные (пароль)
- `403 Forbidden` — нет прав
- `404 Not Found`
- `409 Conflict` — конфликт уникальности (например телефон)

---

## 8) API — справочники

### 8.1. Получить все регионы
- `GET /api/v1/regions`

Ответ `200`:
~~~json
[
  { "id": 1, "name": "Воронежская область" }
]
~~~

### 8.2. Получить все города конкретного региона
- `GET /api/v1/regions/{regionId}/cities`

Ответ `200`:
~~~json
[
  { "id": 11, "name": "Нововоронеж" },
  { "id": 12, "name": "Воронеж" }
]
~~~

---

### 8.3. Добавить регион (только админ)

`POST /api/v1/regions`

Создаёт новый регион. Доступно только администратору (см. **0.1**).

**Request Body:**

~~~json
{
  "actorUserId": "00000000-0000-0000-0000-000000000000",
  "actorPassword": "plain_password",
  "name": "Воронежская область"
}
~~~

**201 Created** + `Location: /api/v1/regions/{id}`

~~~json
{
  "id": 123
}
~~~

**Ошибки:**

* `400 Bad Request` — некорректное имя (`name` пустое/пробелы).
* `401 Unauthorized` — неверные учётные данные или пользователь не админ.
* `409 Conflict` — регион с таким названием уже существует.

---

### 8.4. Добавить город в регион (только админ)

`POST /api/v1/regions/{regionId}/cities`

Создаёт новый город в указанном регионе. Доступно только администратору (см. **0.1**).

**Path Params:**

* `regionId: int` — идентификатор региона.

**Request Body:**

~~~json
{
  "actorUserId": "00000000-0000-0000-0000-000000000000",
  "actorPassword": "plain_password",
  "name": "Нововоронеж"
}
~~~

**201 Created** + `Location: /api/v1/regions/{regionId}/cities/{id}`

~~~json
{
  "id": 456
}
~~~

**Ошибки:**

* `400 Bad Request` — некорректное имя (`name` пустое/пробелы).
* `401 Unauthorized` — неверные учётные данные или пользователь не админ.
* `404 Not Found` — регион не найден.
* `409 Conflict` — город с таким названием уже существует в регионе.

## 9) API — пользователи

### 9.1. Регистрация
- `POST /api/v1/users/register`

Запрос:
~~~json
{
  "lastName": "Иванов",
  "firstName": "Иван",
  "middleName": "Иванович",
  "gender": "male",
  "phoneNumber": "+79990001122",
  "password": "plain_password",
  "birthDate": "2000-01-01",
  "regionId": 1,
  "cityId": 11
}
~~~

Действие:
- валидировать `regionId`, `cityId` и принадлежность города региону
- создать пользователя с:
  - `isAdmin=false`
  - `isPhoneConfirmed=false`
  - `points=0`
- сгенерировать OTP, записать в Redis с TTL, отправить OTP

Ответ `201`:
~~~json
{ "id": "00000000-0000-0000-0000-000000000000" }
~~~

### 9.2. Подтверждение телефона (OTP)
- `POST /api/v1/users/confirm-phone`

Запрос:
~~~json
{ "phoneNumber": "+79990001122", "otpCode": "123456" }
~~~

Действие:
- взять из Redis `otp:phone:{normalizedPhone}`
- если совпало → `IsPhoneConfirmed=true`, удалить ключ

Ответ `200`:
~~~json
{ "isPhoneConfirmed": true }
~~~

### 9.3. Логин (проверка телефона+пароля)
- `POST /api/v1/users/login`

Запрос:
~~~json
{ "phoneNumber": "+79990001122", "password": "plain_password" }
~~~

Ответ `200`:
~~~json
{ "ok": true }
~~~
Если телефона нет или пароль не совпал:
~~~json
{ "ok": false }
~~~

### 9.4. Получить пользователя по телефону (публично)
- `GET /api/v1/users/by-phone/{phoneNumber}`

Ответ `200` (без `password`, без `isAdmin`):
~~~json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "lastName": "Иванов",
  "firstName": "Иван",
  "middleName": "Иванович",
  "gender": "male",
  "phoneNumber": "+79990001122",
  "birthDate": "2000-01-01",
  "regionId": 1,
  "cityId": 11,
  "isPhoneConfirmed": true,
  "points": 25
}
~~~

### 9.5. Получить пользователя по id (публично)
- `GET /api/v1/users/{id}`
- Ответ как в 9.4

### 9.6. Изменить пароль
- `PUT /api/v1/users/{id}/password`

Запрос:
~~~json
{ "oldPassword": "OLD", "newPassword": "NEW" }
~~~

Ответ: `204`

### 9.7. Полное обновление пользователя (PUT)
- `PUT /api/v1/users/{id}`

Запрос (включая пароль владельца):
~~~json
{
  "password": "plain_password",
  "lastName": "Иванов",
  "firstName": "Иван",
  "middleName": "Иванович",
  "gender": "male",
  "phoneNumber": "+79990001122",
  "birthDate": "2000-01-01",
  "regionId": 1,
  "cityId": 11
}
~~~

Ответ: `204`

### 9.8. Изменить телефон (сброс подтверждения)
- `PUT /api/v1/users/{id}/phone`

Запрос:
~~~json
{ "password": "plain_password", "newPhoneNumber": "+79990001123" }
~~~

Действие:
- проверить пароль
- обновить телефон
- `IsPhoneConfirmed=false`

Ответ: `204`

### 9.9. Получить пользователей по региону (только ФИО)
- `GET /api/v1/users/by-region/{regionId}`

Ответ `200`:
~~~json
[
  { "lastName": "Иванов", "firstName": "Иван", "middleName": "Иванович" }
]
~~~

### 9.10. Получить пользователей по городу (только ФИО)
- `GET /api/v1/users/by-city/{cityId}`

Ответ `200`:
~~~json
[
  { "lastName": "Петров", "firstName": "Пётр", "middleName": null }
]
~~~

### 9.11. Узнать, является ли пользователь админом
- `GET /api/v1/users/{id}/is-admin`

Ответ `200`:
~~~json
{ "isAdmin": false }
~~~

### 9.12. Получить список GUID всех админов
- `GET /api/v1/users/admin-ids`

Ответ `200`:
~~~json
[
  "00000000-0000-0000-0000-000000000001",
  "00000000-0000-0000-0000-000000000002"
]
~~~

---

## 10) API — задания (админское управление)

### 10.1. Создать задание (любой админ)
- `POST /api/v1/tasks`

Запрос:
~~~json
{
  "actorUserId": "00000000-0000-0000-0000-000000000001",
  "actorPassword": "plain_password",

  "title": "Название",
  "description": "Описание",
  "requirementsText": "Требования",
  "rewardPoints": 100,
  "coverImageUrl": "https://example.com/cover.jpg",
  "executionLocation": "Локация (текст)",

  "publishedAt": "2026-01-24T10:00:00Z",
  "deadlineAt": "2026-02-01T10:00:00Z",
  "status": "Open",

  "regionId": 1,
  "cityId": 11,

  "trustedAdminIds": [
    "00000000-0000-0000-0000-000000000002"
  ]
}
~~~

Правила:
- актор должен быть админом (пароль совпадает)
- `regionId` существует
- если `cityId != null` → город существует и принадлежит `regionId`
- все `trustedAdminIds` существуют и являются админами

Ответ `201`:
~~~json
{ "id": "00000000-0000-0000-0000-00000000AAAA" }
~~~

### 10.2. Редактировать задание (PUT, только создатель)
- `PUT /api/v1/tasks/{taskId}`

Запрос: как создание.  
Права: `actorUserId == tasks.AuthorUserId` (и пароль совпадает).

Ответ: `204`

### 10.3. Удалить задание (только создатель)
- `DELETE /api/v1/tasks/{taskId}`

Запрос:
~~~json
{ "actorUserId": "00000000-0000-0000-0000-000000000001", "actorPassword": "plain_password" }
~~~

Ответ: `204`

### 10.4. Досрочно закрыть задание (только создатель)
- `POST /api/v1/tasks/{taskId}/close`

Запрос:
~~~json
{ "actorUserId": "00000000-0000-0000-0000-000000000001", "actorPassword": "plain_password" }
~~~

Действие: установить `Status = Closed`.  
Ответ: `204`

### 10.5. Получить полную модель задания (только админ)
- `GET /api/v1/tasks/{taskId}?actorUserId=...&actorPassword=...`

Ответ `200`:
~~~json
{
  "id": "00000000-0000-0000-0000-00000000AAAA",
  "authorUserId": "00000000-0000-0000-0000-000000000001",
  "title": "Название",
  "description": "Описание",
  "requirementsText": "Требования",
  "rewardPoints": 100,
  "coverImageUrl": "https://example.com/cover.jpg",
  "executionLocation": "Локация (текст)",
  "publishedAt": "2026-01-24T10:00:00Z",
  "deadlineAt": "2026-02-01T10:00:00Z",
  "status": "Open",
  "regionId": 1,
  "cityId": 11,
  "trustedAdminIds": [
    "00000000-0000-0000-0000-000000000002"
  ]
}
~~~

---

## 11) API — submissions (сдача/проверка)

### 11.1. Сдать задание (пользователь)
- `POST /api/v1/tasks/{taskId}/submit`

Запрос:
~~~json
{
  "userId": "00000000-0000-0000-0000-00000000BBBB",
  "password": "plain_password",
  "photos": [
    "https://example.com/p1.jpg",
    "https://example.com/p2.jpg"
  ],
  "proofText": "ФИО/ссылка/текст подтверждения"
}
~~~

Действие:
- проверить пользователя по `userId + password` (верифицировать пароль через хэшер)
- проверить, что задание существует и не закрыто
- создать или обновить submission (по `UNIQUE (taskId, userId)`)

Ответ: `200` или `201`

### 11.2. Список сдавших на проверку (создатель или доверенное лицо)
- `GET /api/v1/tasks/{taskId}/submitted?actorUserId=...&actorPassword=...`

Ответ `200`:
~~~json
[
  {
    "userId": "00000000-0000-0000-0000-00000000BBBB",
    "lastName": "Иванов",
    "firstName": "Иван",
    "middleName": "Иванович"
  }
]
~~~

### 11.3. Список подтверждённых (создатель или доверенное лицо)
- `GET /api/v1/tasks/{taskId}/approved?actorUserId=...&actorPassword=...`

Ответ `200`:
~~~json
[
  {
    "userId": "00000000-0000-0000-0000-00000000BBBB",
    "lastName": "Иванов",
    "firstName": "Иван",
    "middleName": "Иванович"
  }
]
~~~

### 11.4. Получить пользователя из submitted + submission
- `GET /api/v1/tasks/{taskId}/submitted/{userId}?actorUserId=...&actorPassword=...`

Ответ `200`:
~~~json
{
  "user": {
    "id": "00000000-0000-0000-0000-00000000BBBB",
    "lastName": "Иванов",
    "firstName": "Иван",
    "middleName": "Иванович",
    "gender": "male",
    "phoneNumber": "+79990001122",
    "birthDate": "2000-01-01",
    "regionId": 1,
    "cityId": 11,
    "isPhoneConfirmed": true,
    "points": 0
  },
  "submission": {
    "id": "00000000-0000-0000-0000-00000000CCCC",
    "taskId": "00000000-0000-0000-0000-00000000AAAA",
    "userId": "00000000-0000-0000-0000-00000000BBBB",
    "submittedAt": "2026-01-24T12:00:00Z",
    "confirmedByAdminId": null,
    "confirmedAt": null,
    "photos": [
      "https://example.com/p1.jpg"
    ],
    "proofText": "..."
  }
}
~~~

### 11.5. Подтвердить сдачу + начислить баллы (создатель или доверенное лицо)
- `POST /api/v1/tasks/{taskId}/approve`

Запрос:
~~~json
{
  "actorUserId": "00000000-0000-0000-0000-000000000001",
  "actorPassword": "plain_password",
  "userId": "00000000-0000-0000-0000-00000000BBBB"
}
~~~

Действие (атомарно, в транзакции):
1) проверить права (создатель или доверенное лицо) + что актор админ
2) найти submission (taskId, userId)
3) если уже подтверждено — не начислять повторно
4) записать `ConfirmedByAdminId = actorUserId`, `ConfirmedAt = now`
5) увеличить `users.Points += tasks.RewardPoints`

Ответ: `204` (или `200`)

---

## 12) API — лента заданий (по региону+городу)

### 12.1. Получить все задания для (regionId, cityId)
Правило фильтра:
- вернуть задачи, где `task.RegionId == regionId`
- и дополнительно:
  - `task.CityId == null` **или** `task.CityId == cityId`

- `GET /api/v1/tasks/feed?regionId={regionId}&cityId={cityId}`

Ответ `200` (карточки):
~~~json
[
  {
    "id": "00000000-0000-0000-0000-00000000AAAA",
    "title": "Название",
    "description": "Описание",
    "rewardPoints": 100,
    "coverImageUrl": "https://example.com/cover.jpg",
    "deadlineAt": "2026-02-01T10:00:00Z",
    "status": "Open",
    "regionId": 1,
    "cityId": null
  }
]
~~~

---

## 13) Конфигурация (рекомендуемые переменные)

- Postgres:
  - `ConnectionStrings__Postgres=...`
- Redis:
  - `ConnectionStrings__Redis=...`
- OTP:
  - `Otp__TtlSeconds=300`
  - `Otp__Length=6`

---

## 14) Минимальный набор сущностей/таблиц (итог)

### Таблицы Postgres
- `regions`
- `cities`
- `users`
- `tasks`
- `task_trusted_admins`
- `task_submissions`

### Redis ключи
- `otp:phone:{phoneNumberNormalized}` → `{otpCode}` (TTL)

