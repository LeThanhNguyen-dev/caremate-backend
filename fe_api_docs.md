# API Documentation for Nurse Confirmation Flow

This document provides the API endpoints and data structures for the Frontend team to implement the Nurse profile submission and Admin review process.

## 1. Authentication
All endpoints require a Bearer Token in the `Authorization` header.
- **Nurse Endpoints**: Require `nurse_unconfirmed` or `nurse_confirmed` role.
- **Admin Endpoints**: Require `admin` role.

---

## 2. Nurse Operations

### Get Current Profile
Retrieve the logged-in nurse's profile and documents.
- **URL**: `/api/Nurse/profile`
- **Method**: `GET`
- **Response**: `NurseProfileDetailDto`

### Update Profile
Update bio and experience details.
- **URL**: `/api/Nurse/profile`
- **Method**: `PUT`
- **Body**: `UpdateNurseProfileDto`
```json
{
  "bio": "I have 5 years of experience in newborn care.",
  "yearsExperience": 5,
  "serviceRadiusKm": 10
}
```

### Upload Document
Submit a certificate, ID card, or other documents.
- **URL**: `/api/Nurse/documents`
- **Method**: `POST`
- **Body**: `UploadDocumentDto`
```json
{
  "type": "hospital_certificate",
  "fileUrl": "https://storage.com/cert123.pdf"
}
```

---

## 3. Admin Operations

### Get Pending Nurses
List all nurses with `nurse_unconfirmed` role who need review.
- **URL**: `/api/Admin/nurses/pending`
- **Method**: `GET`
- **Response**: `Array<NurseProfileDetailDto>`

### Get Nurse Details
View specific details and documents of a nurse.
- **URL**: `/api/Admin/nurses/{id}/details`
- **Method**: `GET`
- **Response**: `NurseProfileDetailDto`

### Review Nurse
Approve or reject a nurse. Approving will transition their role to `nurse_confirmed`.
- **URL**: `/api/Admin/nurses/{id}/review`
- **Method**: `POST`
- **Body**: `ReviewNurseProfileDto`
```json
{
  "isApproved": true,
  "comment": "Credentials verified successfully."
}
```

---

## 4. Common Data Structures

### NurseProfileDetailDto
```json
{
  "userId": 123,
  "fullName": "Jane Doe",
  "email": "jane@example.com",
  "phone": "0123456789",
  "bio": "String...",
  "yearsExperience": 5,
  "serviceRadiusKm": 10,
  "isVerified": "unverified | verified | rejected",
  "documents": [
    {
      "id": 1,
      "type": "id_card",
      "fileUrl": "...",
      "status": "pending_review | approved | rejected"
    }
  ]
}
```
