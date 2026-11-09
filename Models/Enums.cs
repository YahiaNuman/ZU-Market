namespace ZuMarket.Models;

public enum VerificationStatus { Pending = 1, Approved = 2, Rejected = 3 }
public enum ListingCondition { New = 1, LikeNew = 2, Good = 3, Fair = 4 }
public enum ListingModerationStatus { Pending = 1, Approved = 2, Rejected = 3, Hidden = 4, Removed = 5 }
public enum ListingAvailabilityStatus { Available = 1, Sold = 2, Removed = 3 }
public enum ListingReviewState { PendingReview = 1, AiApproved = 2, NeedsManualReview = 3, ManuallyApproved = 4, ManuallyRejected = 5 }
public enum AiReviewDecision { Approved = 1, NeedsManualReview = 2, ProviderUnavailable = 3, InvalidResponse = 4 }
public enum AiProviderAvailability
{
    Disabled,
    MissingApiKey,
    InvalidConfiguration,
    ConnectionFailed,
    AuthenticationFailed,
    ProviderError,
    InvalidResponse,
    Ready
}
public enum ReportReason { Inappropriate = 1, Misleading = 2, SuspectedFraud = 3, Prohibited = 4, Other = 5 }
public enum ReportStatus { Pending = 1, Resolved = 2, Dismissed = 3 }
public enum ReportSuggestedAction { Dismiss = 1, Investigate = 2, Remove = 3 }
public enum ModerationActionType
{
    Approved = 1,
    Rejected = 2,
    Hidden = 3,
    Removed = 4,
    Restored = 5,
    UserDisabled = 6,
    UserEnabled = 7,
    VerificationApproved = 8,
    VerificationRejected = 9,
    ReportResolved = 10,
    ReportDismissed = 11,
    AdminGranted = 12,
    AdminRevoked = 13,
    AiOverrideApproved = 14,
    AiOverrideRejected = 15
}
