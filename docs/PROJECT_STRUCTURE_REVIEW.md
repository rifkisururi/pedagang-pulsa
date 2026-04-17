# Project Structure Review

Tanggal: 2026-04-04

## Ringkasan

Struktur project saat ini sudah memiliki fondasi yang baik karena solusi sudah dipisah menjadi:

- `PedagangPulsa.Api`
- `PedagangPulsa.Web`
- `PedagangPulsa.Application`
- `PedagangPulsa.Domain`
- `PedagangPulsa.Infrastructure`
- `PedagangPulsa.Tests`

Secara konsep ini sudah mendekati modular monolith dengan layering yang jelas. Namun implementasinya masih belum menjaga boundary antar layer secara konsisten, sehingga beban maintenance akan naik saat fitur bertambah.

## Kekuatan Saat Ini

- Pemisahan project utama sudah jelas antara API, Web, Domain, Application, Infrastructure, dan Tests.
- Domain entity dan enum sudah dipisah ke project sendiri.
- Infrastruktur database, migration, dan supplier adapter sudah dipusatkan di project `Infrastructure`.
- MVC admin dan HTTP API sudah dipisah ke host yang berbeda.
- Test project sudah tersedia dan sudah mulai meng-cover service dan controller.

## Masalah Utama

### 1. Boundary antar layer masih bocor

Masalah terbesar ada di arah dependency.

- `PedagangPulsa.Application` masih mereferensikan `PedagangPulsa.Infrastructure`.
- Service di `Application` menggunakan `AppDbContext` secara langsung.
- Service di `Application` juga mengenal detail supplier adapter dari infrastructure.

Akibatnya:

- `Application` tidak benar-benar independen.
- Sulit mengganti persistence atau gateway implementation.
- Unit test jadi lebih berat karena logic terlalu dekat ke EF Core dan infrastructure.

###+ Contoh

- `AuthService`, `TransactionService`, `UserService`, dan service lain masih inject `AppDbContext`.
- `TransactionService` masih bergantung pada `ISupplierAdapterFactory` dari infrastructure.

### 2. Controller memegang terlalu banyak tanggung jawab

Beberapa controller masih berisi:

- query database langsung
- mapping response
- business rule
- SQL mentah
- helper akses database

Kondisi paling berat ada di `AuthController`, karena satu file mencampur:

- HTTP endpoint
- business flow auth
- SQL command manual
- token generation
- kontrak Redis
- implementasi Redis

Akibatnya:

- file menjadi sulit dibaca dan diubah
- testing menjadi rapuh
- reuse logic antar API dan Web menjadi rendah

### 3. Duplikasi composition root

`Program.cs` di `PedagangPulsa.Api` dan `PedagangPulsa.Web` sama-sama mengerjakan hal-hal berikut:

- register `DbContext`
- register service application
- register supplier adapter
- migrate database
- seed data

Akibatnya:

- setup infrastructure tersebar
- resiko drift antar host meningkat
- perubahan dependency injection harus diulang di beberapa tempat

### 4. Struktur Web belum konsisten

`PedagangPulsa.Web` sudah punya folder `Areas/Admin/ViewModels`, tetapi controller admin masih disimpan di root `Controllers`.

Ini menunjukkan dua pendekatan bercampur:

- ingin memakai `Area`
- tetapi routing dan folder controller belum benar-benar mengikuti area

Akibatnya:

- struktur sulit dipahami developer baru
- trace antara halaman admin, controller, dan view model kurang rapi

### 5. Testing belum terisolasi dengan aman

Saat ini helper test memakai database PostgreSQL nyata dengan connection string hardcoded, lalu melakukan cleanup via `TRUNCATE`.

Resikonya:

- test tidak benar-benar reproducible
- test bisa merusak data environment lain
- CI/CD nanti menjadi lebih rumit dan berbahaya

### 6. Root repository terlalu ramai

Di root repo saat ini bercampur:

- dokumentasi
- file SQL
- notes sprint
- helper tool
- file sementara
- artifact yang seharusnya tidak hidup lama

Contoh yang perlu dirapikan:

- dokumentasi dobel di root dan `docs/`
- tool `DropEnums` di root
- file `nul`
- upload runtime di dalam repo

### 7. Ada indikasi drift antara domain dan persistence

Beberapa property referral masih ada di entity `User`, tetapi di-ignore pada mapping EF Core.

Ini tanda bahwa:

- model domain belum sinkron dengan database model
- ada keputusan desain yang belum diselesaikan sampai tuntas

## Saran Perbaikan

### Prioritas 1: rapikan arah dependency

Target dependency:

- `Domain` tidak bergantung ke project lain
- `Application` hanya bergantung ke `Domain`
- `Infrastructure` bergantung ke `Application` dan `Domain`
- `Api` dan `Web` bergantung ke `Application` dan `Infrastructure`

Langkah:

- pindahkan kontrak seperti `IAppDbContext`, `IAuthTokenService`, `ICacheService`, `ISupplierGateway` ke `Application/Abstractions`
- implementasinya tetap di `Infrastructure`
- ubah service application agar bergantung ke interface, bukan concrete class infrastructure

