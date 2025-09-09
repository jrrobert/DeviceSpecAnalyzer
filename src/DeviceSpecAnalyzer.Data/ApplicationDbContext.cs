using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentContent> DocumentContents { get; set; }
    public DbSet<DocumentSection> DocumentSections { get; set; }
    public DbSet<SimilarityResult> SimilarityResults { get; set; }
    public DbSet<SectionSimilarity> SectionSimilarities { get; set; }
    public DbSet<DeviceDriver> DeviceDrivers { get; set; }
    public DbSet<DriverSimilarityReference> DriverSimilarityReferences { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Manufacturer).HasMaxLength(100);
            entity.Property(e => e.DeviceName).HasMaxLength(100);
            entity.Property(e => e.Protocol).HasMaxLength(50);
            entity.Property(e => e.Version).HasMaxLength(50);
            entity.Property(e => e.FileHash).HasMaxLength(32);
            entity.Property(e => e.ProcessingError).HasMaxLength(500);
            
            entity.HasIndex(e => e.FileName).IsUnique();
            entity.HasIndex(e => e.FileHash);
            entity.HasIndex(e => new { e.Manufacturer, e.DeviceName, e.Protocol });
        });

        builder.Entity<DocumentContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Summary).HasMaxLength(1000);
            entity.Property(e => e.Keywords).HasMaxLength(2000);
            
            entity.HasOne(e => e.Document)
                .WithOne(e => e.Content)
                .HasForeignKey<DocumentContent>(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DocumentSection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(200);
            
            entity.HasOne(e => e.Document)
                .WithMany(e => e.Sections)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => new { e.DocumentId, e.SectionType });
        });

        builder.Entity<SimilarityResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ComparisonMethod).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            
            entity.HasOne(e => e.SourceDocument)
                .WithMany(e => e.SourceSimilarities)
                .HasForeignKey(e => e.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(e => e.TargetDocument)
                .WithMany(e => e.TargetSimilarities)
                .HasForeignKey(e => e.TargetDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasIndex(e => new { e.SourceDocumentId, e.TargetDocumentId }).IsUnique();
            entity.HasIndex(e => e.OverallSimilarityScore);
        });

        builder.Entity<SectionSimilarity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MatchDetails).HasMaxLength(500);
            
            entity.HasOne(e => e.SimilarityResult)
                .WithMany(e => e.SectionSimilarities)
                .HasForeignKey(e => e.SimilarityResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeviceDriver>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DriverName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Version).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ImplementationStatus).HasMaxLength(100);
            entity.Property(e => e.CodeRepository).HasMaxLength(200);
            entity.Property(e => e.DeveloperName).HasMaxLength(100);
            
            entity.HasOne(e => e.Document)
                .WithMany(e => e.DeviceDrivers)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.DriverName);
        });

        builder.Entity<DriverSimilarityReference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferenceType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ReferenceNotes).HasMaxLength(500);
            
            entity.HasOne(e => e.DeviceDriver)
                .WithMany(e => e.SimilarityReferences)
                .HasForeignKey(e => e.DeviceDriverId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ReferencedDriver)
                .WithMany()
                .HasForeignKey(e => e.ReferencedDriverId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasIndex(e => new { e.DeviceDriverId, e.ReferencedDriverId }).IsUnique();
        });
    }
}