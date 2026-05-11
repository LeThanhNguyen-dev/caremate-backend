# MomCare API Flow va Endpoint Mapping

## Ket qua kiem tra run project
- Da thu `dotnet build MomCare.sln` va `dotnet restore`.
- Hien tai build **chua chay duoc** trong sandbox do loi quyen truy cap file: `C:\Users\ADMIN\AppData\Roaming\NuGet\NuGet.Config` (Access denied).
- Vi vay, phan danh gia ben duoi duoc tong hop tu source code controller/service contract, chua verify bang runtime response that te.

## Tong quan luong nghiep vu
1. `Auth`: dang ky/dang nhap, lay `accessToken` + `refreshToken`, goi `GET /api/Auth/me` de lay profile token.
2. `Customer booking flow`: tim dich vu -> tim nurse -> xem lich trong -> tao booking -> theo doi va huy booking -> thanh toan -> review.
3. `Nurse flow`: cap nhat profile + giay to -> khai bao dich vu cung cap -> tao slot ranh -> xu ly booking -> chat voi customer.
4. `Admin flow`: duyet nurse -> xem dashboard/bookings/disputes -> cap nhat trang thai dispute.
5. `Realtime flow`: client connect `hubs/notifications` va `hubs/chat` bang bearer token (query `access_token`).

## Endpoint list (gui/nhan)

### 1) Auth (`/api/Auth`)
- `POST /register`
  - Body: `RegisterDto`
  - Response: `200` token/profile object, `400` neu email/phone trung hoac role sai
- `POST /signup/customer`
  - Body: `RegisterDto` (server ep role customer)
  - Response: `200`, `400`
- `POST /signup/nurse`
  - Body: `RegisterNurseDto`
  - Response: `200`, `400`
- `POST /login/external`
  - Body: `ExternalLoginDto`
  - Response: `200`, `401`
- `POST /login`
  - Body: `LoginDto`
  - Response: `200` (co access/refresh token), `401`
- `POST /refresh-token`
  - Body: `RefreshTokenRequestDto`
  - Response: `200`, `401`
- `GET /me` (Authorize)
  - Response: `{ userId, fullName, email, role }`

### 2) Services catalog (`/api/services`)
- `GET /api/services` (public)
  - Query: `isActive?`, `search?`
  - Response: list service
- `GET /api/services/{id}` (public)
  - Response: service detail, `404`
- `POST /api/services` (Admin)
  - Body: `UpsertServiceDto`
  - Response: `201 Created`
- `PUT /api/services/{id}` (Admin)
  - Body: `UpsertServiceDto`
  - Response: `204`, `404`
- `DELETE /api/services/{id}` (Admin)
  - Response: `204`, `404`

### 3) Nurse discovery public (`/api/nurses`)
- `GET /api/nurses`
  - Query: `serviceId?, minPrice?, maxPrice?, startTime?, endTime?`
  - Response: list `NurseDiscoveryDto`
- `GET /api/nurses/{userId}`
  - Response: `NurseProfileDetailDto`, `404`
- `GET /api/nurses/{userId}/availability`
  - Query: `from?, to?`
  - Response: list slot trong
- `GET /api/nurses/{userId}/availability/service/{serviceId}`
  - Query: `from?, to?`
  - Response: list slot trong theo service
- `GET /api/nurses/{userId}/reviews`
  - Query: `page=1, pageSize=10`
  - Response: paged reviews
- `GET /api/nurses/{userId}/rating`
  - Response: `NurseRatingDto`

### 4) Nurse private (`/api/Nurse`, `/api/nurse/services`, `/api/availability`)
- `GET /api/Nurse/profile` (Nurse)
- `PUT /api/Nurse/profile` (Nurse)
  - Body: `UpdateNurseProfileDto`
- `POST /api/Nurse/documents` (Nurse, rate limit upload)
  - Form-data: `UploadDocumentDto`
- `PUT /api/Nurse/documents/{documentId}` (Nurse)
  - Form-data: `UploadDocumentDto`
- `DELETE /api/Nurse/documents/{documentId}` (Nurse)
- `GET /api/Nurse/documents/{documentId}/url` (Nurse)

- `POST /api/nurse/services` (Nurse)
  - Body: `CreateNurseServiceDto`
- `GET /api/nurse/services` (Nurse)
- `PUT /api/nurse/services/{serviceId}` (Nurse)
  - Body: `UpdateNurseServiceDto`
- `DELETE /api/nurse/services/{serviceId}` (Nurse)

- `GET /api/availability/my-slots` (Nurse)
  - Query: `from?, to?`
- `POST /api/availability/slots` (Nurse)
  - Body: `CreateAvailabilitySlotDto`
- `DELETE /api/availability/slots/{slotId}` (Nurse)

### 5) Bookings (`/api/bookings`)
- `POST /api/bookings` (Customer, rate limit booking)
  - Body: `CreateBookingDto`
  - Response: booking detail, `400`
- `GET /api/bookings/my/customer` (Customer)
- `GET /api/bookings/my/nurse` (Nurse)
- `GET /api/bookings/{id}` (Authorize)
  - Response: detail neu co quyen, `404`
