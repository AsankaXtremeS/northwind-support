using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// The escalation history. Part of the ticket aggregate (see <see cref="TicketConfiguration"/>);
/// the agents before and after, and the agent who escalated, are referenced by id.
/// </summary>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", t =>
        {
            t.HasCheckConstraint("CK_TicketEscalations_FromPriority", "[FromPriority] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_TicketEscalations_ToPriority", "[ToPriority] BETWEEN 1 AND 4");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority).HasConversion<int>();

        builder.Property(e => e.ToPriority).HasConversion<int>();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.Reason).HasMaxLength(TicketEscalation.ReasonMaxLength).IsRequired();

        builder.Property(e => e.EscalatedBy).HasMaxLength(TicketEscalation.EscalatedByMaxLength).IsRequired();

        // History must survive: an agent with escalations against them cannot be deleted.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.EscalatedByAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serves "a ticket's history, newest first" with a seek and no sort, and doubles as the
        // index for the TicketId foreign key.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}