### Prioritas 2: ubah struktur dari service-per-layer ke feature-per-slice

Struktur saat ini masih dominan berdasarkan teknis (`Services`, `Controllers`, `DTOs`). Untuk jangka menengah, akan lebih sehat jika diubah per fitur.

Contoh target:

```text
PedagangPulsa.Application/
  Abstractions/
  Features/
    Auth/
    Products/
    Transactions/
    Topups/
    Transfers/
    Referrals/
```

Di dalam tiap fitur bisa disimpan:

- command/query
- validator
- handler/service
- DTO internal

### Prioritas 3: pecah `AuthController`

`AuthController` cocok dijadikan pilot refactor karena paling jelas masalahnya.

Target:

- controller hanya terima request dan return response
- logic auth pindah ke application layer
- akses Redis pindah ke infrastructure service
- token generation dipisah ke service sendiri
- SQL mentah dibungkus dalam repository atau query service

### Prioritas 4: rapikan `Program.cs`

Buat extension method supaya registration lebih terpusat.

Contoh:

- `builder.Services.AddApplicationServices()`
- `builder.Services.AddInfrastructure(builder.Configuration)`
- `app.ApplyDatabaseMigrationsAsync()`

Manfaat:

- host `Api` dan `Web` lebih ringkas
- perubahan wiring lebih mudah dikontrol

### Prioritas 5: konsistenkan admin area

Pilih salah satu:

1. Gunakan `Areas/Admin` secara penuh
2. Hapus konsep area dan tetap pakai root MVC biasa

Rekomendasi: gunakan `Areas/Admin` secara penuh jika admin panel akan terus berkembang.

Target:

```text
PedagangPulsa.Web/
  Areas/
    Admin/
      Controllers/
      Views/
      ViewModels/
```

### Prioritas 6: pisahkan unit test dan integration test

Target:

```text
tests/
  PedagangPulsa.UnitTests/
  PedagangPulsa.IntegrationTests/
```

Rekomendasi:

- unit test jangan tergantung database nyata
- integration test gunakan database ephemeral
- jika memungkinkan gunakan Testcontainers untuk PostgreSQL
- simpan connection string test di environment variable, bukan hardcoded di source code

### Prioritas 7: rapikan struktur repo

Usulan root yang lebih bersih:

```text
src/
  PedagangPulsa.Api/
  PedagangPulsa.Web/
  PedagangPulsa.Application/
  PedagangPulsa.Domain/
  PedagangPulsa.Infrastructure/

tests/
  PedagangPulsa.UnitTests/
  PedagangPulsa.IntegrationTests/

docs/
database/
tools/
scripts/
```

Catatan:

- pindahkan file SQL ke `database/`
- pindahkan tool seperti `DropEnums` ke `tools/`
- jadikan `docs/` sebagai single source untuk dokumentasi
- jangan commit file upload runtime ke repo

## Rencana Refactor Bertahap

Supaya aman, refactor sebaiknya dilakukan bertahap.

### Tahap 1

- rapikan root repo
- hapus file sementara yang tidak perlu
- pindahkan dokumentasi dan SQL ke folder yang tepat
- putuskan strategi admin area

### Tahap 2

- buat abstraction di application
- pindahkan dependency infrastructure ke interface
- buat extension method untuk DI dan startup

### Tahap 3

- refactor fitur `Auth` end-to-end sebagai pilot
- kurangi logic di controller
- pisahkan token, Redis, dan persistence concern

### Tahap 4

- lanjutkan refactor ke fitur `Product`, `Transaction`, `Topup`, `Transfer`
- ubah struktur service menjadi feature-based

### Tahap 5

- pecah test project
- migrasikan test ke environment yang terisolasi
- tambahkan integration test untuk flow penting

## Quick Wins

Beberapa perbaikan cepat yang dampaknya tinggi:

- Buat `AddApplicationServices()` dan `AddInfrastructure()` untuk mengurangi duplikasi `Program.cs`.
- Pindahkan `IRedisService` dan implementasinya keluar dari `AuthController`.
- Hentikan penggunaan connection string database publik di source code test.
- Satukan dokumentasi ke folder `docs/`.
- Putuskan satu pola organisasi untuk admin panel.

## Kesimpulan

Struktur project ini tidak buruk. Fondasinya sudah cukup baik untuk dikembangkan. Masalah utamanya bukan pada jumlah project, tetapi pada boundary yang belum disiplin dan organisasi fitur yang belum konsisten.

Jika dirapikan sekarang, codebase ini bisa berkembang jauh lebih aman. Jika dibiarkan, setiap fitur baru akan menambah coupling dan memperlambat perubahan.

Rekomendasi utama:

1. Rapikan dependency arah layer.
2. Kurangi logic di controller.
3. Pindahkan ke struktur feature-based secara bertahap.
4. Isolasi testing dari database nyata.
5. Rapikan root repo dan dokumentasi.
