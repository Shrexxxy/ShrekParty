# Деплой

Один контейнер приложения (бэкенд + собранный фронт) и Caddy с автоматическим HTTPS.

## Что нужно на сервере

- VPS с Docker и Docker Compose.
- Домен, A-запись которого указывает на IP сервера (до первого запуска — иначе Caddy не получит сертификат).
- Открытые порты 80 и 443.

## Первый запуск

```bash
git clone <репозиторий> shrek-party
cd shrek-party/deploy
cp .env.example .env
nano .env          # домен и учётки админа/ТВ
docker compose up -d --build
```

Проверка: `https://<домен>` открывает страницу регистрации, `docker compose logs -f app` — логи.

## Обновление

```bash
cd shrek-party
git pull
cd deploy
docker compose up -d --build
```

База и загруженные фото живут в томе `shrek-data` и переживают пересборку.

## Копия базы

```bash
docker compose cp app:/data ./backup-$(date +%F-%H%M)
```
