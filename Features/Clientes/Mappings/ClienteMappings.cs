using tmr_backend.Features.Clientes.DTOs.Response;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Clientes.Mappings;

// Mappings de las entidades de BD hacia los DTOs de respuesta.
public static class ClienteMappings
{
    // -------------------------------------------------------------------------
    // ClienteListaResponse (item de la tabla)
    // El TipoIdentificacion viene del Include al catálogo (IdtipoidentificacionNavigation).
    // -------------------------------------------------------------------------
    public static ClienteListaResponse ToListaResponse(
        this TblAdministracionCliente c)
    {
        return new ClienteListaResponse(
            Id: c.Id,
            // Valor del catálogo TID (RUC / Cédula / Pasaporte).
            TipoIdentificacion: c.IdtipoidentificacionNavigation?.Valor ?? "",
            NumeroIdentificacion: c.Numeroidentificacion,
            NombreComercial: c.Nombrecomercial ?? "",
            Email: c.Email ?? "",
            Telefono: c.Telefono ?? "",
            Activo: c.Activo
        );
    }

    // -------------------------------------------------------------------------
    // ClienteDetalleResponse (modal de detalle)
    // Recibe la lista de proyectos aparte (se consulta en el servicio).
    // -------------------------------------------------------------------------
    public static ClienteDetalleResponse ToDetalleResponse(
        this TblAdministracionCliente c, List<ProyectoAsignadoResponse> proyectos)
    {
        return new ClienteDetalleResponse(
            Id: c.Id,
            TipoIdentificacion: c.IdtipoidentificacionNavigation?.Valor ?? "",
            NumeroIdentificacion: c.Numeroidentificacion,
            NombreComercial: c.Nombrecomercial ?? "",
            Activo: c.Activo,

            // ── Datos de contacto ──
            Nombres: c.Nombres,
            Apellidos: c.Apellidos,
            Email: c.Email ?? "",
            Telefono: c.Telefono ?? "",
            Direccion: c.Direccion ?? "",

            // ── Proyectos ──
            Proyectos: proyectos
        );
    }

    // -------------------------------------------------------------------------
    // TipoIdentificacionResponse (dropdown del catálogo TID)
    // -------------------------------------------------------------------------
    public static TipoIdentificacionResponse ToTipoIdentificacionResponse(
        this TblAdministracionCatalogoDetalle c) =>
        new(c.Id, c.Valor);
}