using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("nurse_document_ocr_results")]
public class NurseDocumentOcrResult
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("nurse_document_id")]
    public int NurseDocumentId { get; set; }

    [Column("document_type")]
    [MaxLength(80)]
    public required string DocumentType { get; set; }

    [Column("raw_ocr_text")]
    public string RawOcrText { get; set; } = string.Empty;

    [Column("parsed_data_json")]
    public string ParsedDataJson { get; set; } = "{}";

    [Column("ocr_status")]
    [MaxLength(30)]
    public string OcrStatus { get; set; } = "WARNING";

    [Column("warnings_json")]
    public string WarningsJson { get; set; } = "[]";

    [Column("processed_by")]
    [MaxLength(80)]
    public string ProcessedBy { get; set; } = "auto";

    [Column("processed_at")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    [Column("attempt_count")]
    public int AttemptCount { get; set; } = 1;

    [ForeignKey(nameof(NurseDocumentId))]
    public virtual Document NurseDocument { get; set; } = null!;
}
