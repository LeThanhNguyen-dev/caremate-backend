# Full Project Health Check (2026-05-13)

## 1) Scope da kiem tra
- Restore toan bo solution: `dotnet restore MomCare.sln`
- Build Release: `dotnet build MomCare.sln -c Release`
- Test suite: `dotnet test MomCare.sln -c Release --no-build`
- Runtime smoke test:
  - Chay API: `dotnet run --project src/MomCare.Api/MomCare.Api.csproj --no-build`
  - Chay script: `scripts/smoke-test.ps1`

## 2) Ket qua tong quan
- Build: PASS (`0 Warning`, `0 Error`)
- Test: PASS (khong co test project, lenh ket thuc ma khong sinh test result)
- Runtime smoke: PASS (dang nhap 3 role + endpoint chinh + negotiate SignalR deu thanh cong)

## 3) Bat cap / thieu dang thay

### 3.1 Chua co bo test tu dong
- Hien tai solution khong co test project (`*.Tests.csproj`).
- Rủi ro: regression chi phat hien khi chay tay/smoke.

De xuat:
- Bo sung it nhat:
  - Unit tests cho service quan trong (`AuthService`, `BookingService`, `PaymentService`).
  - Integration tests cho API flow chinh (`Auth`, `Bookings`, `Notifications`).

### 3.2 Smoke script phu thuoc seed account co san
- `scripts/smoke-test.ps1` dang hard-code email:
  - `lan.customer@momcare.local`
  - `huong.nurse@momcare.local`
  - `admin@momcare.local`
- Rủi ro: doi du lieu seed hoac moi truong moi se fail du endpoint van dung.

De xuat:
- Them mode setup/seed rieng trong script (hoac param account).
- Cho phep truyen env var/param thay vi hard-code.

### 3.3 Log runtime qua nhieu (EF query logs day dac)
- Khi chay API, log SQL va seed output rat dai.
- Rủi ro: kho debug issue quan trong trong production-like runs.

De xuat:
- Dieu chinh `Logging:LogLevel` theo environment:
  - Development: giu `Information`
  - Staging/Production: giam EF SQL xuong `Warning`

### 3.4 Chua co report test artifact ro rang
- `dotnet test` chay xong nhung khong co artifact (`trx`, coverage).
- Rủi ro: kho theo doi chat luong theo thoi gian/CI.

De xuat:
- Khi co test project, chuan hoa:
  - `dotnet test --logger \"trx\" --collect \"XPlat Code Coverage\"`
  - Upload artifact len CI.

## 4) Kiem tra endpoint smoke da pass
- `GET /api/Auth/me`
- `GET /api/notifications/mine/unread-count`
- `GET /api/services`
- `GET /api/bookings/my/customer`
- `GET /api/bookings/my/nurse`
- `GET /api/Admin/dashboard`
- `POST /hubs/notifications/negotiate?negotiateVersion=1`
- `POST /hubs/chat/negotiate?negotiateVersion=1`

## 5) Muc uu tien khuyen nghi
1. Tao test project + bo unit/integration test toi thieu cho flow auth/booking/payment.
2. Nang cap `smoke-test.ps1` thanh script co tham so account + seed tu dong.
3. Chuan hoa log level theo moi truong.
4. Them CI quality gate (build + test + smoke nhe + artifact).

## 6) Ket luan
- Dự an hien tai build/run on dinh va smoke flow chinh dang xanh.
- Diem thieu lon nhat la `test automation` va `tinh linh hoat cua smoke script`.
