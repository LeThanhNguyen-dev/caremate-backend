# MomCare API - Complete Endpoints Documentation

**Version:** 1.0  
**Last Updated:** March 18, 2026  
**Status:** ✅ Production Ready (MVP)  
**Build:** ✅ All 4 projects build successfully

---

## 🔧 Base Configuration

```
Base URL: http://localhost:5000 (or your API URL)
Content-Type: application/json
Authentication: Bearer {JWT_TOKEN}
```

---

## 🔐 Authentication Endpoints

### 1. Register New User
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!",
  "fullName": "John Doe",
  "phone": "+84901234567"
}

Response: 200 OK
{
  "userId": 1,
  "email": "user@example.com",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

### 2. Customer Sign Up
```http
POST /api/auth/signup/customer
Content-Type: application/json

{
  "email": "customer@example.com",
  "password": "Password123!",
  "fullName": "Jane Doe",
  "phone": "+84901234567",
  "address": "25 Nguyen Hue, District 1, HCMC"
}

Response: 200 OK
{
  "userId": 2,
  "email": "customer@example.com",
  "role": "Customer",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

### 3. Nurse Sign Up
```http
POST /api/auth/signup/nurse
Content-Type: application/json

{
  "email": "nurse@example.com",
  "password": "Password123!",
  "fullName": "Nurse Mary",
  "phone": "+84901234568",
  "licenseNumber": "NURSE-123456",
  "experience": 7,
  "specialization": "Postpartum care"
}

Response: 201 Created
{
  "userId": 3,
  "email": "nurse@example.com",
  "role": "NurseUnconfirmed",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

### 4. Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}

Response: 200 OK
{
  "userId": 1,
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "role": "Customer"
}
```

### 5. OAuth Login (Google/Facebook)
```http
POST /api/auth/login/external
Content-Type: application/json

{
  "provider": "google",  // or "facebook"
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjU1NzU2NTYz...",
  "email": "user@gmail.com",
  "displayName": "John Doe"
}

Response: 200 OK
{
  "userId": 4,
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "role": "Customer"
}
```

### 6. Refresh Token
```http
POST /api/auth/refresh-token
Content-Type: application/json

{
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}

Response: 200 OK
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

---

## 🏥 Service Catalog Endpoints

### 1. Get All Services
```http
GET /api/services?page=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
{
  "data": [
    {
      "id": 1,
      "name": "Postpartum Care",
      "description": "Comprehensive postpartum care for mothers",
      "basePrice": 500000,
      "duration": 120,
      "unit": "minutes",
      "status": "active"
    }
  ],
  "totalCount": 15,
  "pageNumber": 1,
  "pageSize": 10
}
```

### 2. Get Service Detail
```http
GET /api/services/1
Authorization: Bearer {token}

Response: 200 OK
{
  "id": 1,
  "name": "Postpartum Care",
  "description": "Comprehensive postpartum care for mothers",
  "basePrice": 500000,
  "duration": 120,
  "unit": "minutes",
  "status": "active",
  "createdAt": "2026-03-15T10:30:00Z"
}
```

### 3. Create Service (Admin Only)
```http
POST /api/services
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "name": "Baby Bathing Care",
  "description": "Professional baby bathing and care",
  "basePrice": 300000,
  "duration": 60,
  "unit": "minutes"
}

Response: 201 Created
{
  "id": 2,
  "name": "Baby Bathing Care",
  ...
}
```

### 4. Update Service (Admin Only)
```http
PUT /api/services/1
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "name": "Postpartum Care",
  "description": "Updated description",
  "basePrice": 550000,
  "duration": 120
}

Response: 200 OK
```

### 5. Delete Service (Admin Only)
```http
DELETE /api/services/1
Authorization: Bearer {adminToken}

Response: 204 No Content
```

---

## 👩‍⚕️ Nurse Management Endpoints

### 1. Get Nurse Profile
```http
GET /api/nurse/profile
Authorization: Bearer {nurseToken}

Response: 200 OK
{
  "userId": 3,
  "fullName": "Nurse Mary",
  "phone": "+84901234568",
  "email": "nurse@example.com",
  "bio": "7+ years in maternity care",
  "yearsExperience": 7,
  "specializations": "Postpartum, Newborn care",
  "certifications": "RN License, Neonatal Certificate",
  "maxConcurrentBookings": 3,
  "serviceAreaDescription": "HCMC, radius 20km",
  "isActive": true,
  "verificationStatus": "verified",  // unverified, pending_review, verified
  "averageRating": 4.8,
  "totalReviews": 24,
  "createdAt": "2026-03-10T14:00:00Z"
}
```

### 2. Update Nurse Profile
```http
PUT /api/nurse/profile
Authorization: Bearer {nurseToken}
Content-Type: application/json

{
  "bio": "8 years in maternity care",
  "yearsExperience": 8,
  "certifications": "RN License, BSN, Neonatal Certificate",
  "maxConcurrentBookings": 4,
  "serviceAreaDescription": "HCMC + suburbs, radius 25km"
}

Response: 200 OK
{
  "userId": 3,
  "fullName": "Nurse Mary",
  "bio": "8 years in maternity care",
  ...
}
```

### 3. Upload Nurse Document
```http
POST /api/nurse/documents
Authorization: Bearer {nurseToken}
Content-Type: application/json

{
  "documentType": "license",  // id_card, license, certificate, other
  "fileUrl": "https://storage.example.com/docs/license.pdf"
}

Response: 201 Created
{
  "id": 1,
  "nurseId": 3,
  "documentType": "license",
  "fileUrl": "https://...",
  "uploadedAt": "2026-03-18T10:00:00Z"
}
```

### 4. Get Nurse Documents
```http
GET /api/nurse/documents
Authorization: Bearer {nurseToken}

Response: 200 OK
{
  "documents": [
    {
      "id": 1,
      "documentType": "license",
      "fileUrl": "https://...",
      "uploadedAt": "2026-03-18T10:00:00Z"
    }
  ]
}
```

---

## 🔍 Nurse Discovery & Search

### 1. Search Nurses
```http
GET /api/nurses?serviceId=1&minPrice=400000&maxPrice=600000&page=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
{
  "data": [
    {
      "userId": 3,
      "fullName": "Nurse Mary",
      "phone": "+84901234568",
      "verificationStatus": "verified",
      "yearsExperience": 7,
      "serviceOfferings": [
        {
          "serviceId": 1,
          "serviceName": "Postpartum Care",
          "price": 500000,
          "unit": "fixed"  // fixed, hourly
        }
      ],
      "averageRating": 4.8,
      "totalReviews": 24,
      "availableSlots": 5
    }
  ],
  "totalCount": 12,
  "pageNumber": 1,
  "pageSize": 10
}
```

### 2. Get Nurse Profile Detail
```http
GET /api/nurses/3
Authorization: Bearer {token}

Response: 200 OK
{
  "userId": 3,
  "fullName": "Nurse Mary",
  "phone": "+84901234568",
  "email": "nurse@example.com",
  "bio": "7+ years in maternity care",
  "yearsExperience": 7,
  "verificationStatus": "verified",
  "services": [
    {
      "id": 1,
      "name": "Postpartum Care",
      "price": 500000,
      "unit": "fixed"
    }
  ],
  "averageRating": 4.8,
  "totalReviews": 24,
  "reviews": [
    {
      "id": 1,
      "rating": 5,
      "comment": "Excellent care!",
      "createdAt": "2026-03-17T10:00:00Z"
    }
  ]
}
```

### 3. Check Nurse Availability
```http
GET /api/nurses/3/availability?from=2026-03-20&to=2026-03-27
Authorization: Bearer {token}

Response: 200 OK
{
  "availabilitySlots": [
    {
      "id": 1,
      "startTime": "2026-03-20T09:00:00Z",
      "endTime": "2026-03-20T17:00:00Z",
      "isBooked": false
    },
    {
      "id": 2,
      "startTime": "2026-03-21T09:00:00Z",
      "endTime": "2026-03-21T17:00:00Z",
      "isBooked": true
    }
  ]
}
```

---

## 📅 Availability Management (Nurse)

### 1. Get My Availability Slots
```http
GET /api/availability/my-slots?from=2026-03-20&to=2026-03-27
Authorization: Bearer {nurseToken}

Response: 200 OK
{
  "slots": [
    {
      "id": 1,
      "startTime": "2026-03-20T09:00:00Z",
      "endTime": "2026-03-20T17:00:00Z",
      "isBooked": false,
      "bookingId": null
    }
  ]
}
```

### 2. Create Availability Slot
```http
POST /api/availability/slots
Authorization: Bearer {nurseToken}
Content-Type: application/json

{
  "startTime": "2026-03-22T08:00:00Z",
  "endTime": "2026-03-22T17:00:00Z"
}

Response: 201 Created
{
  "id": 5,
  "nurseProfileId": 1,
  "startTime": "2026-03-22T08:00:00Z",
  "endTime": "2026-03-22T17:00:00Z",
  "isBooked": false,
  "createdAt": "2026-03-18T10:00:00Z"
}
```

### 3. Delete Availability Slot
```http
DELETE /api/availability/slots/1
Authorization: Bearer {nurseToken}

Response: 204 No Content
```

---

## 💼 Nurse Services Management (NEW)

### 1. Add Service Offering
```http
POST /api/nurse/services
Authorization: Bearer {nurseToken}
Content-Type: application/json

{
  "serviceId": 1,
  "price": 550000,
  "unit": "fixed"  // fixed, hourly
}

Response: 201 Created
{
  "id": 1,
  "serviceId": 1,
  "serviceName": "Postpartum Care",
  "price": 550000,
  "unit": "fixed",
  "status": "enabled"
}
```

### 2. Get My Services
```http
GET /api/nurse/services
Authorization: Bearer {nurseToken}

Response: 200 OK
{
  "services": [
    {
      "id": 1,
      "serviceId": 1,
      "serviceName": "Postpartum Care",
      "price": 550000,
      "unit": "fixed",
      "status": "enabled"
    },
    {
      "id": 2,
      "serviceId": 2,
      "serviceName": "Baby Bathing",
      "price": 300000,
      "unit": "fixed",
      "status": "enabled"
    }
  ]
}
```

### 3. Update Service Offering
```http
PUT /api/nurse/services/1
Authorization: Bearer {nurseToken}
Content-Type: application/json

{
  "price": 600000,
  "unit": "fixed"
}

Response: 200 OK
{
  "id": 1,
  "serviceId": 1,
  "serviceName": "Postpartum Care",
  "price": 600000,
  "unit": "fixed",
  "status": "enabled"
}
```

### 4. Remove Service Offering
```http
DELETE /api/nurse/services/1
Authorization: Bearer {nurseToken}

Response: 204 No Content
```

---

## 📋 Booking Endpoints

### 1. Create Booking
```http
POST /api/bookings
Authorization: Bearer {customerToken}
Content-Type: application/json

{
  "nurseId": 3,
  "serviceId": 1,
  "startTime": "2026-03-22T09:00:00Z",
  "endTime": "2026-03-22T12:00:00Z",
  "address": "25 Nguyen Hue, District 1, HCMC",
  "notes": "First-time mother, need guidance"
}

Response: 201 Created
{
  "id": 1,
  "customerId": 2,
  "nurseId": 3,
  "serviceId": 1,
  "serviceName": "Postpartum Care",
  "startTime": "2026-03-22T09:00:00Z",
  "endTime": "2026-03-22T12:00:00Z",
  "address": "25 Nguyen Hue, District 1, HCMC",
  "status": "pending_confirm",
  "totalPrice": 500000,
  "notes": "First-time mother, need guidance",
  "createdAt": "2026-03-18T10:00:00Z"
}
```

### 2. Get My Bookings (As Customer)
```http
GET /api/bookings/my/customer?page=1&pageSize=10
Authorization: Bearer {customerToken}

Response: 200 OK
{
  "data": [
    {
      "id": 1,
      "nurseId": 3,
      "nurseName": "Nurse Mary",
      "serviceId": 1,
      "serviceName": "Postpartum Care",
      "startTime": "2026-03-22T09:00:00Z",
      "endTime": "2026-03-22T12:00:00Z",
      "status": "pending_confirm",
      "totalPrice": 500000
    }
  ],
  "totalCount": 5,
  "pageNumber": 1,
  "pageSize": 10
}
```

### 3. Get My Bookings (As Nurse)
```http
GET /api/bookings/my/nurse?page=1&pageSize=10
Authorization: Bearer {nurseToken}

Response: 200 OK
{
  "data": [
    {
      "id": 1,
      "customerId": 2,
      "customerName": "Jane Doe",
      "serviceId": 1,
      "serviceName": "Postpartum Care",
      "startTime": "2026-03-22T09:00:00Z",
      "endTime": "2026-03-22T12:00:00Z",
      "address": "25 Nguyen Hue, District 1, HCMC",
      "status": "pending_confirm",
      "totalPrice": 500000
    }
  ],
  "totalCount": 8,
  "pageNumber": 1,
  "pageSize": 10
}
```

### 4. Get Booking Detail
```http
GET /api/bookings/1
Authorization: Bearer {token}

Response: 200 OK
{
  "id": 1,
  "customerId": 2,
  "customerName": "Jane Doe",
  "nurseId": 3,
  "nurseName": "Nurse Mary",
  "serviceId": 1,
  "serviceName": "Postpartum Care",
  "startTime": "2026-03-22T09:00:00Z",
  "endTime": "2026-03-22T12:00:00Z",
  "address": "25 Nguyen Hue, District 1, HCMC",
  "status": "pending_confirm",
  "totalPrice": 500000,
  "notes": "First-time mother, need guidance",
  "statusHistory": [
    {
      "status": "pending_confirm",
      "changedBy": "system",
      "note": "Booking created",
      "changedAt": "2026-03-18T10:00:00Z"
    }
  ],
  "createdAt": "2026-03-18T10:00:00Z"
}
```

### 5. Update Booking Status (Nurse/Admin)
```http
PATCH /api/bookings/1/status
Authorization: Bearer {nurseToken}
Content-Type: application/json

{
  "status": "confirmed",  // confirmed, rejected, in_progress, completed, cancelled
  "note": "Confirmed, will arrive on time"
}

Response: 200 OK
{
  "id": 1,
  "status": "confirmed",
  "statusUpdatedAt": "2026-03-18T10:30:00Z",
  "statusHistory": [...]
}

Status Transitions:
- pending_confirm → confirmed, rejected
- confirmed → in_progress, cancelled
- in_progress → completed
- completed → (no transitions)
- rejected, cancelled → (end states)
```

### 6. Cancel Booking (NEW)
```http
POST /api/bookings/1/cancel
Authorization: Bearer {customerToken}
Content-Type: application/json

{
  "reason": "Personal reason",
  "note": "Need to reschedule"
}

Response: 200 OK
{
  "message": "Booking cancelled successfully",
  "refundAmount": 500000,
  "refundPercentage": 100,
  "reason": "Personal reason"
}

Refund Policy:
- ≥24 hours before service: 100% refund
- <24 hours before service: 50% refund
- After service time: 0% refund
```

---

## 💳 Payment Endpoints

### 1. Update Payment Status
```http
PUT /api/payments/booking/1
Authorization: Bearer {customerToken}
Content-Type: application/json

{
  "method": "bank_transfer",  // bank_transfer, credit_card, cash
  "status": "paid",  // paid, pending, failed
  "transactionId": "TXN-2026-03-18-001"
}

Response: 200 OK
{
  "id": 1,
  "bookingId": 1,
  "amount": 500000,
  "method": "bank_transfer",
  "status": "paid",
  "transactionId": "TXN-2026-03-18-001",
  "paidAt": "2026-03-18T10:30:00Z"
}
```

### 2. Get Payment Detail
```http
GET /api/payments/booking/1
Authorization: Bearer {token}

Response: 200 OK
{
  "id": 1,
  "bookingId": 1,
  "amount": 500000,
  "method": "bank_transfer",
  "status": "paid",
  "transactionId": "TXN-2026-03-18-001",
  "refundAmount": null,
  "refundReason": null,
  "refundStatus": null,
  "paidAt": "2026-03-18T10:30:00Z",
  "createdAt": "2026-03-18T09:00:00Z"
}
```

---

## 💬 Chat Endpoints

### 1. Get or Create Conversation by Booking
```http
POST /api/chat/conversations/by-booking/1
Authorization: Bearer {token}

Response: 200 OK or 201 Created
{
  "id": 1,
  "bookingId": 1,
  "participantIds": [2, 3],  // customer and nurse
  "createdAt": "2026-03-18T09:00:00Z"
}
```

### 2. Get Conversation Messages
```http
GET /api/chat/conversations/1/messages?page=1&pageSize=50
Authorization: Bearer {token}

Response: 200 OK
{
  "conversationId": 1,
  "messages": [
    {
      "id": 1,
      "senderId": 3,
      "senderName": "Nurse Mary",
      "message": "Hi! I can arrive at 9 AM",
      "createdAt": "2026-03-18T10:00:00Z"
    },
    {
      "id": 2,
      "senderId": 2,
      "senderName": "Jane Doe",
      "message": "Perfect! See you then",
      "createdAt": "2026-03-18T10:05:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 2
}
```

### 3. Send Message
```http
POST /api/chat/conversations/1/messages
Authorization: Bearer {token}
Content-Type: application/json

{
  "message": "Can you arrive 30 minutes earlier?"
}

Response: 201 Created
{
  "id": 3,
  "conversationId": 1,
  "senderId": 2,
  "senderName": "Jane Doe",
  "message": "Can you arrive 30 minutes earlier?",
  "createdAt": "2026-03-18T10:10:00Z"
}
```

### 4. Real-time Chat (SignalR)
```javascript
// Connect to WebSocket
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/chatHub", {
        accessTokenFactory: () => token
    })
    .withAutomaticReconnect()
    .build();

connection.start();

// Listen for messages
connection.on("ReceiveMessage", (senderId, senderName, message) => {
    console.log(`${senderName}: ${message}`);
});

// Send message
connection.invoke("SendMessage", conversationId, message);
```

---

## ⭐ Review & Rating Endpoints

### 1. Create Review
```http
POST /api/reviews
Authorization: Bearer {customerToken}
Content-Type: application/json

{
  "bookingId": 1,
  "rating": 5,
  "comment": "Excellent care, very professional and kind"
}

Response: 201 Created
{
  "id": 1,
  "bookingId": 1,
  "nurseId": 3,
  "rating": 5,
  "comment": "Excellent care, very professional and kind",
  "createdAt": "2026-03-18T10:30:00Z"
}
```

### 2. Get Nurse Reviews
```http
GET /api/reviews/nurse/3?page=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
{
  "reviews": [
    {
      "id": 1,
      "customerId": 2,
      "customerName": "Jane Doe",
      "rating": 5,
      "comment": "Excellent care, very professional",
      "createdAt": "2026-03-18T10:30:00Z"
    }
  ],
  "averageRating": 4.8,
  "totalReviews": 24,
  "pageNumber": 1,
  "pageSize": 10
}
```

### 3. Get Booking Reviews
```http
GET /api/reviews/booking/1
Authorization: Bearer {token}

Response: 200 OK or 404 Not Found
{
  "id": 1,
  "bookingId": 1,
  "rating": 5,
  "comment": "Excellent care",
  "createdAt": "2026-03-18T10:30:00Z"
}
```

---

## 🔔 Notification Endpoints

### 1. Get My Notifications
```http
GET /api/notifications/mine?page=1&pageSize=20
Authorization: Bearer {token}

Response: 200 OK
{
  "notifications": [
    {
      "id": 1,
      "title": "New booking request",
      "message": "Jane Doe booked your Postpartum Care service for 2026-03-22",
      "type": "booking_request",
      "isRead": false,
      "createdAt": "2026-03-18T10:00:00Z"
    }
  ],
  "unreadCount": 3,
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 15
}
```

### 2. Mark Notification as Read
```http
PATCH /api/notifications/1/read
Authorization: Bearer {token}

Response: 200 OK
{
  "id": 1,
  "isRead": true
}
```

### 3. Mark All as Read
```http
PATCH /api/notifications/read-all
Authorization: Bearer {token}

Response: 200 OK
{
  "message": "All notifications marked as read"
}
```

---

## 🚨 Dispute Endpoints

### 1. Create Dispute
```http
POST /api/disputes
Authorization: Bearer {token}
Content-Type: application/json

{
  "bookingId": 1,
  "reason": "Service quality issue",
  "description": "Nurse didn't arrive on time, caused inconvenience"
}

Response: 201 Created
{
  "id": 1,
  "bookingId": 1,
  "customerId": 2,
  "status": "open",  // open, resolved, rejected
  "reason": "Service quality issue",
  "description": "Nurse didn't arrive on time",
  "createdAt": "2026-03-18T10:30:00Z"
}
```

### 2. Get My Disputes
```http
GET /api/disputes/my?page=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
{
  "disputes": [
    {
      "id": 1,
      "bookingId": 1,
      "status": "open",
      "reason": "Service quality issue",
      "createdAt": "2026-03-18T10:30:00Z",
      "resolution": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 1
}
```

### 3. Resolve Dispute (Admin Only)
```http
PATCH /api/disputes/1
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "status": "resolved",  // resolved, rejected
  "resolution": "Full refund issued to customer"
}

Response: 200 OK
{
  "id": 1,
  "bookingId": 1,
  "status": "resolved",
  "resolution": "Full refund issued to customer",
  "resolvedAt": "2026-03-18T11:00:00Z"
}
```

---

## 👨‍💼 Admin Endpoints

### 1. Get Dashboard
```http
GET /api/admin/dashboard
Authorization: Bearer {adminToken}

Response: 200 OK
{
  "totalBookings": 120,
  "pendingConfirmations": 5,
  "totalRevenue": 45000000,
  "activeNurses": 24,
  "averageBookingRating": 4.7,
  "recentNurseRegistrations": 3
}
```

### 2. Get Pending Nurse Verifications
```http
GET /api/admin/nurses/pending?page=1&pageSize=10
Authorization: Bearer {adminToken}

Response: 200 OK
{
  "nurses": [
    {
      "userId": 3,
      "fullName": "Nurse Linda",
      "phone": "+84901234569",
      "email": "linda@example.com",
      "verificationStatus": "pending_review",
      "documents": [
        {
          "documentType": "license",
          "fileUrl": "https://..."
        }
      ],
      "appliedAt": "2026-03-15T10:00:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 3
}
```

### 3. Review & Approve Nurse
```http
POST /api/admin/nurses/3/review
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "isApproved": true,
  "reason": "All documents verified and valid"
}

Response: 200 OK
{
  "userId": 3,
  "fullName": "Nurse Linda",
  "verificationStatus": "verified",
  "approvedAt": "2026-03-18T11:00:00Z"
}
```

### 4. Get All Bookings (Admin)
```http
GET /api/admin/bookings?status=pending_confirm&page=1&pageSize=20
Authorization: Bearer {adminToken}

Response: 200 OK
{
  "bookings": [
    {
      "id": 1,
      "customerId": 2,
      "customerName": "Jane Doe",
      "nurseId": 3,
      "nurseName": "Nurse Mary",
      "status": "pending_confirm",
      "totalPrice": 500000,
      "createdAt": "2026-03-18T09:00:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 5
}
```

### 5. Get All Disputes (Admin)
```http
GET /api/admin/disputes?status=open&page=1&pageSize=10
Authorization: Bearer {adminToken}

Response: 200 OK
{
  "disputes": [
    {
      "id": 1,
      "bookingId": 1,
      "customerName": "Jane Doe",
      "nurseName": "Nurse Mary",
      "reason": "Service quality issue",
      "status": "open",
      "createdAt": "2026-03-18T10:30:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 1
}
```

---

## 📊 Error Responses

All endpoints may return the following error responses:

### 400 Bad Request
```json
{
  "message": "Invalid input",
  "errors": {
    "email": ["Email is required"],
    "password": ["Password must be at least 6 characters"]
  }
}
```

### 401 Unauthorized
```json
{
  "message": "Authentication required"
}
```

### 403 Forbidden
```json
{
  "message": "You don't have permission to access this resource"
}
```

### 404 Not Found
```json
{
  "message": "Resource not found"
}
```

### 409 Conflict
```json
{
  "message": "Email already registered"
}
```

### 500 Internal Server Error
```json
{
  "message": "An error occurred while processing your request"
}
```

---

## 🔐 Authorization Roles

| Role | Usage |
|------|-------|
| `Customer` | Book services, pay, review |
| `NurseUnconfirmed` | Create profile, upload docs, manage availability |
| `NurseConfirmed` | All nurse features after verification |
| `Admin` | Manage services, verify nurses, handle disputes |

---

## 🧪 Testing Checklist for Frontend

- [ ] Authentication (register, login, refresh token)
- [ ] View services catalog
- [ ] Search nurses with filters
- [ ] View nurse profile & availability
- [ ] Create booking
- [ ] Update booking status
- [ ] Cancel booking with refund
- [ ] Send/receive messages (chat)
- [ ] Create review
- [ ] Create dispute
- [ ] Get notifications
- [ ] Nurse services management
- [ ] Update profile
- [ ] Upload documents

---

## 📞 Support

For API issues or questions:
1. Check the HTTP status code and error message
2. Verify authentication token is valid
3. Check user role for permission issues
4. Review request payload format
5. Check endpoint path and method

---

**Last Updated:** March 18, 2026  
**Build Status:** ✅ All passing  
**Ready for Testing:** ✅ Yes
