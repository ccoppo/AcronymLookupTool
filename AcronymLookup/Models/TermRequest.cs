using System;

namespace AcronymLookup.Models
{
    /// <summary>
    /// Represents a single row from the TermRequests table.
    /// Used for the approval queue workflow (Add/Edit/Delete requests).
    /// </summary>
    public class TermRequest
    {
        // ── Primary Key 
        public int TermRequestID { get; set; }

        // ── Request Context 
        public int ProjectID { get; set; }
        public int RequestedByUserID { get; set; }
        public string RequestedByUserName { get; set; } = string.Empty;   // joined from Users table

        /// <summary>
        /// The type of request: "Add", "Edit", or "Delete".
        /// </summary>
        public string RequestType { get; set; } = string.Empty;

        // ── Term Data (for Add requests)
        public string? NewAbbreviation { get; set; }
        public string? NewDefinition { get; set; }
        public string? NewCategory { get; set; }
        public string? NewNotes { get; set; }

        // ── Term Data (for Edit / Delete requests)
        public int? AbbreviationID { get; set; }
        public string? EditedDefinition { get; set; }
        public string? EditedCategory { get; set; }
        public string? EditedNotes { get; set; }

        // ── Request Details
        public string? RequestReason { get; set; }
        public DateTime DateRequested { get; set; }

        // ── Approval
        /// <summary>
        /// The current status of the request: "Pending", "Approved", or "Rejected".
        /// </summary>
        public string Status { get; set; } = "Pending";
        public int? ReviewedByUserID { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime? DateReviewed { get; set; }

        // ── Convenience helpers (read-only) 

        /// <summary>
        /// The term abbreviation to display in the queue, regardless of request type.
        /// </summary>
        public string DisplayAbbreviation =>
            RequestType == "Add"
                ? (NewAbbreviation ?? "(unknown)")
                : (NewAbbreviation ?? "(unknown)");   // Edit/Delete will populate this via JOIN

        /// <summary>
        /// The definition to display in the queue for review.
        /// For Add: the proposed new definition.
        /// For Edit: the proposed edited definition.
        /// For Delete: empty (just showing what will be removed).
        /// </summary>
        public string DisplayDefinition => RequestType switch
        {
            "Add"    => NewDefinition ?? string.Empty,
            "Edit"   => EditedDefinition ?? string.Empty,
            "Delete" => "(This term will be deleted)",
            _        => string.Empty
        };

    }
}




