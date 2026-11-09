using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZuMarket.Models;

#nullable disable

namespace ZuMarket.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261109090000_AddAiListingReview")]
partial class AddAiListingReview
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("ZuMarket.Models.Listing", b =>
        {
            b.Property<int>("Id").HasColumnType("int");
            b.Property<int>("ReviewState").HasColumnType("int").HasDefaultValue(1);
            b.HasKey("Id");
            b.ToTable("Listings");
        });
        modelBuilder.Entity("ZuMarket.Models.AiListingReview", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int").HasAnnotation("SqlServer:Identity", "1, 1");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<decimal>("Confidence").HasPrecision(5, 4).HasColumnType("decimal(5,4)");
            b.Property<int>("Decision").HasColumnType("int");
            b.Property<string>("Explanation").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<bool>("FinalDecisionWasAi").HasColumnType("bit");
            b.Property<int>("ListingId").HasColumnType("int");
            b.Property<string>("Model").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<string>("Provider").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<string>("RuleChecksJson").IsRequired().HasColumnType("nvarchar(max)");
            b.HasKey("Id");
            b.HasIndex("ListingId", "CreatedAtUtc");
            b.ToTable("AiListingReviews");
        });
        modelBuilder.Entity("ZuMarket.Models.AiListingReview", b => b.HasOne("ZuMarket.Models.Listing", "Listing").WithMany("AiReviews").HasForeignKey("ListingId").OnDelete(DeleteBehavior.Cascade).IsRequired());
        modelBuilder.Entity("ZuMarket.Models.Listing", b => b.Navigation("AiReviews"));
    }
}
