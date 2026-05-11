# MomCare API — Phan Tich Toan Dien: Thieu Gi & Can Bo Sung Gi

## Phan 1: Tong Quan Hien Tai

### Luong nghiep vu hien co
1. **Auth**: dang ky/dang nhap, lay `accessToken` + `refreshToken`, goi `GET /api/Auth/me` de lay profile.
2. **Customer booking flow**: tim dich vu -> tim nurse -> xem lich trong -> tao booking -> theo doi & huy -> thanh toan -> review.
3. **Nurse flow**: cap nhat profile + giay to -> khai bao dich vu cung cap -> tao slot ranh -> xu ly booking -> chat voi customer.
4. **Admin flow**: duyet nurse -> xem dashboard/bookings/disputes -> cap nhat trang thai dispute.
5. **Realtime flow**: client connect `hubs/notifications` va `hubs/chat` bang bearer token.

## Phan 2: Cac Tinh Nang Dang Thieu

### Thieu Nghiem Trong (Blocker)

#### 2.1 Authentication & Security
- Khong co `POST /api/Auth/logout` de revoke token phia server.
- Khong co `POST /api/Auth/forgot-password` / `POST /api/Auth/reset-password`.
- Khong co `POST /api/Auth/verify-email`.
- Khong co `PATCH /api/Auth/change-password`.
- Naming conflict: `/api/Nurse` (private) vs `/api/nurses` (public).

#### 2.2 Payment — Thieu Luong Thuc Te
- `PUT /api/payments/booking/{bookingId}` chi cap nhat trang thai; thieu:
  - `POST /api/payments/booking/{bookingId}/initiate`
  - `POST /api/payments/webhook`
  - `GET /api/payments/booking/{bookingId}`
- Chua co refund flow khi cancel booking da thanh toan.

#### 2.3 Profile Nguoi Dung
- Thieu customer profile endpoints (`GET/PUT /api/users/me/profile`).
- Thieu upload avatar cho customer va nurse.

#### 2.4 Quan Ly Dia Chi / Location
- Chua co address book (`POST/GET/DELETE /api/users/me/addresses`).
- Chua co filter nurse theo khoang cach.
- Chua ro nurse khai bao khu vuc phuc vu.

#### 2.5 Nurse Earnings / Wallet
- Thieu `GET /api/nurse/earnings`.
- Thieu `GET /api/nurse/earnings/history`.
- Thieu `POST /api/nurse/payouts/request`.
- Chua ro commission/fee structure.

### Thieu Quan Trong (Should Have)

#### 2.6 Booking
- Thieu `GET /api/bookings/{id}/timeline`.
- Thieu luong reschedule.
- Can state machine ro rang cho `PATCH /api/bookings/{id}/status`.

#### 2.7 Notifications
- Thieu `DELETE /api/notifications/{id}`.
- Thieu `DELETE /api/notifications`.
- Thieu notification preferences.

#### 2.8 Availability
- Thieu `PUT /api/availability/slots/{slotId}`.
- Thieu recurring slots.

#### 2.9 Admin
- Thieu user management.
- Thieu service statistics.
- Thieu `GET /api/Admin/payments`.
- Thieu broadcast announcements.

#### 2.10 Pricing & Packages
- Chua co combo package.
- Chua co voucher/coupon.
- Chua co loyalty points.

#### 2.11 Nurse Onboarding
- Chua ro state onboarding (`draft -> submitted -> under_review -> approved/rejected`).
- Thieu `GET /api/Nurse/onboarding/status`.
- Thieu certification expiry tracking.

#### 2.12 Scheduling Nang Cao
- Chua co buffer time giua booking.
- Chua co confirmation deadline.
- Chua co waitlist.

#### 2.13 Emergency / SOS
- Chua co SOS/emergency contact.
- Chua co incident report endpoint rieng.

### Thieu Nice-to-Have (Enhancement)
- Search tong hop, filter discovery nang cao, so sanh nurse.
- Favorites/bookmark nurse.
- Block time off, default working hours.
- Support tickets rieng.
- Invoice/receipt/tax invoice.
- Admin moderation, system config, audit log.
- Nurse stats, export report, retention metrics.
- SMS OTP, push token registration, email template management.

## Phan 3: Bo Sung De Tang Hieu Qua He Thong

### Hieu Qua Ky Thuat
- Them Redis cache cho endpoint public.
- Them background jobs (auto-cancel, reminder, cleanup token, payout).
- Chuyen sang cloud object storage/presigned upload flow.
- Them health checks + structured logging + metrics.
- Bo sung audit fields/soft delete cho entity quan trong.

