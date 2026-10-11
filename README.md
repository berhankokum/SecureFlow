# SecureFlow

Organizasyonların ekiplerini, projelerini ve iş öğelerini yönetmesi için geliştirilmiş çok kiracılı (multi-tenant) bir SaaS projesi. ASP.NET Core 10 backend ve React + TypeScript frontend; Modular Monolith ve Clean Architecture yapısıyla geliştirilmiştir.

Proje; tenant izolasyonu, kaynak bazlı yetkilendirme, iş akışları, eşzamanlı güncellemeler ve gerçek zamanlı bildirimler gibi uygulama geliştirme konularına odaklanır.

**Durum:** Yerel ortamda çalıştırılabilir. Production Docker yapılandırması ve CI/GHCR yayınlama akışı hazırlanmıştır; herkese açık canlı demo ve canlı deployment henüz yoktur. CI tanımı, her commit'in başarılı olduğu veya uygulamanın canlıda çalıştığı anlamına gelmez. Son çalıştırmalar [GitHub Actions](https://github.com/berhankokum/SecureFlow/actions) üzerinden incelenebilir.

## Özellikler

- Kayıt, e-posta doğrulama, giriş/çıkış ve parola sıfırlama.
- Organizasyon oluşturma ve değiştirme; üyelik, davet, rol ve ekip yönetimi.
- Proje üyelikleri; görev, hata ve hikâye türlerinde iş öğeleri; atama, öncelik, son tarih ve etiketler.
- İş akışı durumları ve geçişleri, Kanban panosu, arama ve filtreleme.
- Yorumlar, kullanıcı mention'ları, aktivite geçmişi ve bildirim tercihleri.
- SignalR ile gerçek zamanlı güncellemeler; S3 uyumlu dosya ekleri.
- Organizasyon dashboard'u ve yetki kontrollü denetim kayıtları.
- Redis önbellekleme, arka plan işleme, Transactional Outbox ve optimistic concurrency kontrolleri.

## Mimari ve güvenlik

Backend tek bir uygulama olarak çalışır; iş alanları `src/Modules` altında ayrılır. Veriler EF Core ile ortak PostgreSQL veritabanında tutulur. Tenant'a ait kaynaklarda organizasyon bağlamı, aktif üyelik ve gerekli izinler sunucu tarafında doğrulanır; frontend kontrolleri yalnız kullanıcı deneyimini destekler.

Kimlik doğrulamada ASP.NET Core Identity ve cookie kullanılır. CSRF koruması, rate limiting, dosya doğrulama, güvenlik header'ları ve kaynak bazlı erişim kontrolleri bulunur. Production yapılandırmasında non-root container'lar, Nginx reverse proxy, açıkça tanımlanmış güvenilir proxy adresleri ve sertifikayla korunan kalıcı Data Protection anahtarları kullanılır.

Bu önlemler bağımsız güvenlik denetimi veya kusursuz güvenlik garantisi olarak değerlendirilmemelidir.

## Teknolojiler

| Alan | Kullanılan teknolojiler |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core, EF Core, Npgsql, Identity, FluentValidation |
| Frontend | React, TypeScript, Vite, React Router, TanStack Query, React Hook Form, Zod, Tailwind CSS |
| Veri ve iletişim | PostgreSQL, Redis, SignalR, S3 uyumlu object storage (yerelde RustFS) |
| İzlenebilirlik | Serilog, OpenTelemetry, health endpoint'leri |
| Test | xUnit, FluentAssertions, NSubstitute, Testcontainers, Vitest, React Testing Library, Playwright |
| Altyapı | Docker Compose, Nginx, Mailpit, GitHub Actions, GHCR |

## Yerel kurulum

### Gereksinimler

- .NET 10 SDK ve Node.js 24.
- Linux container çalıştırabilen Docker Engine veya Docker Desktop; Docker Compose V2.
- Bağımlılık ve imaj indirmeleri için internet erişimi.
- Yerelde boş `5080`, `5173`, `5432`, `6379`, `9000`, `9001`, `1025` ve `8025` portları.

### 1. Depoyu klonlayın ve örnek ayarları kopyalayın

