/* using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Infrastructure.Persistence.Configurations;

public class EventoOutboxConfiguration : IEntityTypeConfiguration<EventoOutbox>
{
    public void Configure(EntityTypeBuilder<EventoOutbox> builder)
    {
        builder.ToTable("tbl_time_report_evento_outbox", "time_report");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.TipoEvento)
            .HasColumnName("tipoevento")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .IsRequired();

        builder.Property(e => e.Intentos)
            .HasColumnName("intentos")
            .HasDefaultValue(0);

        builder.Property(e => e.UltimoError)
            .HasColumnName("ultimoerror");

        builder.Property(e => e.FechaCreacion)
            .HasColumnName("fechacreacion")
            .IsRequired();

        builder.Property(e => e.FechaProcesado)
            .HasColumnName("fechaprocesado");
    }
}
 */