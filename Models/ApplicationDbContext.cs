using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ZuMarket.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<VerificationRequest> VerificationRequests => Set<VerificationRequest>();
    public DbSet<ListingReport> ListingReports => Set<ListingReport>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<AiListingReview> AiListingReviews => Set<AiListingReview>();
    public DbSet<ListingReportSummary> ListingReportSummaries => Set<ListingReportSummary>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().HasIndex(x => x.UniversityId).IsUnique().HasFilter("[UniversityId] IS NOT NULL");
        builder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Entity<ApplicationUser>().HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
        builder.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<Category>().HasData(
            new Category { Id = 5, Key = "FacultyIT", Slug = "it", SortOrder = 1 },
            new Category { Id = 6, Key = "FacultyNursing", Slug = "nursing", SortOrder = 2 },
            new Category { Id = 7, Key = "FacultyMedical", Slug = "medical", SortOrder = 3 },
            new Category { Id = 8, Key = "FacultyLiterature", Slug = "literature", SortOrder = 4 },
            new Category { Id = 9, Key = "FacultyDentistry", Slug = "dentistry", SortOrder = 5 },
            new Category { Id = 10, Key = "FacultyPharmacy", Slug = "pharmacy", SortOrder = 6 },
            new Category { Id = 11, Key = "FacultyEngineering", Slug = "engineering", SortOrder = 7 },
            new Category { Id = 12, Key = "FacultyScience", Slug = "science", SortOrder = 8 },
            new Category { Id = 13, Key = "FacultyBusiness", Slug = "business", SortOrder = 9 },
            new Category { Id = 14, Key = "FacultyLaw", Slug = "law", SortOrder = 10 },
            new Category { Id = 15, Key = "FacultyEducation", Slug = "education", SortOrder = 11 },
            new Category { Id = 16, Key = "FacultyMedia", Slug = "media", SortOrder = 12 },
            new Category { Id = 17, Key = "FacultyDesign", Slug = "design", SortOrder = 13 },
            new Category { Id = 18, Key = "FacultySharia", Slug = "sharia", SortOrder = 14 },
            new Category { Id = 4, Key = "Other", Slug = "other", SortOrder = 15 });

        builder.Entity<Listing>().Property(x => x.Price).HasPrecision(12, 2);
        builder.Entity<Listing>().Property(x => x.ReviewState).HasDefaultValue(ListingReviewState.PendingReview).HasSentinel((ListingReviewState)0);
        builder.Entity<Listing>().HasIndex(x => new { x.ModerationStatus, x.AvailabilityStatus, x.CreatedAtUtc });
        builder.Entity<Listing>().HasIndex(x => x.CategoryId);
        builder.Entity<Listing>().HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Listing>().HasOne(x => x.Category).WithMany(x => x.Listings).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ListingImage>().HasIndex(x => new { x.ListingId, x.SortOrder });
        builder.Entity<ListingImage>().HasOne(x => x.Listing).WithMany(x => x.Images).HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AiListingReview>().Property(x => x.Confidence).HasPrecision(5, 4);
        builder.Entity<AiListingReview>().HasIndex(x => new { x.ListingId, x.CreatedAtUtc });
        builder.Entity<AiListingReview>().HasOne(x => x.Listing).WithMany(x => x.AiReviews).HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Favorite>().HasKey(x => new { x.UserId, x.ListingId });
        builder.Entity<Favorite>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Favorite>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Conversation>().HasIndex(x => new { x.ListingId, x.BuyerId, x.SellerId }).IsUnique();
        builder.Entity<Conversation>().HasIndex(x => x.LastMessageAtUtc);
        builder.Entity<Conversation>().HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Conversation>().HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Conversation>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Message>().HasIndex(x => new { x.ConversationId, x.SentAtUtc });
        builder.Entity<Message>().HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Message>().HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VerificationRequest>().HasIndex(x => new { x.UserId, x.SubmittedAtUtc });
        builder.Entity<VerificationRequest>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<VerificationRequest>().HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ListingReport>().HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.Entity<ListingReport>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ListingReport>().HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ListingReport>().HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ListingReportSummary>().HasIndex(x => new { x.ListingId, x.CreatedAtUtc });
        builder.Entity<ListingReportSummary>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ListingReportSummary>().HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ModerationAction>().HasIndex(x => x.CreatedAtUtc);
        builder.Entity<ModerationAction>().HasOne(x => x.Administrator).WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ModerationAction>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ModerationAction>().HasOne(x => x.TargetUser).WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
