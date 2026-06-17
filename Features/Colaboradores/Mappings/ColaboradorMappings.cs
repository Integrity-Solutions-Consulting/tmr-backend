using tmr_backend.Features.Colaboradores.DTOs.Response;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Colaboradores.Mappings;

// Mappings de las entidades de BD hacia los DTOs de respuesta.
public static class ColaboradorMappings
{
    // ColaboradorListaResponse (item de la tabla)
    // Recibe numProyectos aparte porque ese conteo se calcula en el servicio.
    public static ColaboradorListaResponse ToListaResponse(
        this TblAdministracionEmpleado e, int numProyectos)
    {
        // Datos de la persona (vienen del Include en el servicio).
        var persona = e.IdpersonaNavigation;

        return new ColaboradorListaResponse(
            Id: e.Id,
            IdPersona: e.Idpersona,
            CodigoEmpleado: e.Codigoempleado,
            NumeroIdentificacion: persona?.Numeroidentificacion ?? "",
            // Asociación: el valor del catálogo de empresa (RPS, ISC, RPS E ISC).
            Asociacion: e.IdempresacatalogoNavigation?.Valor ?? "",
            // Nombre completo = nombres + apellidos.
            NombreCompleto: $"{persona?.Nombres} {persona?.Apellidos}".Trim(),
            Email: persona?.Email ?? "",
            Cargo: e.IdcargoNavigation?.Nombrecargo ?? "",
            NumProyectos: numProyectos,
            Activo: e.Activo
        );
    }

    // -------------------------------------------------------------------------
    // ColaboradorDetalleResponse (modal de detalle / editar)
    // Recibe la lista de proyectos aparte (se consulta en el servicio).
    // -------------------------------------------------------------------------
    public static ColaboradorDetalleResponse ToDetalleResponse(
        this TblAdministracionEmpleado e, List<ProyectoAsignadoResponse> proyectos)
    {
        var persona = e.IdpersonaNavigation;

        return new ColaboradorDetalleResponse(
            Id: e.Id,
            CodigoEmpleado: e.Codigoempleado,
            Asociacion: e.IdempresacatalogoNavigation?.Valor ?? "",
            TipoContrato: e.IdtipocontratoNavigation?.Valor ?? "",
            Activo: e.Activo,

            // ── IDs necesarios para precargar el modal editar ──
            IdEmpresaCatalogo: e.Idempresacatalogo,
            TipoPersona: persona?.Tipopersona ?? "NATURAL",
            IdTipoIdentificacion: persona?.Idtipoidentificacion,
            IdGenero: persona?.Idgenero,
            IdNacionalidad: persona?.Idnacionalidad,
            IdTipoContrato: e.Idtipocontrato,
            IdDepartamento: e.IdcargoNavigation?.Iddepartamento,
            IdCargo: e.Idcargo,
            IdModoTrabajo: e.Idmodotrabajo,
            IdCategoriaEmpleado: e.Idcategoriaempleado,

            // ── Datos laborales ──
            // El departamento se obtiene a través del cargo (cargo → departamento).
            Departamento: e.IdcargoNavigation?.IddepartamentoNavigation?.Valor ?? "",
            FechaIngreso: e.Fechaingreso,
            Cargo: e.IdcargoNavigation?.Nombrecargo ?? "",
            AniosExperiencia: e.Aniosexperiencia,
            Modalidad: e.IdmodotrabajoNavigation?.Valor ?? "",
            Categoria: e.IdcategoriaempleadoNavigation?.Valor ?? "",

            // ── Datos personales ──
            IdPersona: persona?.Id ?? 0,
            Nombres: persona?.Nombres ?? "",
            Apellidos: persona?.Apellidos ?? "",
            NumeroIdentificacion: persona?.Numeroidentificacion ?? "",
            FechaNacimiento: persona?.Fechanacimiento,
            Genero: persona?.IdgeneroNavigation?.Valor ?? "",
            Nacionalidad: persona?.IdnacionalidadNavigation?.Valor ?? "",

            // ── Datos de contacto ──
            Email: persona?.Email ?? "",
            Telefono: persona?.Telefono ?? "",
            Direccion: persona?.Direccion ?? "",

            // ── Proyectos ──
            Proyectos: proyectos
        );
    }

    // Para los dropdowns.
    public static CatalogoResponse ToCatalogoResponse(
        this TblAdministracionCatalogoDetalle c) =>
        new(c.Id, c.Valor);

    // Dropdown de cargos por departamento.
    public static CargoResponse ToCargoResponse(
        this TblAdministracionCargo c) =>
        new(c.Id, c.Nombrecargo);
}