```sh
git clone https://github.com/berhankokum/SecureFlow.git
cd SecureFlow
```

PowerShell:

```powershell
Copy-Item .env.example .env
```

macOS/Linux:

```sh
cp .env.example .env
```

Bu kopyalama adımı yalnız ilk kurulum içindir; mevcut `.env` dosyanızı ezmeyin. [`.env.example`](.env.example) içindeki parolalar yalnız yerel geliştirme için herkese açık örneklerdir. Gerçek servis anahtarı değildir ve production'da kullanılmamalıdır.

PostgreSQL parolasını değiştirirseniz `POSTGRES_PASSWORD` ile `ConnectionStrings__Postgres` içindeki `Password` alanını birlikte güncelleyin. RustFS bilgileri için `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` ile `ObjectStorage__AccessKey`/`ObjectStorage__SecretKey` eşleşmelidir. `MINIO_*` isimleri geçmiş ayarlarla uyumluluk içindir; mevcut servis RustFS'tir. `.env` yükleyicisi `${...}` referanslarını genişletmez; değerleri açıkça yazın.

### 2. Yerel bağımlılıkları başlatın

Repository kökünde:

```sh
docker compose up -d --build --wait
```

Bu komut PostgreSQL, Redis, RustFS ve Mailpit'i başlatır; API/frontend ayrı çalıştırılır. Servis portları `127.0.0.1` adresine bağlıdır. Mevcut PostgreSQL volume'undaki parola `.env` değiştiğinde otomatik değişmez; bağlantı hatasını çözmek için verilerinizi silmeyin.

### 3. Veritabanını hazırlayın ve API'yi başlatın

```sh
dotnet restore SecureFlow.slnx
dotnet tool install --global dotnet-ef --version 10.0.11
dotnet ef database update --project src/SecureFlow.Api --startup-project src/SecureFlow.Api --configuration Debug
dotnet run --project src/SecureFlow.Api --launch-profile http
```

`dotnet-ef` zaten kuruluysa kurulum adımını tekrar çalıştırmayın; sürümünün projedeki EF Core 10 ile uyumunu kontrol edin. Migration komutu yerel geliştirme veritabanına uygulanır. API başlangıcında otomatik migration çalıştırılmaz.

Debug build'leri veya Development ortamı, repository kökündeki `.env` dosyasından yalnız eksik ayarları tamamlar. Mevcut environment/CLI/appsettings değerlerini ezmez. Bu bölüm production kurulum yönergesi değildir.

### 4. Frontend'i ayrı terminalde başlatın

```sh
cd frontend
npm ci
npm run dev
```

Uygulama: **http://localhost:5173**. Vite, `/api`, `/health` ve `/hubs` isteklerini `http://localhost:5080` adresindeki API'ye yönlendirir.

### 5. İlk hesabı oluşturun

1. Uygulamadan kayıt olun.
2. **http://localhost:8025** adresindeki Mailpit ekranını açın.
3. Doğrulama e-postasındaki bağlantıyla hesabınızı doğrulayın ve giriş yapın.
4. Organizasyon ve proje oluşturup iş öğeleri ekleyin.

Yerel e-postalar gerçek gelen kutunuza gönderilmez; Mailpit'te tutulur. Hazır admin hesabı veya otomatik demo verisi yoktur.

## İncelemek için örnek akış

Organizasyon ve proje oluşturun; bir iş öğesine öncelik, etiket ve sorumlu atayın. Kanban üzerinden durumunu değiştirip yorum ekleyin. İkinci bir kullanıcıyı Mailpit'teki davet bağlantısıyla organizasyona ve ilgili projeye dahil ederek yetki farklarını ve gerçek zamanlı güncellemeleri ayrı tarayıcı oturumlarında inceleyebilirsiniz.

## Testler

### Backend

Docker çalışır durumda olmalıdır. Integration testleri ayrı geçici container'lar kullanır. Testlerin beklediği yerel PostgreSQL imajını hazırlayın; aşağıdaki build komutu development veritabanını başlatmaz veya değiştirmez:

```sh
docker build -t secureflow-postgres:17-patched-20260919 infra/postgres
dotnet test SecureFlow.slnx --configuration Release --filter "Category!=Browser"
```

