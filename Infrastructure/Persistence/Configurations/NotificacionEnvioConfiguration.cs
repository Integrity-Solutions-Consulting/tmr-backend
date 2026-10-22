/* using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using tmr_backend.Features.Notificaciones.Domain;

namespace tmr_backend.Infrastructure.Persistence.Configurations;

public class NotificacionEnvioConfiguration : IEntityTypeConfiguration<NotificacionEnvio>
{
    public void Configure(EntityTypeBuilder<NotificacionEnvio> builder)
    {
        builder.ToTable("tbl_time_report_notificacion_envio", "time_report");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.IdEmpleado)
            .HasColumnName("idempleado")
            .IsRequired();

        builder.Property(e => e.InicioPeriodo)
            .HasColumnName("inicioperiodo")
            .IsRequired();

        builder.Property(e => e.FinPeriodo)
            .HasColumnName("finperiodo")
            .IsRequired();

        builder.Property(e => e.FechaCorte)
            .HasColumnName("fechacorte");

        builder.Property(e => e.Origen)
            .HasColumnName("origen")
            .IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .IsRequired();

        builder.Property(e => e.HorasFaltantes)
            .HasColumnName("horasfaltantes")
            .HasPrecision(18, 2);

        builder.Property(e => e.IdUsuarioEjecutor)
            .HasColumnName("idusuarioejecutor");

        builder.Property(e => e.FechaEnvio)
            .HasColumnName("fechaenvio")
            .IsRequired();

        builder.Property(e => e.DetalleError)
            .HasColumnName("detalleerror");
    }
}
 */