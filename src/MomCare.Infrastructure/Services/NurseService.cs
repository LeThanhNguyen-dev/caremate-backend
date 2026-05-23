using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class NurseService : INurseService
{
    private readonly MomCareContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICloudinaryService _cloudinaryService;

    private const int MaxIdCardFront = 1;
    private const int MaxIdCardBack = 1;
    private const int MaxCertificates = 4;
    private const long MaxDocumentBytes = 5 * 1024 * 1024;

    public NurseService(
        MomCareContext context,
        UserManager<ApplicationUser> userManager,
        ICloudinaryService cloudinaryService)
    {
        _context = context;
        _userManager = userManager;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<NurseProfileDetailDto?> GetProfileAsync(int userId)
    {
        var nurse = await _userManager.FindByIdAsync(userId.ToString());
        if (nurse == null) return null;

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null) return null;

        var reviews = await _context.Reviews
            .Where(r => r.NurseId == userId && !r.IsDeleted) // Exclude soft-deleted reviews
            .Include(r => r.Booking)
                .ThenInclude(b => b.Service)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDetailDto
            {
                Id = r.Id,
                BookingId = r.BookingId,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.FullName,
                CustomerAvatar = r.Customer.Avatar,
                ServiceId = r.Booking.ServiceId,
                ServiceName = r.Booking.Service.Name,
                ServiceCategory = r.Booking.Service.Category,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var ratingDistribution = reviews
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        for (int i = 1; i <= 5; i++) ratingDistribution.TryAdd(i, 0);

        return new NurseProfileDetailDto
        {
            UserId = nurse.Id,
            FullName = nurse.FullName,
            Email = nurse.Email ?? string.Empty,
            Phone = nurse.PhoneNumber,
            Avatar = nurse.Avatar,
            BankBin = nurse.BankBin,
            BankAccountNumber = nurse.BankAccountNumber,
            BankAccountName = nurse.BankAccountName,
            Bio = profile.Bio,
            Specialization = profile.Specialization,
            YearsExperience = profile.YearsExperience,
            ServiceRadiusKm = profile.ServiceRadiusKm,
            IsVerified = profile.IsVerified,
            AverageRating = profile.AverageRating,
            RejectionReason = profile.RejectionReason,
            VerificationSubmissionStatus = profile.VerificationSubmissionStatus,
            TotalReviews = reviews.Count,
            RatingDistribution = ratingDistribution,
            Documents = profile.Documents.Select(d => new NurseDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                FileUrl = _cloudinaryService.GetSignedUrl(d.PublicId),
                PublicId = d.PublicId,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList(),
            Reviews = reviews
        };
    }

    public async Task<bool> UpdateProfileAsync(int userId, UpdateNurseProfileDto updateDto)
    {
        var profile = await _context.NurseProfiles.FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null) return false;
        var nurse = await _userManager.FindByIdAsync(userId.ToString());
        if (nurse == null) return false;

        if (!string.IsNullOrWhiteSpace(updateDto.FullName))
        {
            nurse.FullName = updateDto.FullName.Trim();
        }

        nurse.PhoneNumber = string.IsNullOrWhiteSpace(updateDto.PhoneNumber) ? null : updateDto.PhoneNumber.Trim();
        nurse.Avatar = string.IsNullOrWhiteSpace(updateDto.Avatar) ? nurse.Avatar : updateDto.Avatar.Trim();
        profile.Bio = updateDto.Bio;
        profile.Specialization = string.IsNullOrWhiteSpace(updateDto.Specialization) ? null : updateDto.Specialization.Trim();
        profile.YearsExperience = updateDto.YearsExperience;
        profile.ServiceRadiusKm = updateDto.ServiceRadiusKm;
        profile.UpdatedAt = DateTime.UtcNow;

        nurse.BankBin = string.IsNullOrWhiteSpace(updateDto.BankBin) ? null : updateDto.BankBin.Trim();
        nurse.BankAccountNumber = string.IsNullOrWhiteSpace(updateDto.BankAccountNumber) ? null : updateDto.BankAccountNumber.Trim();
        nurse.BankAccountName = string.IsNullOrWhiteSpace(updateDto.BankAccountName) ? null : updateDto.BankAccountName.Trim();
        nurse.UpdatedAt = DateTime.UtcNow;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<string?> UploadAvatarAsync(int userId, IFormFile file)
    {
        var nurse = await _userManager.FindByIdAsync(userId.ToString());
        if (nurse == null) return null;

        var uploadResult = await _cloudinaryService.UploadPublicAsync(file, $"caremate/nurses/{userId}/avatar");
        nurse.Avatar = uploadResult.Url;
        nurse.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(nurse);
        return result.Succeeded ? uploadResult.Url : null;
    }

    public async Task<NurseDocumentDto?> UploadDocumentAsync(int userId, UploadDocumentDto uploadDto)
    {
        var normalizedType = NormalizeIncomingDocumentType(uploadDto.Type);
        if (!DocumentTypes.IsValid(normalizedType))
        {
            throw new ArgumentException($"Invalid document type: {uploadDto.Type}");
        }

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null) return null;

        EnsureDocumentsCanBeChanged(profile);
        ValidateDocumentFile(uploadDto.File);
        ValidateDocumentLimits(profile.Documents, normalizedType);

        var folder = $"caremate/nurses/{userId}/documents";
        var uploadResult = await _cloudinaryService.UploadPrivateAsync(uploadDto.File, folder);

        var document = new Document
        {
            NurseProfileId = profile.Id,
            Type = normalizedType,
            PublicId = uploadResult.PublicId,
            Status = DocumentStatuses.PendingReview,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        if (profile.IsVerified == "rejected")
        {
            profile.IsVerified = "unverified";
            profile.RejectionReason = null;
            profile.ConfirmedAt = null;
            profile.VerificationSubmissionStatus = "draft";
            profile.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        return new NurseDocumentDto
        {
            Id = document.Id,
            Type = document.Type,
            FileUrl = _cloudinaryService.GetSignedUrl(document.PublicId),
            PublicId = document.PublicId,
            Status = document.Status,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }

    public async Task<IReadOnlyList<NurseDocumentDto>> UploadDocumentsAsync(int userId, UploadDocumentsDto uploadDto)
    {
        var normalizedType = NormalizeIncomingDocumentType(uploadDto.Type);
        if (!DocumentTypes.IsValid(normalizedType))
        {
            throw new ArgumentException($"Invalid document type: {uploadDto.Type}");
        }

        if (uploadDto.Files == null || uploadDto.Files.Count == 0)
        {
            throw new ArgumentException("Please select at least one file.");
        }

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null) return Array.Empty<NurseDocumentDto>();

        EnsureDocumentsCanBeChanged(profile);
        if (IsSingleDocumentType(normalizedType) && uploadDto.Files.Count > 1)
        {
            throw new ArgumentException("Only 1 file is allowed for ID card front/back.");
        }

        var createdDocs = new List<Document>();
        var folder = $"caremate/nurses/{userId}/documents";

        foreach (var file in uploadDto.Files)
        {
            ValidateDocumentFile(file);

            var uploadResult = await _cloudinaryService.UploadPrivateAsync(file, folder);
            var existingSingleDoc = IsSingleDocumentType(normalizedType)
                ? profile.Documents.FirstOrDefault(d => d.Type.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
                : null;

            if (existingSingleDoc != null)
            {
                if (!string.IsNullOrEmpty(existingSingleDoc.PublicId))
                {
                    await _cloudinaryService.DeleteAsync(existingSingleDoc.PublicId);
                }

                existingSingleDoc.PublicId = uploadResult.PublicId;
                existingSingleDoc.Status = DocumentStatuses.PendingReview;
                existingSingleDoc.UpdatedAt = DateTime.UtcNow;
                createdDocs.Add(existingSingleDoc);
                continue;
            }

            ValidateDocumentLimits(profile.Documents, normalizedType);

            var document = new Document
            {
                NurseProfileId = profile.Id,
                Type = normalizedType,
                PublicId = uploadResult.PublicId,
                Status = DocumentStatuses.PendingReview,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            profile.Documents.Add(document);

            createdDocs.Add(document);
        }

        if (profile.IsVerified == "rejected")
        {
            profile.IsVerified = "unverified";
            profile.RejectionReason = null;
            profile.ConfirmedAt = null;
            profile.VerificationSubmissionStatus = "draft";
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return createdDocs.Select(document => new NurseDocumentDto
        {
            Id = document.Id,
            Type = document.Type,
            FileUrl = _cloudinaryService.GetSignedUrl(document.PublicId),
            PublicId = document.PublicId,
            Status = document.Status,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        }).ToList();
    }

    public async Task<bool> SubmitVerificationAsync(int userId)
    {
        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null) return false;

        if (profile.IsVerified == "verified" || profile.VerificationSubmissionStatus == "approved")
        {
            throw new ArgumentException("This profile has already been verified.");
        }

        if (profile.VerificationSubmissionStatus == "submitted")
        {
            throw new ArgumentException("This verification dossier has already been submitted and is waiting for review.");
        }

        var hasFront = profile.Documents.Any(d => d.Type == DocumentTypes.IdCardFront);
        var hasBack = profile.Documents.Any(d => d.Type == DocumentTypes.IdCardBack);
        var hasCertificate = profile.Documents.Any(d => d.Type == DocumentTypes.Certificate);
        if (!hasFront || !hasBack || !hasCertificate)
        {
            throw new ArgumentException("Verification dossier is incomplete. Required: ID card front, ID card back, and certificate.");
        }

        profile.VerificationSubmissionStatus = "submitted";
        profile.IsVerified = "unverified";
        profile.RejectionReason = null;
        profile.ConfirmedAt = null;
        profile.UpdatedAt = DateTime.UtcNow;
        foreach (var doc in profile.Documents)
        {
            doc.Status = DocumentStatuses.PendingReview;
            doc.UpdatedAt = DateTime.UtcNow;
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<NurseDocumentDto?> ReplaceDocumentAsync(int userId, int documentId, UploadDocumentDto uploadDto)
    {
        var normalizedType = NormalizeIncomingDocumentType(uploadDto.Type);
        if (!DocumentTypes.IsValid(normalizedType))
        {
            throw new ArgumentException($"Invalid document type: {uploadDto.Type}");
        }

        var profile = await _context.NurseProfiles.FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null) return null;

        var existingDoc = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.NurseProfileId == profile.Id);

        if (existingDoc == null) return null;

        EnsureDocumentsCanBeChanged(profile);
        ValidateDocumentFile(uploadDto.File);

        if (!string.IsNullOrEmpty(existingDoc.PublicId))
        {
            await _cloudinaryService.DeleteAsync(existingDoc.PublicId);
        }

        var folder = $"caremate/nurses/{userId}/documents";
        var uploadResult = await _cloudinaryService.UploadPrivateAsync(uploadDto.File, folder);

        existingDoc.PublicId = uploadResult.PublicId;
        existingDoc.Type = normalizedType;
        existingDoc.Status = DocumentStatuses.PendingReview;
        existingDoc.UpdatedAt = DateTime.UtcNow;
        if (profile.IsVerified == "rejected")
        {
            profile.IsVerified = "unverified";
            profile.RejectionReason = null;
            profile.ConfirmedAt = null;
            profile.VerificationSubmissionStatus = "draft";
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return new NurseDocumentDto
        {
            Id = existingDoc.Id,
            Type = existingDoc.Type,
            FileUrl = _cloudinaryService.GetSignedUrl(existingDoc.PublicId),
            PublicId = existingDoc.PublicId,
            Status = existingDoc.Status,
            CreatedAt = existingDoc.CreatedAt,
            UpdatedAt = existingDoc.UpdatedAt
        };
    }

    public async Task<bool> DeleteDocumentAsync(int userId, int documentId)
    {
        var profile = await _context.NurseProfiles.FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null) return false;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.NurseProfileId == profile.Id);

        if (document == null) return false;

        EnsureDocumentsCanBeChanged(profile);

        if (!string.IsNullOrEmpty(document.PublicId))
        {
            await _cloudinaryService.DeleteAsync(document.PublicId);
        }

        _context.Documents.Remove(document);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<string?> GetDocumentSignedUrlAsync(int userId, int documentId)
    {
        var profile = await _context.NurseProfiles.FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null) return null;

        // Authorization: Only the owner nurse can access their documents
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.NurseProfileId == profile.Id);

        if (document == null) return null;

        return _cloudinaryService.GetSignedUrl(document.PublicId);
    }

    private static void ValidateDocumentLimits(ICollection<Document> existingDocs, string newType)
    {
        var typeLower = newType.ToLowerInvariant();
        var count = existingDocs.Count(d => d.Type.Equals(typeLower, StringComparison.OrdinalIgnoreCase));

        switch (typeLower)
        {
            case DocumentTypes.IdCardFront when count >= MaxIdCardFront:
                throw new ArgumentException("Maximum 1 ID card front image allowed. Use replace instead.");
            case DocumentTypes.IdCardBack when count >= MaxIdCardBack:
                throw new ArgumentException("Maximum 1 ID card back image allowed. Use replace instead.");
            case DocumentTypes.Certificate when count >= MaxCertificates:
                throw new ArgumentException($"Maximum {MaxCertificates} certificate images allowed.");
        }
    }

    private static void EnsureDocumentsCanBeChanged(NurseProfile profile)
    {
        if (profile.VerificationSubmissionStatus == "submitted")
        {
            throw new ArgumentException("This verification dossier is already submitted. Please wait for admin review before changing documents.");
        }

        if (profile.IsVerified == "verified" || profile.VerificationSubmissionStatus == "approved")
        {
            throw new ArgumentException("Verified documents cannot be changed from this flow.");
        }
    }

    private static void ValidateDocumentFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        if (file.Length > MaxDocumentBytes)
        {
            throw new ArgumentException("Each document must be 5MB or smaller.");
        }

        var contentType = file.ContentType.Trim().ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png"))
        {
            throw new ArgumentException("Only JPG and PNG documents are supported.");
        }
    }

    private static bool IsSingleDocumentType(string type)
    {
        return type.Equals(DocumentTypes.IdCardFront, StringComparison.OrdinalIgnoreCase)
            || type.Equals(DocumentTypes.IdCardBack, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIncomingDocumentType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "id_card" => DocumentTypes.IdCardFront,
            "hospital_certificate" => DocumentTypes.Certificate,
            _ => normalized
        };
    }
}