### Frontend ve browser kontrolleri

`frontend` klasöründe:

```sh
npm ci
npm test
npm run lint
npm run build
npx playwright install chromium
npm run test:e2e
npm run test:security-build
```

Linux'ta gerekli browser sistem bağımlılıkları için `npx playwright install --with-deps chromium` kullanılabilir. `test:e2e` paketi mock API yanıtlarıyla frontend akışlarını, `test:security-build` ise build çıktısının CSP davranışını kontrol eder.

Gerçek HTTPS API, veritabanı ve cookie akışını test etmek için frontend build ve Chromium kurulumundan sonra repository kökünde:

```sh
dotnet test tests/SecureFlow.IntegrationTests/SecureFlow.IntegrationTests.csproj --configuration Release --filter "Category=Browser"
```

### Production container smoke testi

Docker ve frontend browser bağımlılıkları hazırken repository kökünde:

```sh
docker build -t secureflow-postgres:17-patched-20260919 infra/postgres
docker build -f infra/production/Api.Dockerfile -t secureflow-api:phase27 .
docker build -f infra/production/Frontend.Dockerfile -t secureflow-edge:phase27 .
dotnet run --project tools/production-smoke/ProductionSmoke.csproj --configuration Release -- --run
```

Araç kendi geçici Compose projesini, test sertifikalarını ve credentials'ını oluşturur. HTTPS, gerçek Nginx, SMTP→Mailpit, auth/CSRF, WebSocket ve container yeniden oluşturulduğunda Data Protection devamlılığını doğrular. Sonunda yalnız kendi test container/volume'larını temizler; repository `.env` dosyasını kullanmaz. Bu test canlı deployment değildir.

## CI ve production durumu

[CI workflow'u](.github/workflows/ci.yml) PR ve `master` push'larında backend/frontend testlerini, gerçek browser entegrasyonunu, secret taramasını, bağımlılık/container güvenlik kontrollerini ve production smoke testini çalıştırır.

GHCR yayını, tüm gerekli kontroller başarılıysa ve repository variable'ı `ENABLE_GHCR_PUBLISH=true` ise yalnız `master` push'unda etkinleşir. Test edilen API, edge ve yamalı PostgreSQL imajları yeniden build edilmeden aktarılır. Release çıktısı commit ve image digest'lerini içerir. PR'lara registry yazma yetkisi verilmez.

[Production Compose](compose.production.yml) development Compose'tan ayrıdır. Gerçek sunucu, domain/HTTPS, SMTP sağlayıcısı, private bucket/erişim politikaları, sınırlı yetkili DB kullanıcısı, migration, yedekleme/geri yükleme ve izleme kurulumu ayrıca gereklidir. Canlı sunucuya deployment job'ı henüz yoktur.

Production API, repository dışından `/run/secureflow/settings.json` ve ayrı Data Protection sertifikası bekler. Gerçek anahtarları yalnız dış secret dosyalarında tutun; [production örneğini](infra/production/settings.example.json) gerçek değerlerle doldurup commit etmeyin. Development `.env` dosyasını production'a taşımayın. Kalıcı veriler ve key-ring için `down -v` kullanmayın.

## Kaynak kod yapısı

```text
src/
  SecureFlow.Api/          Uygulama başlangıcı, persistence, security, realtime
  SecureFlow.SharedKernel/ Ortak domain temelleri
  SecureFlow.Contracts/    Modüller arası sözleşmeler
  Modules/                Identity, Organizations, Teams, Projects, WorkItems,
                          Collaboration, Notifications, Files, Audit, Outbox
frontend/                 React uygulaması ve browser testleri
tests/                    Unit, integration, architecture ve test-suite kontrolleri
infra/                    PostgreSQL ve production container yapılandırmaları
tools/                    İzole doğrulama araçları
.github/                  CI ve release script'leri
```

## Lisans

Bu repository'nin özgün kaynak kodu [MIT lisansı](LICENSE) ile paylaşılır. Kullanılan üçüncü taraf kütüphane ve container imajları kendi lisanslarına tabidir.