### Hieu Qua Van Hanh
- Idempotency key cho booking/payment.
- Webhook retry + dead letter + signature verify.
- Rate limiting chi tiet hon (login/otp/search/review).
- Chuan hoa pagination response envelope.
- Chuan hoa error schema (`code`, `message`, `details`).
- API versioning (`/api/v1/...`).
- Graceful degradation cho SignalR/payment.

### Hieu Qua Kinh Doanh
- Smart recommendation/matching.
- Nurse incentive (completion rate, response time, badge).
- Dynamic pricing signal.
- Conversion funnel tracking.
- Referral system.

## Phan 4: Ma Tran Uu Tien Tong Hop

| # | Bo Sung | Impact | Effort | Uu Tien |
|---|---------|--------|--------|---------|
| 1 | Address management | Cao | Thap | P0 |
| 2 | Nurse earnings & payout | Cao | Trung binh | P0 |
| 3 | Auth: logout / forgot-password / verify-email | Cao | Thap | P0 |
| 4 | Payment: initiate + webhook + refund | Cao | Cao | P0 |
| 5 | Idempotency key cho booking/payment | Cao | Thap | P0 |
| 6 | Background job scheduler | Cao | Trung binh | P0 |
| 7 | File storage len cloud (S3/Blob) | Cao | Trung binh | P0 |
| 8 | Health check endpoint | Cao | Rat thap | P0 |
| 9 | Nurse onboarding status | Trung binh | Thap | P0 |
| 10 | Device token / Push notification | Cao | Trung binh | P0 |

## Phan 5: Top 3 Rui Ro Can Xu Ly Ngay

1. **Double Booking / Double Payment**
- Nguyen nhan: chua co idempotency key.
- Fix: them `Idempotency-Key` va luu cache 24h.

2. **Mat Event Thanh Toan**
- Nguyen nhan: chua co webhook retry/dead letter.
- Fix: queue-based webhook processing + retry.

3. **File Upload Mat Du Lieu Khi Scale**
- Nguyen nhan: neu dung local disk.
- Fix: object storage (S3/Azure Blob) + URL-based upload.

## Ke hoach trien khai de xuat (2 sprint)

### Sprint 1 (P0)
- Auth security: logout, forgot/reset password, change-password, verify-email.
- Payment core: initiate, webhook, query status, refund baseline.
- Reliability: idempotency key, health endpoints, logging correlation id.
- User core: customer profile + address CRUD.

### Sprint 2 (P1)
- Nurse payout/earnings API.
- Background jobs (auto-cancel, reminders, token cleanup).
- Error schema + pagination envelope + API versioning.
- Notifications preferences + delete endpoints.

---
Tai lieu nay duoc tong hop tu source code va danh gia nghiep vu hien tai. Se can cap nhat lai sau khi chay full integration test/runtime verification.

## Da implement ngay (ban demo)
- POST /api/Auth/logout (revoke refresh token).
- PATCH /api/Auth/change-password.
- POST /api/Auth/forgot-password + POST /api/Auth/reset-password (flow don gian de test local).
- GET/PUT /api/users/me/profile.
- DELETE /api/notifications/{id} + DELETE /api/notifications.
- GET /health.

## Certi Bug Fix (2026-05-11)

### Loi goc
- Du lieu documents cu trong seed dang dung type: `id_card`, `hospital_certificate`.
- Code hien tai chi chap nhan: `id_card_front`, `id_card_back`, `certificate`.
- Dan den FE/BE lech mapping khi xu ly certi.

### Da sua
- Chuan hoa seed ve type moi:
  - `id_card` -> `id_card_front`
  - `hospital_certificate` -> `certificate`
- Them ham normalize legacy type trong seed de tu dong map du lieu cu.
- Khi seed document, bo sung `UpdatedAt` de tranh gia tri default `0001-01-01` gay loi hien thi.

### File da sua
- `src/MomCare.Infrastructure/Data/MomCareSeedData.cs`

### Ket qua
- `dotnet build MomCare.sln --configfile NuGet.Config`: **Build succeeded (0 errors)**.
- Sau khi chay lai app voi seed, profile nurse/documents se tra ve type dung chuan moi.

### Luu y cho frontend
- Chi gui `type` theo 3 gia tri hop le:
  - `id_card_front`
  - `id_card_back`
  - `certificate`
- Neu gap `429 Too Many Requests` khi upload, doi do limiter upload (5 req / 10 phut).

## Certi cho y ta moi (Update 2026-05-11)
- API upload/replace certi da linh hoat hon: chap nhan ca input cu va moi.
- Mapping tu dong:
  - `id_card` -> `id_card_front`
  - `hospital_certificate` -> `certificate`
- Van uu tien frontend gui dung chuan:
  - `id_card_front`
  - `id_card_back`
  - `certificate`
- File sua: `src/MomCare.Infrastructure/Services/NurseService.cs`.