- `PATCH /api/bookings/{id}/status` (Authorize)
  - Body: `UpdateBookingStatusDto`
  - Response: `204`, `400`
- `POST /api/bookings/{id}/cancel` (Customer/Admin)
  - Body: `CancelBookingDto`
  - Response: `200`, `400`

### 6) Payments (`/api/payments`)
- `PUT /api/payments/booking/{bookingId}` (Authorize)
  - Body: `UpdatePaymentStatusDto`
  - Response: payment, `400`

### 7) Reviews (`/api/reviews`)
- `POST /api/reviews` (Customer)
  - Body: `CreateReviewDto`
- `PUT /api/reviews/{id}` (Customer)
  - Body: `UpdateReviewDto`
- `DELETE /api/reviews/{id}` (Customer)
- `GET /api/reviews/nurse/{nurseUserId}` (public)
- `GET /api/reviews/nurse/{nurseUserId}/rating` (public)

### 8) Chat (`/api/chat`)
- `POST /api/chat/conversations/by-booking/{bookingId}` (Authorize)
- `GET /api/chat/conversations/{conversationId}/messages` (Authorize)
  - Query: `limit=50`, `lastMessageId?`
- `POST /api/chat/conversations/{conversationId}/messages` (Authorize)
  - Body: `SendChatMessageDto`

### 9) Notifications (`/api/notifications`)
- `GET /api/notifications/mine` (Authorize)
- `GET /api/notifications/mine/unread-count` (Authorize)
- `PATCH /api/notifications/{id}/read` (Authorize)
- `PATCH /api/notifications/read-all` (Authorize)

### 10) Disputes (`/api/disputes`)
- `POST /api/disputes` (Authorize)
  - Body: `CreateDisputeDto`
- `GET /api/disputes` (Authorize)
  - User thuong: dispute cua minh; Admin: tat ca
- `PATCH /api/disputes/{id}` (Admin)
  - Body: `UpdateDisputeStatusDto`

### 11) Admin (`/api/Admin`)
- `GET /api/Admin/nurses/pending`
- `GET /api/Admin/nurses/{id}/details`
- `POST /api/Admin/nurses/{id}/review`
  - Body: `ReviewNurseProfileDto`
- `GET /api/Admin/dashboard`
- `GET /api/Admin/bookings?status=`
- `GET /api/Admin/disputes?status=`

### 12) Realtime hubs
- `POST /hubs/notifications/negotiate?negotiateVersion=1`
- `POST /hubs/chat/negotiate?negotiateVersion=1`
- WebSocket endpoint:
  - `/hubs/notifications`
  - `/hubs/chat`

## Danh gia hop ly/chua hop ly
- Hop ly:
  - Phan quyen role kha ro rang theo nhom endpoint.
  - Co rate limiting cho upload va booking.
  - Luong booking -> payment -> review/dispute du thong.
- Can can nhac cai tien:
  - Naming khong dong nhat: co ca `/api/Nurse` va `/api/nurses` (khac chu hoa + so it/so nhieu).
  - `PUT /api/payments/booking/{bookingId}` dang mo cho moi user da login; nen xac dinh ro role duoc phep doi trang thai payment.
  - `PATCH /api/bookings/{id}/status` chua rat ro role tai controller level (dang rely service rule); nen bo sung authorize role minh bach hon.
  - Nhieu endpoint `BadRequest` tra message text, chua co error code schema thong nhat.

## Flow de gui/nhan cho team frontend
1. Dang ky/dang nhap qua `/api/Auth/*`, luu `accessToken` + `refreshToken`.
2. Public browse: `/api/services`, `/api/nurses`, `/api/nurses/{id}`.
3. Customer tao booking: chon slot -> `POST /api/bookings`.
4. Nurse xu ly booking: `PATCH /api/bookings/{id}/status`.
5. Payment cap nhat: `PUT /api/payments/booking/{bookingId}`.
6. Sau completed: customer `POST /api/reviews` hoac tao `POST /api/disputes` neu co van de.
7. Realtime: ket noi hubs de nhan thong bao/chat.

## Goi y chay full verification khi co quyen machine
- Build/run API local (`dotnet run --project src/MomCare.Api`).
- Chay smoke script: `scripts/smoke-test.ps1`.
- Xuat OpenAPI tu `/openapi/v1.json` (development) de cross-check endpoint runtime voi tai lieu nay.

## Cap nhat moi (da code)
- Auth: them POST /api/Auth/logout, PATCH /api/Auth/change-password, POST /api/Auth/forgot-password, POST /api/Auth/reset-password.
- Users: them GET /api/users/me/profile, PUT /api/users/me/profile.
- Notifications: them DELETE /api/notifications/{id}, DELETE /api/notifications.
- System: them GET /health.


## Cap nhat Certi Fix (2026-05-11)
- Da fix mapping type certi trong seed du lieu cu.
- Type document chuan hien tai:
  - `id_card_front`
  - `id_card_back`
  - `certificate`
- Da bo sung normalize du lieu legacy trong `MomCareSeedData` de tranh loi profile/documents.
